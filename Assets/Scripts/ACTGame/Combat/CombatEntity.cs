using System;
using System.Collections.Generic;
using EGamePlay;
using EGamePlay.Combat;
using EGamePlay.Unity;
using UnityEngine;

namespace ACTGameEditor.Combat
{
    /// <summary>
    /// 战斗实体：组件装配、门控查询、时间轴消息与受击/死亡落地。
    /// </summary>
    public sealed class CombatEntity : Entity, IPosition, ICombatUnit
    {
        #region ICombatUnit

        Entity ICombatUnit.Entity => this;
        long ICombatUnit.Id => Id;
        bool ICombatUnit.isTruePlayer => isTruePlayer;
        bool ICombatUnit.UsesPlayerCombatClock => UsesPlayerCombatClock;

        #endregion

        #region 标识 / Transform

        public uint NetId { get; private set; }
        public bool isTruePlayer;
        /// <summary>玩家三人小队成员。候场也走玩家钟。</summary>
        public bool IsPlayerSquad { get; private set; }
        /// <summary>小队槽 0/1/2；非小队为 -1。</summary>
        public int SquadSlot { get; private set; }
        /// <summary>上场 / 退场中 / 候场。非小队恒为 OnField。</summary>
        public SquadPresence SquadPresence { get; private set; }
        /// <summary>候场隐藏。非小队恒为 false。</summary>
        public bool IsBench => IsPlayerSquad && SquadPresence == SquadPresence.Bench;
        int _playerCombatClockHold;
        public AgentTag CurAgent { get; set; }
        public Transform ModelTrans { get; set; }
        public Transform RootTransform { get; set; }
        public Vector3 Position { get; set; }
        public Quaternion Rotation { get; set; }
        public ActPlayer AttackPlayer { get; set; }

        #endregion

        #region 组件引用

        public VitalComponent CurrentVital { get; private set; }
        public CombatTagComponent TagHost { get; private set; }
        public ActionPointComponent ActionPoints { get; private set; }
        /// <summary>Buff 列表与按优先级分发。</summary>
        public StatusComponent Status { get; private set; }
        /// <summary>技能组等级。</summary>
        public SkillLevelComponent SkillLevels { get; private set; }
        public EntityTimeScaleComponent TimeScale { get; private set; }
        public CombatFormComponent FormComponent => _formComponent;
        /// <summary>技能队列与 CD。</summary>
        public ActSpellComponent Spell => _spell;
#if UNITY
        /// <summary>动画与 MotionDirector。</summary>
        public AnimComponent Anim => _anim;
#endif
        /// <summary>失衡计量。玩家未 Configure，<see cref="CombatMeterComponent.IsConfigured"/> 为 false。</summary>
        public CombatMeterComponent DazeMeter { get; private set; }

        public DamageActionAbility DamageAbility { get; private set; }
        public ResourceActionAbility ResourceAbility { get; private set; }
        public AddStatusActionAbility AddStatusAbility { get; private set; }

        #endregion

        #region 技能占轴

        public ISkillExecutionHandle ActiveExecution { get; set; }

        public ActSkillRunner SpellingExecution
        {
            get => ActiveExecution as ActSkillRunner;
            set => ActiveExecution = value;
        }

        #endregion

        #region 状态

        CombatStateDirector _stateDirector;
        PlayerStateEnum _curState = PlayerStateEnum.Idle;

        public PlayerStateEnum CurState => _curState;
        public MoveTypeEnum CurMoveState { get; set; } = MoveTypeEnum.Idle;
        public CombatStateDirector StateDirector => _stateDirector;

        public void ApplyStateFromDirector(PlayerStateEnum state) => _curState = state;

        #endregion

        #region 门控（Tag + 状态 + 技能覆盖）

        const float MoveWeightEpsilon = 0.0001f;
        float _skillMoveWeight = 1f;
        bool _timedMoveLock;

        public bool IsCanCauseHarm => TagHost != null && !TagHost.HasIndex(TagHost.AttackDamageForbidIndex);

