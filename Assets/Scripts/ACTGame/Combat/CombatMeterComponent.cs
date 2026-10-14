using EGamePlay;
using EGamePlay.Combat;
using UnityEngine;

namespace ACTGameEditor.Combat
{
    /// <summary>
    /// 敌人偏谐条：不是 TimeBuff，不进 IdStatuses。满条进 Ready，怪不硬直、不开连携窗。
    /// </summary>
    public sealed class CombatMeterComponent : EGamePlay.Component
    {
        /// <summary>M1 冲击力常量。以后进 RoleAttri 只改这里。</summary>
        public const float DefaultImpact = 100f;

        /// <summary>表 VacuumSeconds≤0 时的真空锁零秒。</summary>
        public const float DefaultVacuumSeconds = 5f;

        public override bool IsNeedUpdate { get; protected set; } = true;

        CombatEntity _owner;
        bool _configured;

        float _max = 100f;
        float _resist;
        float _idleRegenPerSec;
        bool _canDaze = true;
        float _parryDazeRatio = CombatParry.DefaultParryDazeRatio;
        float _vacuumSeconds = DefaultVacuumSeconds;

        float _current;
        float _vacuumRemain;
        DazePhase _phase;

        /// <summary>已按档配过表。玩家未 Configure，UI 不画条。</summary>
        public bool IsConfigured => _configured;

        /// <summary>血条下偏谐条。未配档或不可积蓄时不显示。</summary>
        public bool ShowHarmonyHud => _configured && _canDaze;

        /// <summary>失谐：条满，可按 F。不进 Stagger。</summary>
        public bool IsHarmonyReady => _phase == DazePhase.Ready;

        /// <summary>0~1，UI 只读。</summary>
        public float CurrentRatio => _max > 0.0001f ? Mathf.Clamp01(_current / _max) : 0f;

        /// <summary>招架成功写入的倍率；走 AddDaze(Parry) 冲击力公式。</summary>
        public float ParryDazeRatio => _parryDazeRatio > 0.0001f
            ? _parryDazeRatio
            : CombatParry.DefaultParryDazeRatio;

        public DazePhase Phase => _phase;

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
            ForceReset();
            _owner = null;
            _configured = false;
        }

        public override void OnReset()
        {
            ForceReset();
            _configured = false;
            _max = 100f;
            _resist = 0f;
            _idleRegenPerSec = 0f;
            _canDaze = true;
            _parryDazeRatio = CombatParry.DefaultParryDazeRatio;
            _vacuumSeconds = DefaultVacuumSeconds;
        }

        /// <summary>
        /// 按 DazeSetting.Id 配偏谐。id≤0 或缺行则保持未配置（不画条），禁止回退杂兵档。
        /// </summary>
        public void Configure(int dazeSettingId)
        {
            if (dazeSettingId <= 0)
                return;

            DazeSetting row = SkillSettingMgr.Instance != null
                ? SkillSettingMgr.Instance.GetDazeSetting(dazeSettingId)
                : null;
            if (row == null || row.Id != dazeSettingId)
                return;

            _max = row.MaxDaze > 1f ? row.MaxDaze : 100f;
            _resist = Mathf.Clamp01(row.DazeResist);
            _idleRegenPerSec = row.IdleRegenPerSec;
            _canDaze = row.CanDaze != 0;
            _parryDazeRatio = row.ParryDazeRatio > 0.0001f
                ? row.ParryDazeRatio
                : CombatParry.DefaultParryDazeRatio;
            _vacuumSeconds = row.VacuumSeconds > 0.05f
                ? row.VacuumSeconds
                : DefaultVacuumSeconds;

            _configured = true;
            _current = 0f;
            _phase = DazePhase.Idle;
            _vacuumRemain = 0f;
        }

        /// <summary>
        /// 加偏谐。Skill 的 raw 是段表 DazeRatio；DirectPct 的 raw 是上限百分比；Debug 的 raw 是绝对点数。
        /// 满条进 Ready，不硬直。
        /// </summary>
        public void AddDaze(float raw, DazeSource source)
        {
            if (!_configured || !_canDaze || _owner == null || _owner.IsDead)
                return;
            if (IsHarmonyLockedPhase())
                return;

            float points = ResolvePoints(raw, source);
            if (points <= 0f)
                return;

            if (_phase == DazePhase.Idle)
                _phase = DazePhase.Charging;

            _current += points;
            if (_current >= _max)
                EnterHarmonyReady();
        }

