using EGamePlay;
using EGamePlay.Combat;
using System;
using System.Collections.Generic;
using static ACTGameEditor.IdleSkillMapping;

namespace ACTGameEditor
{
    /// <summary>
    /// 技能冷却管理器（参考崩坏3 风格）。
    /// - 冷却时长使用 Luban 的 <see cref="SkillDemoSetting.Cooldown"/> 配置。
    /// - 通过 <see cref="Init"/> 或 <see cref="InitFromSlotConfig"/> 预注册技能列表。
    /// - 计时由外部传入的 deltaTime 驱动（推荐使用 GameTimeManager.WorldDelta 或 PlayerDelta），
    ///   从而自动适配时空断裂 / HitStop / 暂停等时间流速效果。
    /// - UI 可通过 Remaining/Total/Fill 查询并显示倒计时。
    /// </summary>
    public class SkillCDTimer
    {
        private readonly Dictionary<int, XCTimer> _skillTimers = new Dictionary<int, XCTimer>(16);
        public float GlobalCdRate { get; private set; } = 1f;

        /// <summary>使用槽位配置初始化冷却表（槽位→默认技能）。</summary>
        public void InitFromSlotConfig(SkillSlotConfig slotConfig)
        {
            _skillTimers.Clear();
            if (slotConfig == null || slotConfig.Slots == null) return;

            foreach (var entry in slotConfig.Slots)
            {
                if (entry.DefaultSkillId <= 0) continue;
                var timer = CreateOrGetTimerInternal(entry.DefaultSkillId, 0);
                timer.Pause();
            }
        }

        /// <summary>
        /// 使用 IdleSkillMapping 的映射列表初始化冷却表（兼容旧逻辑）。
        /// </summary>
        public void Init(List<Mapping> skillKeyList)
        {
            _skillTimers.Clear();
            if (skillKeyList == null || skillKeyList.Count == 0) return;

            foreach (var item in skillKeyList)
            {
                if (item.SkillId == 0) continue;
                var timer = CreateOrGetTimerInternal(item.SkillId, 0);
                timer.Pause();
            }
        }

        /// <summary>
        /// 设置全局冷却倍率（例如 Debug 调整）。
        /// 注意：这只是乘在每个 XCTimer.CDRate 外层的一个系数。
        /// </summary>
        public void SetCdRate(float cdRate)
        {
            GlobalCdRate = Math.Max(0f, cdRate);
            foreach (var kv in _skillTimers)
            {
                kv.Value.CDRate = GlobalCdRate;
            }
        }

        /// <summary>
        /// 获取指定技能的底层计时器；若尚未创建，则按照配置懒加载一个。
        /// </summary>
        public XCTimer GetTimer(int skillId)
        {
            if (_skillTimers.TryGetValue(skillId, out var timer)) return timer;
            return CreateOrGetTimerInternal(skillId);
        }

        /// <summary>
        /// 技能是否冷却结束（可施放）。
        /// 若该技能从未注册过冷却，则视为无CD，直接返回 true。
        /// </summary>
        public bool IsCDEnd(int skillId)
        {
            var timer = GetTimer(skillId);
            return timer == null || !timer.IsRunning;
        }

        /// <summary>
        /// 触发一次技能冷却。
        /// - 若 overrideDurationSeconds &gt; 0，则本次冷却使用覆盖时长；
        /// - 否则使用 <see cref="SkillDemoSetting.Cooldown"/> 配置。
        /// </summary>
        public void StartCooldown(int skillId, float overrideDurationSeconds = -1f)
        {
            var timer = GetOrCreateTimerWithDuration(skillId, overrideDurationSeconds);
            if (timer.TotalTime <= 0f)
            {
                // 没有冷却配置，视为无CD
                timer.Cancel();
                return;
            }

            timer.CDRate = GlobalCdRate;
            timer.Restart();
            timer.Start();
        }

        /// <summary> 立即结束指定技能的冷却（例如被动重置CD）。 </summary>
        public void ForceFinishCooldown(int skillId)
        {
            var timer = GetTimer(skillId);
            if (timer == null) return;
            timer.Cancel();
        }

        /// <summary> 剩余冷却时间（秒），未在冷却中时为 0。 </summary>
        public float GetRemaining(int skillId)
        {
            var timer = GetTimer(skillId);
            if (timer == null || !timer.IsRunning) return 0f;
            return timer.RemainingTime;
        }

        /// <summary> 冷却总时长（秒），若无配置则为 0。 </summary>
        public float GetTotal(int skillId)
        {
            var timer = GetTimer(skillId);
            return timer?.TotalTime ?? 0f;
        }

        /// <summary>
        /// 冷却进度（0-1），用于 UI 填充。
        /// - 0 表示无冷却或刚结束；
        /// - 1 表示冷却刚开始。
        /// </summary>
        public float GetFill01(int skillId)
        {
            var timer = GetTimer(skillId);
            if (timer == null || !timer.IsRunning) return 0f;
            // XCTimer.FillAmount: 已经过时间 / 总时长
            return 1f - timer.FillAmount;
        }

        /// <summary>
        /// 每帧更新所有技能冷却。
        /// 建议传入 GameTimeManager.WorldDelta 或 GameTimeManager.PlayerDelta，
        /// 以适配时空断裂 / HitStop / 暂停等效果。
        /// </summary>
        public void OnUpdate(float deltaTime)
        {
            if (_skillTimers.Count == 0) return;

            foreach (var timer in _skillTimers.Values)
            {
                timer.Update(deltaTime);
            }
        }

        private XCTimer GetOrCreateTimerWithDuration(int skillId, float durationOverrideSeconds)
        {
            if (_skillTimers.TryGetValue(skillId, out var existed))
            {
                if (durationOverrideSeconds >= 0f)
                    existed.Init(skillId.ToString(), durationOverrideSeconds);
                return existed;
            }

            return CreateOrGetTimerInternal(skillId, durationOverrideSeconds >= 0f ? durationOverrideSeconds : -1f);
        }

        private XCTimer CreateOrGetTimerInternal(int skillId, float defaultDurationSeconds = -1f)
        {
            if (_skillTimers.TryGetValue(skillId, out var existed)) return existed;

            float duration = ResolveCooldownDuration(skillId, defaultDurationSeconds);
            var timer = new XCTimer();
            timer.Init(skillId.ToString(), duration);
            timer.CDRate = GlobalCdRate;
            _skillTimers.Add(skillId, timer);
            return timer;
        }

        private static float ResolveCooldownDuration(int skillId, float defaultDurationSeconds)
        {
            var setting = SkillSettingMgr.Instance.GetSkillDemoSetting(skillId);
            float durationFromConfig = (setting != null && setting.Cooldown > 0f) ? setting.Cooldown : 0f;

            if (durationFromConfig > 0f) return durationFromConfig;
            if (defaultDurationSeconds > 0f) return defaultDurationSeconds;
            return 0f;
        }
    }
}