        /// <summary>水平走跑倍率：死/受击/禁移/限时锁为 0，其余用技能覆盖（占轴默认 0）。</summary>
        public float MoveWeight
        {
            get
            {
                if (IsDead || _timedMoveLock)
                    return 0f;
                if (CurState == PlayerStateEnum.Hit || CurState == PlayerStateEnum.Control
                    || CurState == PlayerStateEnum.Stagger)
                    return 0f;
                if (TagHost == null || TagHost.HasIndex(TagHost.MoveForbidIndex))
                    return 0f;
                return Mathf.Clamp01(_skillMoveWeight);
            }
        }

        /// <summary>Locomotion 当前能否水平位移。</summary>
        public bool IsCanMove => MoveWeight > MoveWeightEpsilon;

        public bool IsCanJump
        {
            get
            {
                if (IsDead || ActiveExecution != null || CurState == PlayerStateEnum.Hit
                    || CurState == PlayerStateEnum.Control || CurState == PlayerStateEnum.Stagger)
                    return false;
                if (_timedMoveLock)
                    return false;
                if (TagHost != null && TagHost.HasIndex(TagHost.MoveForbidIndex))
                    return false;
#if UNITY
                if (_inputMove == null || !_inputMove.Enable)
                    return false;
#endif
                return true;
            }
        }

        public bool IsAirborne
        {
            get
            {
#if UNITY
                return _inputMove != null && !_inputMove.IsGrounded;
#else
                return false;
#endif
            }
        }

        public bool IsDead => _stateDirector != null && _stateDirector.IsDead
            || (CurrentVital != null && CurrentVital.CheckDead());

        public bool IsCanSpellSkill => !IsDead
            && TagHost != null
            && !TagHost.HasIndex(TagHost.UnStoppedIndex)
            && !TagHost.HasIndex(TagHost.SkillForbidIndex)
            && CurState != PlayerStateEnum.Hit
            && CurState != PlayerStateEnum.Control
            && CurState != PlayerStateEnum.Stagger;

        /// <summary>高优先级自身取消（大招顶普攻）。沉默/眩晕的 SkillForbid 会挡住；闪避不走这里。</summary>
        public bool IsCanSelfCancelSkill => !IsDead
            && TagHost != null
            && !TagHost.HasIndex(TagHost.SkillForbidIndex)
            && CurState != PlayerStateEnum.Hit
            && CurState != PlayerStateEnum.Control
            && CurState != PlayerStateEnum.Stagger;

        /// <summary>
        /// 闪避：禁移（眩晕）不可；仅禁技能（沉默）可以。
        /// 短受击仍可闪；不认 UnStopped / SkillForbid，避免霸体和沉默误伤闪避。
        /// </summary>
        public bool IsCanRollSkill => !IsDead
            && TagHost != null
            && !TagHost.HasIndex(TagHost.MoveForbidIndex)
            && CurState != PlayerStateEnum.Control
            && CurState != PlayerStateEnum.Stagger;

        /// <summary>招架：与闪避同一道门，受击中可出。</summary>
        public bool IsCanParrySkill => IsCanRollSkill;

        public bool IsUnstopped => TagHost != null && TagHost.HasIndex(TagHost.UnStoppedIndex);

        #endregion

        #region 私有字段

        AbilityComponent _abilityComponent;
        ActSpellComponent _spell;
        CombatFormComponent _formComponent;
        ICombatTimelinePresenter _timelinePresenter;

#if UNITY
        InputMoveComponent _inputMove;
        AnimComponent _anim;
        public bool useAnimaRoot;
        public bool IsGrounded => _inputMove != null && _inputMove.IsGrounded;
#endif

        #endregion

        #region 生命周期

        public override void Awake(object initData)
        {
            var data = (GameObjectData)initData;
            InitializeIdentity(data);
            AddCoreComponents(data);
            AddPresentationComponents();
#if UNITY
            AddUnityComponents(data);
#endif
            AttachActionAbilities();
        }

        public override void OnDestroy()
        {
            CombatPresentationDirector.StopByEntity(Id, keepDeathDissolve: true);
            ClearLifecycleRefs();
        }

