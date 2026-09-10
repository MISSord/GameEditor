using ACTGameEditor.Combat.Ai;
using EGamePlay;
using EGamePlay.Combat;
using UnityEngine;

namespace ACTGameEditor.Combat
{
    /// <summary>
    /// 失衡计量条：不是 TimeBuff，不进 IdStatuses。
    /// 技能命中在 PostReceiveDamage 加值；条满由宿主进 Stagger，导演 Punish。
    /// </summary>
    public sealed class CombatMeterComponent : EGamePlay.Component
    {
        /// <summary>M1 冲击力常量。以后进 RoleAttri 只改这里。</summary>
        public const float DefaultImpact = 100f;

        const long RecoverTagSourceId = -2101;

        public override bool IsNeedUpdate { get; protected set; } = true;

        CombatEntity _owner;
        AttributeComponent _attri;
        readonly FloatModifier _vulnMod = new FloatModifier();
        bool _vulnApplied;
        bool _recoverTagOn;
        bool _configured;
        bool _drainPaused;

        float _max = 100f;
        float _holdSeconds = 1f;
        float _durationSeconds = 3f;
        float _vulnBonus = 0.5f;
        float _recoverIFrame = 0.4f;
        float _resist;
        float _idleRegenPerSec;
        int _chainCount = 1;
        bool _canDaze = true;
        float _parryDazeRatio = CombatParry.DefaultParryDazeRatio;

        float _current;
        float _holdRemain;
        float _recoverRemain;
        int _chainRemaining;
        DazePhase _phase;

        /// <summary>已按档配过表。玩家未 Configure，UI 不画条。</summary>
        public bool IsConfigured => _configured;

        /// <summary>0~1，UI 只读。</summary>
        public float CurrentRatio => _max > 0.0001f ? Mathf.Clamp01(_current / _max) : 0f;

        /// <summary>Opened / Draining：硬直中。</summary>
        public bool IsOpened => _phase == DazePhase.Opened || _phase == DazePhase.Draining;

        /// <summary>Opened/Draining 且仍有连携次数。</summary>
        public bool IsChainWindow => IsOpened && _chainRemaining > 0;

        /// <summary>招架成功写入的失衡倍率；走 AddDaze(Parry) 冲击力公式。</summary>
        public float ParryDazeRatio => _parryDazeRatio > 0.0001f
            ? _parryDazeRatio
            : CombatParry.DefaultParryDazeRatio;

        public DazePhase Phase => _phase;

        public override void Awake()
        {
            _owner = GetEntity<CombatEntity>();
            _attri = _owner?.GetComponent<AttributeComponent>();
            if (_owner != null)
                _owner.ListenActionPoint(ActionPointType.PostReceiveDamage, OnPostReceiveDamage);
        }

        public override void OnDestroy()
        {
            if (_owner != null)
                _owner.UnListenActionPoint(ActionPointType.PostReceiveDamage, OnPostReceiveDamage);
            ForceReset(notifyDirector: false);
            _owner = null;
            _attri = null;
            _configured = false;
        }

        public override void OnReset()
        {
            ForceReset(notifyDirector: true);
            _configured = false;
            _max = 100f;
            _holdSeconds = 1f;
            _durationSeconds = 3f;
            _vulnBonus = 0.5f;
            _recoverIFrame = 0.4f;
            _resist = 0f;
            _idleRegenPerSec = 0f;
            _chainCount = 1;
            _canDaze = true;
            _parryDazeRatio = CombatParry.DefaultParryDazeRatio;
        }

        /// <summary>生成时按杂兵/精英/首领档写入。缺表行回退杂兵。</summary>
        public void Configure(int dazeSettingId)
        {
            DazeSetting row = SkillSettingMgr.Instance != null
                ? SkillSettingMgr.Instance.GetDazeSetting(dazeSettingId)
                : null;
            if (row == null || row.Id != dazeSettingId)
                row = SkillSettingMgr.Instance != null
                    ? SkillSettingMgr.Instance.GetDazeSetting(DazeTierId.Grunt)
                    : null;

            if (row != null)
            {
                _max = row.MaxDaze > 1f ? row.MaxDaze : 100f;
                _holdSeconds = row.HoldSeconds > 0f ? row.HoldSeconds : 1f;
                _durationSeconds = row.DurationSeconds > 0.05f ? row.DurationSeconds : 3f;
                _vulnBonus = row.VulnBonus;
                _chainCount = row.ChainCount > 0 ? row.ChainCount : 1;
                _recoverIFrame = row.RecoverIFrame > 0f ? row.RecoverIFrame : 0.4f;
                _resist = Mathf.Clamp01(row.DazeResist);
                _idleRegenPerSec = row.IdleRegenPerSec;
                _canDaze = row.CanDaze != 0;
                _parryDazeRatio = row.ParryDazeRatio > 0.0001f
                    ? row.ParryDazeRatio
                    : CombatParry.DefaultParryDazeRatio;
            }

            _configured = true;
            _current = 0f;
            _phase = DazePhase.Idle;
            _chainRemaining = 0;
            _drainPaused = false;
        }

        /// <summary>
        /// 加失衡。Skill 的 raw 是段表 DazeRatio；DirectPct 的 raw 是上限百分比；Debug 的 raw 是绝对点数。
        /// </summary>
        public void AddDaze(float raw, DazeSource source)
        {
            if (!_configured || !_canDaze || _owner == null || _owner.IsDead)
                return;
            if (_phase == DazePhase.Opened || _phase == DazePhase.Draining || _phase == DazePhase.Recover)
                return;

            float points;
            if (source == DazeSource.DirectPct)
                points = _max * raw;
            else if (source == DazeSource.Debug)
                points = raw;
            else
                points = DefaultImpact * raw * (1f - _resist);

            if (points <= 0f)
                return;

            if (_phase == DazePhase.Idle)
                _phase = DazePhase.Charging;

            _current += points;
            if (_current >= _max)
                Open();
        }

