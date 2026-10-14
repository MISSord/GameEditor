using EGamePlay;
using EGamePlay.Combat;
using UnityEngine;

namespace ACTGameEditor.Combat
{
    /// <summary>共振度相位。白条 Intact；扣光 Breaking → Immobilized 黄条；起身 Recover 回满。</summary>
    public enum VibrationPhase : byte
    {
        Intact = 0,
        Breaking = 1,
        Immobilized = 2,
        Recover = 3,
    }

    /// <summary>
    /// 敌人共振度：不是 TimeBuff，不进 IdStatuses，不改 Vulnerability。
    /// 黄条 / 起身 i-frame 走世界钟，不乘实体 TimeScale。
    /// 招架 / 谐度破坏按 <see cref="VibrationRule"/> 全局百分比砍条，不乘抗性。
    /// </summary>
    public sealed class CombatVibrationComponent : EGamePlay.Component
    {
        /// <summary>系统 Source：瘫痪窗 <c>State.VibrationBreak</c>。不是 Buff，不是 MoveForbid。</summary>
        public const long BreakTagSourceId = 90002;

        /// <summary>系统 Source：起身 <c>Combat.DazeRecover</c>。</summary>
        public const long RecoverTagSourceId = 90003;

        static readonly TagSource BreakTagSource = TagSource.Manual(BreakTagSourceId);
        static readonly TagSource RecoverTagSource = TagSource.Manual(RecoverTagSourceId);

        public override bool IsNeedUpdate { get; protected set; } = true;

        CombatEntity _owner;
        bool _configured;
        bool _canBreak;
        bool _breakTagPushed;
        bool _recoverTagPushed;
        bool _tuneBreakArmed;

        float _max = 100f;
        float _resist;
        float _idleRegenPerSec;
        float _impact;
        float _breakHoldSeconds;
        float _immobilizeSeconds;
        float _recoverIFrame;

        float _current;
        float _holdRemain;
        float _immobilizeRemain;
        float _recoverRemain;
        VibrationPhase _phase;

        /// <summary>已按怪物行配过且 CanBreak。玩家 / 杂兵为 false。</summary>
        public bool IsConfigured => _configured;

        /// <summary>Intact / Breaking / Immobilized 才画头顶韧条。Recover / 死亡 / 无条不画。</summary>
        public bool ShowVibrationHud =>
            _configured && _canBreak
            && _phase != VibrationPhase.Recover
            && _owner != null && !_owner.IsDead;

        /// <summary>黄条倒计时中。窗内伤害公式不变。</summary>
        public bool IsYellowBar => _phase == VibrationPhase.Immobilized;

        /// <summary>Intact 为剩余共振度；Breaking 为 0；Immobilized 为黄条剩余比例。</summary>
        public float DisplayRatio
        {
            get
            {
                if (_phase == VibrationPhase.Immobilized)
                    return _immobilizeSeconds > 0.0001f
                        ? Mathf.Clamp01(_immobilizeRemain / _immobilizeSeconds)
                        : 0f;
                if (_phase == VibrationPhase.Breaking)
                    return 0f;
                return _max > 0.0001f ? Mathf.Clamp01(_current / _max) : 0f;
            }
        }

        /// <summary>当前相位。UI / 调试只读。</summary>
        public VibrationPhase Phase => _phase;

        public override void Awake()
        {
            _owner = GetEntity<CombatEntity>();
            if (_owner != null)
                _owner.ListenActionPoint(ActionPointType.PostReceiveDamage, OnPostReceiveDamage);
        }

        public override void OnDestroy()
        {
            if (_owner != null)
                _owner.UnListenActionPoint(ActionPointType.PostReceiveDamage, OnPostReceiveDamage);
            ClearTags();
            ForceReset();
            _owner = null;
            _configured = false;
        }

        public override void OnReset()
        {
            ClearTags();
            ForceReset();
            _configured = false;
            _canBreak = false;
            _max = 100f;
            _resist = 0f;
            _idleRegenPerSec = 0f;
            _impact = 0f;
            _breakHoldSeconds = 0f;
            _immobilizeSeconds = 0f;
            _recoverIFrame = 0f;
            _tuneBreakArmed = false;
        }

        /// <summary>
        /// 按 MonsterId 查 <see cref="VibrationSetting"/>。缺行、CanBreak=0 或 Max≤0 则无条，禁止回退第一行。
        /// </summary>
        public void Configure(int monsterId)
        {
            if (monsterId <= 0)
                return;

            VibrationSetting row = SkillSettingMgr.Instance != null
                ? SkillSettingMgr.Instance.GetVibrationSettingOrNull(monsterId)
                : null;
            if (row == null || row.MonsterId != monsterId || row.CanBreak == 0 || row.MaxVibration <= 0.0001f)
                return;

            VibrationRule rule = SkillSettingMgr.Instance.GetVibrationRuleOrNull();
            _impact = rule != null && rule.VibrationImpact > 0f ? rule.VibrationImpact : 0f;

            _max = row.MaxVibration;
            _resist = Mathf.Clamp01(row.VibrationResist);
            _idleRegenPerSec = row.IdleRegenPerSec;
            _breakHoldSeconds = row.BreakHoldSeconds;
            _immobilizeSeconds = row.ImmobilizeSeconds;
            _recoverIFrame = row.RecoverIFrame;
            _canBreak = true;
            _configured = true;
            _current = _max;
            _phase = VibrationPhase.Intact;
            _holdRemain = 0f;
            _immobilizeRemain = 0f;
            _recoverRemain = 0f;
        }