        public override void OnReset()
        {
            isTruePlayer = false;
            IsPlayerSquad = false;
            SquadSlot = -1;
            SquadPresence = SquadPresence.OnField;
            _playerCombatClockHold = 0;
            NetId = 0;
            CurMoveState = MoveTypeEnum.Idle;
            _curState = PlayerStateEnum.Idle;
            AttackPlayer = null;
            ModelTrans = null;
            RootTransform = null;
            Position = default;
            Rotation = default;
            ActiveExecution = null;
            SkillLevels = null;
            _skillMoveWeight = 1f;
            _timedMoveLock = false;
        }

        public override void Update(float deltaTime)
        {
            if (IsBench)
            {
                _spell?.Update(deltaTime);
                return;
            }

            if (RootTransform != null)
            {
                Position = RootTransform.position;
                Rotation = RootTransform.rotation;
            }

            _stateDirector?.Tick(CombatTimeClock.GetLayerTime(this));

            for (int i = 0; i < UpdateComponents.Count; i++)
                UpdateComponents[i].Update(deltaTime);
        }

        /// <inheritdoc />
        public void TickPendingSkillInput()
        {
            if (IsPlayerSquad && (!isTruePlayer || IsBench || SquadPresence == SquadPresence.Exiting))
                return;
            if (AttackPlayer is IAttackPlayer attack)
                attack.TickSkillInput();
        }

        public override void FixedUpdate(float fixDeltaTime)
        {
            if (IsBench)
                return;
            for (int i = 0; i < FixedUpdateComponents.Count; i++)
                FixedUpdateComponents[i].FixedUpdate(fixDeltaTime);
        }

        void InitializeIdentity(GameObjectData data)
        {
            isTruePlayer = data.isTruePlayer;
            IsPlayerSquad = data.IsPlayerSquad;
            SquadSlot = data.SquadSlot;
            SquadPresence = data.IsPlayerSquad && !data.isTruePlayer
                ? SquadPresence.Bench
                : SquadPresence.OnField;
            NetId = PlayerManager.GetID();
        }

        void AddCoreComponents(GameObjectData data)
        {
            Status = AddComponent<StatusComponent>();
            TagHost = AddComponent<CombatTagComponent>();

            _stateDirector = new CombatStateDirector();
            _stateDirector.Bind(this);

            AddComponent<AttributeComponent>().InitializeCharacter(data.CharacterId, data.Level);
            SkillLevels = AddComponent<SkillLevelComponent>();

            TimeScale = AddComponent<EntityTimeScaleComponent>();
            ActionPoints = AddComponent<ActionPointComponent>();
            _abilityComponent = AddComponent<AbilityComponent>();
            _spell = AddComponent<ActSpellComponent>();
            _formComponent = AddComponent<CombatFormComponent>();

            CurrentVital = AddComponent<VitalComponent>();
            CurrentVital.InitVital();
            AddComponent<CombatPoiseComponent>();
            DazeMeter = AddComponent<CombatMeterComponent>();
        }

        void AddPresentationComponents()
        {
            _timelinePresenter = AddComponent<CombatTimelinePresenter>();
            AddComponent<CombatActionPointFxRouter>();
            AddComponent<CombatHitResolver>();
        }

#if UNITY
        void AddUnityComponents(GameObjectData data)
        {
            AddComponent<AnimComponent>(initData: data);
            _anim = GetComponent<AnimComponent>();
            _inputMove = AddComponent<InputMoveComponent>(data);
            useAnimaRoot = false;
        }
#endif

        void AttachActionAbilities()
        {
            DamageAbility = AttachAction<DamageActionAbility>();
            ResourceAbility = AttachAction<ResourceActionAbility>();
            AddStatusAbility = AttachAction<AddStatusActionAbility>();
        }