        /// <summary>调试：打满进 Ready。未配档时按怪物行 DazeSettingId 配，缺行不回退杂兵。</summary>
        public void DebugFill()
        {
            if (!_configured)
            {
                MonsterSetting monster = _owner != null && SkillSettingMgr.Instance != null
                    ? SkillSettingMgr.Instance.GetMonsterOrNull(_owner.MonsterId)
                    : null;
                if (monster == null || monster.DazeSettingId <= 0)
                    return;
                Configure(monster.DazeSettingId);
            }
            if (!_canDaze || _owner == null || _owner.IsDead)
                return;
            EnterHarmonyReady();
        }

        /// <summary>调试：清条回 Idle。</summary>
        public void DebugClear()
        {
            if (!_configured)
                return;
            _current = 0f;
            _vacuumRemain = 0f;
            _phase = DazePhase.Idle;
        }

        /// <summary>死亡：清相位。不起身动画。</summary>
        public void OnOwnerDeath()
        {
            _current = 0f;
            _vacuumRemain = 0f;
            _phase = DazePhase.Idle;
        }

        public override void Update(float deltaTime)
        {
            if (!_configured || _owner == null || _owner.IsDead)
                return;

            if (_phase == DazePhase.Vacuum)
            {
                float world = GameTimeManager.WorldDelta;
                if (world <= 0f)
                    return;
                _vacuumRemain -= world;
                if (_vacuumRemain > 0f)
                    return;
                _vacuumRemain = 0f;
                _current = 0f;
                _phase = DazePhase.Idle;
                return;
            }

            if (deltaTime <= 0f)
                return;

            if (_phase == DazePhase.Charging)
                TickIdleRegen(deltaTime);
        }

        float ResolvePoints(float raw, DazeSource source)
        {
            if (source == DazeSource.DirectPct)
                return _max * raw;
            if (source == DazeSource.Debug)
                return raw;
            return DefaultImpact * raw * (1f - _resist);
        }

        bool IsHarmonyLockedPhase()
        {
            return _phase == DazePhase.Ready
                || _phase == DazePhase.Executing
                || _phase == DazePhase.Vacuum;
        }

        void EnterHarmonyReady()
        {
            _current = _max;
            _phase = DazePhase.Ready;
            _vacuumRemain = 0f;
        }

        /// <summary>破坏技开轴：Ready → Executing。失败则条仍停在 Ready。</summary>
        public bool TryBeginHarmonyExecute()
        {
            if (_phase != DazePhase.Ready)
                return false;
            _phase = DazePhase.Executing;
            _current = _max;
            return true;
        }

        /// <summary>破坏技结束：条锁 0，拒绝加值，到期回 Idle。</summary>
        public void BeginHarmonyVacuum()
        {
            if (_phase != DazePhase.Executing && _phase != DazePhase.Ready)
                return;
            _current = 0f;
            _phase = DazePhase.Vacuum;
            _vacuumRemain = _vacuumSeconds;
        }

        void TickIdleRegen(float deltaTime)
        {
            if (_idleRegenPerSec <= 0f || _current <= 0f)
                return;
            _current -= _max * _idleRegenPerSec * deltaTime;
            if (_current > 0f)
                return;
            _current = 0f;
            _phase = DazePhase.Idle;
        }

        void OnPostReceiveDamage(Entity action)
        {
            if (action is not DamageAction damage || _owner == null)
                return;
            if (damage.DamageActionEffect.HasFlag(DamageActionEffect.Dodge)
                || damage.DamageActionEffect.HasFlag(DamageActionEffect.Immunity)
                || damage.DamageActionEffect.HasFlag(DamageActionEffect.Interrupt)
                || damage.DamageActionEffect.HasFlag(DamageActionEffect.Parry))
                return;
            if (damage.DamageSource != DamageSource.Skill)
                return;
            if (_owner.IsDead)
                return;

            int skillId = damage.TriggerContext.SourceAbility != null
                ? damage.TriggerContext.SourceAbility.SkillID
                : 0;
            int segment = damage.TriggerContext.DamageSegmentIndex;
            SkillDamageSetting setting = SkillSettingMgr.Instance.GetSkillDamageSetting(skillId, segment);
            if (setting == null || setting.DazeRatio <= 0f)
                return;

            AddDaze(setting.DazeRatio, DazeSource.Skill);
        }

        void ForceReset()
        {
            _current = 0f;
            _vacuumRemain = 0f;
            _phase = DazePhase.Idle;
        }
    }
}