        /// <summary>
        /// 扣共振度。仅 Intact 生效。段表路径已乘抗性；招架 / F 请走 <see cref="ApplyFromMaxPct"/>，不乘抗性。
        /// </summary>
        public void ApplyVibration(float points)
        {
            if (!_configured || !_canBreak || _owner == null || _owner.IsDead)
                return;
            if (_phase != VibrationPhase.Intact)
                return;
            if (points <= 0f)
                return;

            _current -= points;
            if (_current > 0f)
                return;
            _current = 0f;
            BeginBreak();
        }

        /// <summary>
        /// 按目标 Max 的百分比削韧。招架 / F 用。不乘 <c>VibrationResist</c>。pct≤0 忽略。
        /// </summary>
        public void ApplyFromMaxPct(float pct)
        {
            if (pct <= 0.0001f)
                return;
            ApplyVibration(_max * pct);
        }

        /// <summary>黄闪招架成交：读全局 <c>ParryVibrationPct</c>。缺表行不回退常量。</summary>
        public void ApplyParryCut()
        {
            VibrationRule rule = SkillSettingMgr.Instance != null
                ? SkillSettingMgr.Instance.GetVibrationRuleOrNull()
                : null;
            if (rule == null || rule.ParryVibrationPct <= 0.0001f)
                return;
            ApplyFromMaxPct(rule.ParryVibrationPct);
        }

        /// <summary>
        /// F 轴启动时武装一次 TuneBreak；第一次有效命中消费。
        /// 轴结束不要 Disarm：同帧末盒在 Session.Finish 之后才 Flush。
        /// 已破韧 / 起身时不武装：瘫痪窗拒绝削韧，武装了也只会被无效命中吞掉。
        /// </summary>
        public void ArmHarmonyTuneBreak()
        {
            if (!_configured || !_canBreak || _phase != VibrationPhase.Intact)
                return;
            _tuneBreakArmed = true;
        }

        /// <summary>调试：立刻打空并破韧。未配条则忽略。</summary>
        public void DebugBreak()
        {
            if (!_configured || !_canBreak || _owner == null || _owner.IsDead)
                return;
            if (_phase != VibrationPhase.Intact)
                return;
            _current = 0f;
            BeginBreak();
        }

        /// <summary>死亡：清 Tag 与相位。不 ExitStagger（死亡槽会清）。</summary>
        public void OnOwnerDeath()
        {
            ClearTags();
            _current = 0f;
            _holdRemain = 0f;
            _immobilizeRemain = 0f;
            _recoverRemain = 0f;
            _phase = VibrationPhase.Intact;
            _tuneBreakArmed = false;
        }

        public override void Update(float deltaTime)
        {
            if (!_configured || !_canBreak || _owner == null || _owner.IsDead)
                return;

            float world = GameTimeManager.WorldDelta;
            if (world <= 0f)
                return;

            switch (_phase)
            {
                case VibrationPhase.Intact:
                    TickIdleRegen(world);
                    return;
                case VibrationPhase.Breaking:
                    _holdRemain -= world;
                    if (_holdRemain > 0f)
                        return;
                    EnterImmobilized();
                    return;
                case VibrationPhase.Immobilized:
                    _immobilizeRemain -= world;
                    if (_immobilizeRemain > 0f)
                        return;
                    EnterRecover();
                    return;
                case VibrationPhase.Recover:
                    _recoverRemain -= world;
                    if (_recoverRemain > 0f)
                        return;
                    EndRecover();
                    return;
            }
        }