        void ClearLifecycleRefs()
        {
            _stateDirector?.Unbind();
            _stateDirector = null;
            TagHost = null;
            Status = null;
            SkillLevels = null;
            ActionPoints = null;
            TimeScale = null;
            CurrentVital = null;
            DazeMeter = null;
            _formComponent = null;
            _timelinePresenter = null;
            _abilityComponent = null;
            _spell = null;
            ActiveExecution = null;
            DamageAbility = null;
            ResourceAbility = null;
            AddStatusAbility = null;
#if UNITY
            _inputMove = null;
            _anim = null;
            useAnimaRoot = false;
#endif
        }

        #endregion

        #region 输入 / Locomotion

        /// <summary>本地玩家接管 / 死亡时开关电机 Tick，技能不要写这里。</summary>
        public void ChangeInputMoveState(bool state)
        {
#if UNITY
            if (_inputMove != null)
                _inputMove.Enable = state;
#endif
        }

        /// <summary>电机 AutoRotate 开关（闪避、时间轴 SetCanRotate）。</summary>
        public void ChangeInputRotateState(bool state)
        {
#if UNITY
            _inputMove?.SetRotationEnabled(state);
#endif
        }

        /// <summary>技能开轴：默认站桩，时间轴 SetCanMove 可再打开。</summary>
        public void BeginSkillMoveLock() => _skillMoveWeight = 0f;

        /// <summary>技能交轴：恢复满权；受击/禁移仍由 <see cref="MoveWeight"/> 合成压掉。</summary>
        public void EndSkillMoveLock() => _skillMoveWeight = 1f;

        /// <summary>闪避起步：非走路时锁存快跑，结束移动时接疾跑。</summary>
        public void ArmSprintFromDodge()
        {
#if UNITY
            _inputMove?.ArmSprintFromDodge();
#endif
        }

        /// <summary>时间轴 SetCanMove：只改技能覆盖，不拧电机 Enable。</summary>
        public void SetSkillMoveAllowed(bool allowed)
        {
            _skillMoveWeight = allowed ? 1f : 0f;
            if (allowed)
                _timedMoveLock = false;
        }

        /// <summary>时间轴 SetUnMoveTime：限时强制权重 0，到期只清锁，不改技能覆盖。</summary>
        public void SetTimedMoveLock(bool locked) => _timedMoveLock = locked;

        #endregion

        #region 行动点（转发）

        public void ListenActionPoint(ActionPointType actionPointType, Action<Entity> action) =>
            ActionPoints?.ListenActionPoint(actionPointType, action);

        public void UnListenActionPoint(ActionPointType actionPointType, Action<Entity> action) =>
            ActionPoints?.UnListenActionPoint(actionPointType, action);

        public void TriggerActionPoint(ActionPointType actionPointType, Entity action) =>
            ActionPoints?.TriggerActionPoint(actionPointType, action);

        #endregion

        #region Tag（转发）

        public bool HasTag(string tagName) => TagHost != null && TagHost.HasTag(tagName);

        public bool CanSpellSkillWithTagLists(List<string> required, List<string> blocked) =>
            TagHost != null && TagHost.CanSpellSkillWithTagLists(required, blocked);

        public void PushTag(TagSource source, string tagName) => TagHost?.PushTag(source, tagName);
        public void PopTag(TagSource source, string tagName) => TagHost?.PopTag(source, tagName);
        public void PopTagsFrom(TagSource source) => TagHost?.PopTagsFrom(source);

        public void GrantTagFor(TagSource source, string tagName, float durationSeconds) =>
            TagHost?.GrantTagFor(source, tagName, durationSeconds);

        public void GrantUnstoppedFor(float durationSeconds, TagSource source) =>
            TagHost?.GrantUnstoppedFor(durationSeconds, source);

        #endregion

        #region 时间流速（转发）

        public float GetTimeScale() => TimeScale != null ? TimeScale.GetTimeScale() : 1f;

        /// <summary>小队成员（含候场）、当前主控，或 SkillTimeStop 发起者，走玩家钟。</summary>
        public bool UsesPlayerCombatClock => IsPlayerSquad || isTruePlayer || _playerCombatClockHold > 0;

        /// <summary>小队 Presence 只由 <see cref="CombatSquad"/> / <see cref="PlayerManager"/> 写入。</summary>
        public void SetSquadPresence(SquadPresence presence)
        {
            if (!IsPlayerSquad)
                return;
            SquadPresence = presence;
        }

