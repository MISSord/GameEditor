using System;
using System.Collections.Generic;
using ACTGameEditor;
using EGamePlay;
using EGamePlay.Combat;
using EGamePlay.Unity;
using EGamePlay.Unity.Locomotion;
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

        #endregion

        #region 标识 / Transform

        public uint NetId { get; private set; }
        public bool isTruePlayer;
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
        public EntityTimeScaleComponent TimeScale { get; private set; }
        public CombatFormComponent FormComponent => _formComponent;

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

        #region 门控（Tag + 状态 + 占轴）

        public bool IsCanCauseHarm => TagHost != null && !TagHost.HasIndex(TagHost.AttackDamageForbidIndex);

        /// <summary>Locomotion 可读入并位移。</summary>
        public bool IsCanMove => TagHost != null
            && !TagHost.HasIndex(TagHost.MoveForbidIndex)
            && ActiveExecution == null
            && (CurState == PlayerStateEnum.Idle || CurState == PlayerStateEnum.Moving);

        public bool IsCanJump
        {
            get
            {
                if (IsDead || ActiveExecution != null || CurState == PlayerStateEnum.Hit)
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
            && CurState != PlayerStateEnum.Hit;

        /// <summary>高优先级自身取消（闪避/大招顶普攻）。</summary>
        public bool IsCanSelfCancelSkill => !IsDead
            && TagHost != null
            && !TagHost.HasIndex(TagHost.SkillForbidIndex)
            && CurState != PlayerStateEnum.Hit;

        public bool IsUnstopped => TagHost != null && TagHost.HasIndex(TagHost.UnStoppedIndex);

        #endregion

        #region 私有字段

        AbilityComponent _abilityComponent;
        CombatFormComponent _formComponent;
        ICombatTimelinePresenter _timelinePresenter;

#if UNITY
        InputMoveComponent _inputMove;
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
            NetId = 0;
            CurMoveState = MoveTypeEnum.Idle;
            _curState = PlayerStateEnum.Idle;
            AttackPlayer = null;
            ModelTrans = null;
            RootTransform = null;
            Position = default;
            Rotation = default;
            ActiveExecution = null;
        }

        public override void Update(float deltaTime)
        {
            if (RootTransform != null)
            {
                Position = RootTransform.position;
                Rotation = RootTransform.rotation;
            }

            _stateDirector?.Tick(GameTimeManager.PlayerTime);

            for (int i = 0; i < UpdateComponents.Count; i++)
                UpdateComponents[i].Update(deltaTime);
        }

        public override void FixedUpdate(float fixDeltaTime)
        {
            for (int i = 0; i < FixedUpdateComponents.Count; i++)
                FixedUpdateComponents[i].FixedUpdate(fixDeltaTime);
        }

        void InitializeIdentity(GameObjectData data)
        {
            isTruePlayer = data.agent == AgentTag.PlayerA;
            NetId = PlayerManager.GetID(isTruePlayer);
        }

        void AddCoreComponents(GameObjectData data)
        {
            AddComponent<StatusComponent>();
            TagHost = AddComponent<CombatTagComponent>();

            _stateDirector = new CombatStateDirector();
            _stateDirector.Bind(this);

            AddComponent<AttributeComponent>().InitializeCharacter(data.CharacterId, data.Level);

            TimeScale = AddComponent<EntityTimeScaleComponent>();
            ActionPoints = AddComponent<ActionPointComponent>();
            _abilityComponent = AddComponent<AbilityComponent>();
            AddComponent<ActSpellComponent>();
            _formComponent = AddComponent<CombatFormComponent>();

            CurrentVital = AddComponent<VitalComponent>();
            CurrentVital.InitVital();
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
            ActionPoints = null;
            TimeScale = null;
            CurrentVital = null;
            _formComponent = null;
            _timelinePresenter = null;
            _abilityComponent = null;
            ActiveExecution = null;
            DamageAbility = null;
            ResourceAbility = null;
            AddStatusAbility = null;
#if UNITY
            _inputMove = null;
            useAnimaRoot = false;
#endif
        }

        #endregion

        #region 输入 / Locomotion

        public void ChangeInputMoveState(bool state)
        {
#if UNITY
            if (_inputMove != null)
                _inputMove.Enable = state;
#endif
        }

        public void ChangeInputRotateState(bool state)
        {
#if UNITY
            _inputMove?.SetRotationEnabled(state);
#endif
        }

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

        public void AddTag(string tagName) => TagHost?.AddTag(tagName);
        public void RemoveTag(string tagName) => TagHost?.RemoveTag(tagName);
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

        public void AddTimeScaleModifier(int sourceId, float scale) =>
            TimeScale?.AddTimeScaleModifier(sourceId, scale);

        public void RemoveTimeScaleModifierBySource(int sourceId) =>
            TimeScale?.RemoveTimeScaleModifierBySource(sourceId);

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

            _stateDirector?.EnterDead();

            var runner = ActiveExecution;
            ActiveExecution = null;
            runner?.BreakSkill();

#if UNITY
            AnimComponent anim = GetComponent<AnimComponent>();
            anim?.Director?.ForceLocomotion();
            anim?.Motion?.SetPolicy(MotionPolicy.Locomotion);
            anim?.Motion?.SetSkillSuppressGravity(false);
            ChangeInputMoveState(false);
#endif
        }

        /// <summary>受击硬直 + 打断技能 + 受击动画。霸体/已死亡时返回 false。</summary>
        public bool TryApplyHitReaction(long sourceId, float durationSeconds = 0.35f)
        {
            if (_curState == PlayerStateEnum.Dead || IsUnstopped)
                return false;

            _stateDirector?.EnterHit(sourceId, durationSeconds);

            var runner = ActiveExecution;
            if (runner != null)
            {
                ActiveExecution = null;
                runner.BreakSkill();
            }

#if UNITY
            GetComponent<AnimComponent>()?.Director?.PlayDamageReaction();
#endif
            return true;
        }

        #endregion
    }
}