        void OnPostReceiveDamage(Entity action)
        {
            if (action is not DamageAction damage || _owner == null)
                return;
            if (damage.DamageSource != DamageSource.Skill)
                return;
            if (_owner.IsDead)
                return;
            if (!_configured || !_canBreak)
                return;

            int skillId = damage.TriggerContext.SourceAbility != null
                ? damage.TriggerContext.SourceAbility.SkillID
                : 0;
            if (_tuneBreakArmed && CombatHarmonyBreak.IsHarmonyBreakSkill(skillId))
            {
                TryConsumeHarmonyTuneBreak(damage);
                return;
            }

            if (damage.DamageActionEffect.HasFlag(DamageActionEffect.Dodge)
                || damage.DamageActionEffect.HasFlag(DamageActionEffect.Immunity)
                || damage.DamageActionEffect.HasFlag(DamageActionEffect.Interrupt)
                || damage.DamageActionEffect.HasFlag(DamageActionEffect.Parry))
                return;
            if (_phase != VibrationPhase.Intact)
                return;
            if (_impact <= 0f)
                return;

            int segment = damage.TriggerContext.DamageSegmentIndex;
            SkillSettingMgr mgr = SkillSettingMgr.Instance;
            if (mgr == null)
                return;
            SkillDamageSetting setting = mgr.GetSkillDamageSetting(skillId, segment);
            if (setting == null || setting.SkillId != skillId || setting.VibrationRatio <= 0f)
                return;

            float points = _impact * setting.VibrationRatio * (1f - _resist);
            ApplyVibration(points);
        }

        /// <summary>
        /// F 多段盒只砍一次全局比例。闪避 / 免疫 / 非 Intact 不消费武装，避免瘫痪中按 F 把 30% 砍空。
        /// </summary>
        void TryConsumeHarmonyTuneBreak(DamageAction damage)
        {
            if (damage.DamageActionEffect.HasFlag(DamageActionEffect.Dodge)
                || damage.DamageActionEffect.HasFlag(DamageActionEffect.Immunity)
                || damage.DamageActionEffect.HasFlag(DamageActionEffect.Interrupt)
                || damage.DamageActionEffect.HasFlag(DamageActionEffect.Parry))
                return;
            if (_phase != VibrationPhase.Intact)
                return;

            _tuneBreakArmed = false;
            VibrationRule rule = SkillSettingMgr.Instance != null
                ? SkillSettingMgr.Instance.GetVibrationRuleOrNull()
                : null;
            if (rule == null || rule.TuneBreakVibrationPct <= 0.0001f)
                return;
            ApplyFromMaxPct(rule.TuneBreakVibrationPct);
        }

        void BeginBreak()
        {
            _phase = VibrationPhase.Breaking;
            _holdRemain = _breakHoldSeconds;
            _owner.ApplyVibrationBreak();
            PushBreakTag();
            if (_holdRemain <= 0f)
                EnterImmobilized();
        }

        void EnterImmobilized()
        {
            _phase = VibrationPhase.Immobilized;
            _holdRemain = 0f;
            _immobilizeRemain = _immobilizeSeconds;
            if (_immobilizeRemain <= 0f)
                EnterRecover();
        }

        void EnterRecover()
        {
            _immobilizeRemain = 0f;
            _current = _max;
            PopBreakTag();
            if (_recoverIFrame > 0f)
            {
                _phase = VibrationPhase.Recover;
                _recoverRemain = _recoverIFrame;
                PushRecoverTag();
            }
            else
            {
                _phase = VibrationPhase.Intact;
                _recoverRemain = 0f;
            }

            _owner.ApplyVibrationRecover();
        }

        void EndRecover()
        {
            PopRecoverTag();
            _recoverRemain = 0f;
            _phase = VibrationPhase.Intact;
            _current = _max;
        }

        void TickIdleRegen(float worldDelta)
        {
            if (_idleRegenPerSec <= 0f || _current >= _max)
                return;
            _current += _max * _idleRegenPerSec * worldDelta;
            if (_current > _max)
                _current = _max;
        }

        void PushBreakTag()
        {
            if (_breakTagPushed || _owner?.TagHost == null)
                return;
            _owner.TagHost.PushTag(BreakTagSource, CombatTags.StateVibrationBreak);
            _breakTagPushed = true;
        }

        void PopBreakTag()
        {
            if (!_breakTagPushed || _owner?.TagHost == null)
            {
                _breakTagPushed = false;
                return;
            }
            _owner.TagHost.PopTag(BreakTagSource, CombatTags.StateVibrationBreak);
            _breakTagPushed = false;
        }

        void PushRecoverTag()
        {
            if (_recoverTagPushed || _owner?.TagHost == null)
                return;
            _owner.TagHost.PushTag(RecoverTagSource, CombatTags.CombatDazeRecover);
            _recoverTagPushed = true;
        }

        void PopRecoverTag()
        {
            if (!_recoverTagPushed || _owner?.TagHost == null)
            {
                _recoverTagPushed = false;
                return;
            }
            _owner.TagHost.PopTag(RecoverTagSource, CombatTags.CombatDazeRecover);
            _recoverTagPushed = false;
        }

        void ClearTags()
        {
            PopBreakTag();
            PopRecoverTag();
        }

        void ForceReset()
        {
            _current = 0f;
            _holdRemain = 0f;
            _immobilizeRemain = 0f;
            _recoverRemain = 0f;
            _phase = VibrationPhase.Intact;
            _breakTagPushed = false;
            _recoverTagPushed = false;
            _tuneBreakArmed = false;
        }
    }
}