        /// <summary>关掉 CharacterController 再瞬移，避免胶囊穿透。</summary>
        public void WarpTo(Vector3 pos, Quaternion rot)
        {
            Transform root = RootTransform;
            if (root == null)
            {
                Position = pos;
                Rotation = rot;
                return;
            }

            CharacterController cc = root.GetComponent<CharacterController>();
            if (cc != null && cc.enabled)
            {
                cc.enabled = false;
                root.SetPositionAndRotation(pos, rot);
                cc.enabled = true;
            }
            else
            {
                root.SetPositionAndRotation(pos, rot);
            }

            Physics.SyncTransforms();
            Position = pos;
            Rotation = rot;
        }

        /// <summary>SkillTimeStop 开始时调用，与 <see cref="RemovePlayerCombatClockHold"/> 成对。</summary>
        public void AddPlayerCombatClockHold() => _playerCombatClockHold++;

        /// <summary>SkillTimeStop 结束或撤销时调用。</summary>
        public void RemovePlayerCombatClockHold()
        {
            if (_playerCombatClockHold > 0)
                _playerCombatClockHold--;
        }

        /// <summary>叠加实体时间倍率（冻结/减速等）。命中顿帧请用 <see cref="ApplyHitStopTimeScale"/>。</summary>
        public void AddTimeScaleModifier(int sourceId, float scale)
        {
            TimeScale?.AddTimeScaleModifier(sourceId, scale);
            RefreshAnimSpeed();
        }

        /// <summary>按来源撤实体时间倍率。</summary>
        public void RemoveTimeScaleModifierBySource(int sourceId)
        {
            TimeScale?.RemoveTimeScaleModifierBySource(sourceId);
            RefreshAnimSpeed();
        }

        /// <summary>命中顿帧：替换本单位上的 HitStop 实体倍率。</summary>
        public void ApplyHitStopTimeScale(float scale)
        {
            TimeScale?.RemoveBySource(CombatTimeClock.HitStopSourceId);
            TimeScale?.AddModifier(CombatTimeClock.HitStopSourceId, scale);
            RefreshAnimSpeed();
        }

        /// <summary>结束命中顿帧实体倍率。</summary>
        public void ClearHitStopTimeScale()
        {
            TimeScale?.RemoveBySource(CombatTimeClock.HitStopSourceId);
            RefreshAnimSpeed();
        }

        void RefreshAnimSpeed() => _anim?.Director?.RefreshSpeedFromOwner();

        #endregion

        #region 技能 / Action

        public T AttachAction<T>() where T : Entity, IActionAbility
        {
            var action = AddChild<T>();
            action.Enable = true;
            return action;
        }

        public void BindSkillInput(int skillId) => _abilityComponent?.AttachAbility(skillId);
        public void UnBindSkillInput(int skillId) => _abilityComponent?.RemoveAbility(skillId);

        #endregion

        #region 时间轴消息

        /// <summary>战斗规则（EGamePlay）→ 表现（Presenter）。</summary>
        public void HandleTimelineMessage(
            string msgName,
            float floatMsg,
            bool boolMsg,
            TagSource? source = null,
            string strMsg = null)
        {
            if (CombatTimelineRules.TryApply(this, msgName, floatMsg, source))
                return;

            _timelinePresenter?.ApplyPresentationMessage(msgName, floatMsg, boolMsg, strMsg, source);
        }

        public void HandleTimelineMessageFinish(string msgName, bool boolMsg, bool setOppositeOnFinish)
        {
            if (!setOppositeOnFinish || string.IsNullOrEmpty(msgName))
                return;

            _timelinePresenter?.ApplyPresentationMessage(msgName, 0f, !boolMsg);
        }

        #endregion

        #region 战斗落地（死亡 / 受击）