        /// <summary>消耗一次连携次数。次数用尽后仍可普攻压窗。</summary>
        public bool TryConsumeChain()
        {
            if (!IsChainWindow)
                return false;
            _chainRemaining--;
            return true;
        }

        /// <summary>连携轴播放期间暂停掉条，避免窗在演出中途关掉。</summary>
        public void SetDrainPaused(bool paused) => _drainPaused = paused;

        /// <summary>调试：直接打满并破衡。</summary>
        public void DebugFill()
        {
            if (!_configured)
                Configure(DazeTierId.Grunt);
            if (!_canDaze || _owner == null || _owner.IsDead)
                return;
            if (IsOpened)
                return;
            if (_phase == DazePhase.Recover)
                ClearRecoverTag();
            _current = _max;
            Open();
        }

        /// <summary>调试：清条并立刻起身（不起身无敌）。</summary>
        public void DebugClear()
        {
            if (!_configured)
                return;
            bool wasStagger = IsOpened;
            ClearVuln();
            ClearRecoverTag();
            _current = 0f;
            _holdRemain = 0f;
            _recoverRemain = 0f;
            _chainRemaining = 0;
            _drainPaused = false;
            _phase = DazePhase.Idle;
            if (wasStagger)
                _owner?.EndDazeStagger();
        }

        /// <summary>死亡：撤易伤、还 Punish、清相位。不起身动画。</summary>
        public void OnOwnerDeath()
        {
            bool wasStagger = IsOpened;
            ClearVuln();
            ClearRecoverTag();
            _current = 0f;
            _holdRemain = 0f;
            _recoverRemain = 0f;
            _chainRemaining = 0;
            _drainPaused = false;
            _phase = DazePhase.Idle;
            if (wasStagger)
                CombatEncounterDirector.Instance?.NotifyBreakMeterClosed(_owner);
            _owner?.StateDirector?.ExitStagger();
        }

        public override void Update(float deltaTime)
        {
            if (!_configured || _owner == null || _owner.IsDead || deltaTime <= 0f)
                return;

            switch (_phase)
            {
                case DazePhase.Charging:
                    TickIdleRegen(deltaTime);
                    break;
                case DazePhase.Opened:
                    if (_drainPaused)
                        break;
                    _holdRemain -= deltaTime;
                    if (_holdRemain <= 0f)
                    {
                        _phase = DazePhase.Draining;
                        TickDrain(-_holdRemain);
                    }
                    break;
                case DazePhase.Draining:
                    if (_drainPaused)
                        break;
                    TickDrain(deltaTime);
                    break;
                case DazePhase.Recover:
                    _recoverRemain -= deltaTime;
                    if (_recoverRemain <= 0f)
                    {
                        ClearRecoverTag();
                        _phase = DazePhase.Idle;
                    }
                    break;
            }
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

        void TickDrain(float deltaTime)
        {
            float drainPerSec = _max / _durationSeconds;
            _current -= drainPerSec * deltaTime;
            if (_current > 0f)
                return;
            BeginRecover();
        }

        void Open()
        {
            _current = _max;
            _phase = DazePhase.Opened;
            _holdRemain = _holdSeconds;
            _chainRemaining = _chainCount;
            ApplyVuln();
            _owner?.BeginDazeStagger();
        }

        void BeginRecover()
        {
            _current = 0f;
            _phase = DazePhase.Recover;
            _recoverRemain = _recoverIFrame;
            _chainRemaining = 0;
            ClearVuln();
            _owner?.EndDazeStagger();
            PushRecoverTag();
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

        void ApplyVuln()
        {
            if (_vulnApplied || _vulnBonus <= 0f)
                return;
            if (_attri == null)
                _attri = _owner?.GetComponent<AttributeComponent>();
            if (_attri == null || !_attri.TryGetNumeric(AttributeType.Vulnerability, out FloatNumeric numeric))
                return;
            _vulnMod.Value = _vulnBonus;
            numeric.AddModifier(ModifyType.Add, _vulnMod);
            _vulnApplied = true;
        }

        void ClearVuln()
        {
            if (!_vulnApplied)
                return;
            _vulnApplied = false;
            if (_attri != null && _attri.TryGetNumeric(AttributeType.Vulnerability, out FloatNumeric numeric))
                numeric.RemoveModifier(ModifyType.Add, _vulnMod);
            _vulnMod.Value = 0f;
        }

        void PushRecoverTag()
        {
            if (_recoverTagOn || _owner?.TagHost == null || _recoverIFrame <= 0f)
                return;
            _owner.PushTag(TagSource.Manual(RecoverTagSourceId), CombatTags.CombatDazeRecover);
            _recoverTagOn = true;
        }

        void ClearRecoverTag()
        {
            if (!_recoverTagOn)
                return;
            _recoverTagOn = false;
            _owner?.PopTag(TagSource.Manual(RecoverTagSourceId), CombatTags.CombatDazeRecover);
        }

        void ForceReset(bool notifyDirector)
        {
            bool wasStagger = IsOpened;
            ClearVuln();
            ClearRecoverTag();
            _current = 0f;
            _holdRemain = 0f;
            _recoverRemain = 0f;
            _chainRemaining = 0;
            _drainPaused = false;
            _phase = DazePhase.Idle;
            if (notifyDirector && wasStagger)
                CombatEncounterDirector.Instance?.NotifyBreakMeterClosed(_owner);
        }
    }
}