        public void ApplyDeath()
        {
            if (_curState == PlayerStateEnum.Dead)
                return;

            DazeMeter?.OnOwnerDeath();
            _stateDirector?.EnterDead();

            var runner = ActiveExecution;
            ActiveExecution = null;
            runner?.BreakSkill();

#if UNITY
            AnimComponent anim = GetComponent<AnimComponent>();
            anim?.Director?.ForceLocomotion();
            anim?.Motion?.SetPolicy(MotionPolicy.Locomotion);
            anim?.Motion?.SetSkillSuppressGravity(false);
            EndSkillMoveLock();
            SetTimedMoveLock(false);
            ChangeInputMoveState(false);
#endif
            Status?.RemoveAll(BuffRemoveReason.Death);
            GetComponent<PassiveSkillBuffComponent>()?.NotifyOwnerDeath();
            if (IsPlayerSquad)
                CombatSquad.Instance?.NotifyMemberDeath(this);
        }

        /// <summary>重受击硬直 + 打断技能 + 受击动画。轻段不要调；已死亡/硬控中返回 false。霸体走抗打断比大小，这里不再问 UnStopped。</summary>
        public bool TryApplyHitReaction(long sourceId, float durationSeconds = 0.35f)
        {
            if (!CanEnterHitReaction())
                return false;

            bool exiting = IsPlayerSquad && SquadPresence == SquadPresence.Exiting;
            BreakActiveSkill();
            if (exiting)
                return false;

            _stateDirector?.EnterHit(sourceId, durationSeconds);

#if UNITY
            GetComponent<AnimComponent>()?.Director?.PlayDamageReaction();
#endif
            return true;
        }

        /// <summary>
        /// 招架硬直：断轴后播 Stun 并按时长交回，避免短 Damage 自动回 Idle。
        /// 计时走宿主层钟（敌人=世界钟）。
        /// </summary>
        public bool TryApplyParryStun(long sourceId, float durationSeconds)
        {
            if (!CanEnterHitReaction())
                return false;

            float stun = durationSeconds > 0.01f ? durationSeconds : 0.55f;
            BreakActiveSkill();
            _stateDirector?.EnterHit(sourceId, stun);

#if UNITY
            GetComponent<AnimComponent>()?.Director?.PlayHeldControlReaction(0.05f, stun);
#endif
            return true;
        }

        bool CanEnterHitReaction()
        {
            if (_curState == PlayerStateEnum.Dead || _curState == PlayerStateEnum.Control
                || _curState == PlayerStateEnum.Stagger)
                return false;
            return TagHost == null || !TagHost.HasIndex(TagHost.MoveForbidIndex);
        }

        void BreakActiveSkill()
        {
            var runner = ActiveExecution;
            if (runner == null)
                return;
            ActiveExecution = null;
            runner.BreakSkill();
        }

        /// <summary>MoveForbid 0→1 断招进控制槽；1→0 退出。多层眩晕靠 Tag 计数。冻结仍在时不退出。</summary>
        public void NotifyHardControlChanged(bool entered)
        {
            if (entered)
                EnterHardControl(playHeldReaction: TagHost == null || !TagHost.HasIndex(TagHost.FreezeIndex));
            else if (TagHost == null || !TagHost.HasIndex(TagHost.FreezeIndex))
                ExitHardControl();
        }

        /// <summary>冻结：实体钟归零 + 冰壳；无 MoveForbid 时也进控制槽但不播眩晕动作。</summary>
        public void NotifyFreezeChanged(bool entered)
        {
            if (entered)
            {
                ApplyFreezeTimeScale();
                ApplyFreezeVisual(true);
                if (_stateDirector == null || !_stateDirector.IsControl)
                    EnterHardControl(playHeldReaction: false);
            }
            else
            {
                ClearFreezeTimeScale();
                ApplyFreezeVisual(false);
                if (TagHost == null || !TagHost.HasIndex(TagHost.MoveForbidIndex))
                    ExitHardControl();
            }
        }

        /// <summary>冻结：实体 TimeScale=0（替换本源）。</summary>
        public void ApplyFreezeTimeScale()
        {
            TimeScale?.RemoveBySource(CombatTimeClock.FreezeSourceId);
            TimeScale?.AddModifier(CombatTimeClock.FreezeSourceId, 0f);
            RefreshAnimSpeed();
        }

        /// <summary>解除冻结实体钟。</summary>
        public void ClearFreezeTimeScale()
        {
            TimeScale?.RemoveBySource(CombatTimeClock.FreezeSourceId);
            RefreshAnimSpeed();
        }

        void ApplyFreezeVisual(bool frozen)
        {
#if UNITY
            AttackPlayer?.GetComponent<CharacterRenderFX>()?.SetFreeze(frozen ? 1f : 0f);
#endif
        }

        void EnterHardControl(bool playHeldReaction)
        {
            if (IsDead)
                return;

            _stateDirector?.EnterControl();

            var runner = ActiveExecution;
            if (runner != null)
            {
                ActiveExecution = null;
                runner.BreakSkill();
            }

            EndSkillMoveLock();
            ChangeInputMoveState(false);

#if UNITY
            if (playHeldReaction)
            {
                AnimComponent anim = GetComponent<AnimComponent>();
                anim?.Director?.PlayHeldControlReaction();
            }
#endif
        }

        void ExitHardControl()
        {
            if (IsDead)
                return;

            _stateDirector?.ExitControl();
            // 人机不要跟主控共用键盘；有 EnemyBrain 时仍开电机，停步靠 MoveWeight。
            ChangeInputMoveState(ShouldEnableMotor());

            if (_stateDirector != null && _stateDirector.IsStagger)
            {
#if UNITY
                GetComponent<AnimComponent>()?.Director?.PlayHeldControlReaction();
#endif
                return;
            }

#if UNITY
            AnimComponent anim = GetComponent<AnimComponent>();
            anim?.Director?.ForceLocomotion();
            anim?.Motion?.SetPolicy(MotionPolicy.Locomotion);
            anim?.Motion?.SetSkillSuppressGravity(false);
#endif
        }

        /// <summary>失衡条满：无条件断招进 Stagger，导演 Punish，播破衡包。表现不进 DamageAction。</summary>
        public void BeginDazeStagger()
        {
            if (IsDead)
                return;

            var runner = ActiveExecution;
            if (runner != null)
            {
                ActiveExecution = null;
                runner.BreakSkill();
            }

            _stateDirector?.EnterStagger();
            EndSkillMoveLock();
            ChangeInputMoveState(false);
            Ai.CombatEncounterDirector.Instance?.NotifyBreakMeterOpened(this);

#if UNITY
            AnimComponent anim = GetComponent<AnimComponent>();
            anim?.Director?.PlayHeldControlReaction();
            var fx = CombatFxPlayContext.ForOwner(this, CombatFxSource.Entity(Id));
            fx.ActionTarget = this;
            fx.ActionCreator = Ai.CombatEncounterDirector.Instance != null
                ? Ai.CombatEncounterDirector.Instance.FocusTarget
                : null;
            CombatFxPackagePlayer.Play(CombatFxPackageId.StaggerBreak, in fx);
#endif
        }

        /// <summary>失衡条掉光：退 Stagger，导演结束 Punish（场上无人 Opened 时）。冻结仍在则保持控制姿态。</summary>
        public void EndDazeStagger()
        {
            if (IsDead)
                return;

            _stateDirector?.ExitStagger();
            Ai.CombatEncounterDirector.Instance?.NotifyBreakMeterClosed(this);

            if (_stateDirector != null && _stateDirector.IsControl)
                return;

            ChangeInputMoveState(ShouldEnableMotor());

#if UNITY
            AnimComponent anim = GetComponent<AnimComponent>();
            anim?.Director?.ForceLocomotion();
            anim?.Motion?.SetPolicy(MotionPolicy.Locomotion);
            anim?.Motion?.SetSkillSuppressGravity(false);
#endif
        }

        bool ShouldEnableMotor()
        {
            if (IsDead || IsBench)
                return false;
            if (GetComponent<Ai.EnemyBrainComponent>() != null)
                return true;
            return isTruePlayer || SquadPresence == SquadPresence.Exiting;
        }

        #endregion
    }
}
