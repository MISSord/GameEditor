using EGamePlay;
using EGamePlay.Combat;
using EGamePlay.Unity;
using EGamePlay.Unity.Locomotion;
using System;
using UnityEngine;

namespace ACTGameEditor.Combat
{
    /// <summary>
    /// 最小战斗实体单位，目前暂无子弹这样更小的单位（或者说子弹走CombatEntity（虽然功能上是冗余的））
    /// </summary>
    public sealed class CombatEntity : Entity, IPosition, ICombatUnit
    {
        Entity ICombatUnit.Entity => this;
        long ICombatUnit.Id => Id;
        bool ICombatUnit.isTruePlayer => isTruePlayer;
        #region 基础标识与 Transform

        public uint NetId { get; private set; }
        // 是否为本地玩家实体
        public bool isTruePlayer = false;
        public Transform ModelTrans { get; set; }
        public Transform RootTransform { get; set; }
        public VitalComponent CurrentVital { get; private set; }
        public ActPlayer AttackPlayer { get; set; }

        /// <summary>当前技能占轴（Combat 层接口）。</summary>
        public ISkillExecutionHandle ActiveExecution { get; set; }

        /// <summary>ACT 层强类型访问。</summary>
        public ActSkillRunner SpellingExecution
        {
            get => ActiveExecution as ActSkillRunner;
            set => ActiveExecution = value;
        }

        //伤害行动能力
        public DamageActionAbility DamageAbility { get; private set; }
        //资源变动行动能力（治疗/资源回复等）
        public ResourceActionAbility ResourceAbility { get; private set; }
        //施加状态行动能力
        public AddStatusActionAbility AddStatusAbility { get; private set; }

        public Vector3 Position { get; set; }
        public Quaternion Rotation { get; set; }

        #endregion

        // 阵营信息
        public AgentTag CurAgent { get; set; }

        CombatStateDirector _stateDirector;
        PlayerStateEnum _curState = PlayerStateEnum.Idle;

        /// <summary>行为态（只应由 CombatStateDirector 写入）。</summary>
        public PlayerStateEnum CurState => _curState;

        /// <summary>供 StateDirector 同步可见态。</summary>
        public void ApplyStateFromDirector(PlayerStateEnum state) => _curState = state;

        /// <summary>行为态裁决。</summary>
        public CombatStateDirector StateDirector => _stateDirector;

        // 当前移动状态
        public MoveTypeEnum CurMoveState { get; set; } = MoveTypeEnum.Idle;

        /// <summary>Tag 读写与定时释放。</summary>
        public CombatTagComponent TagHost { get; private set; }

        /// <summary>行动点事件。</summary>
        public ActionPointComponent ActionPoints { get; private set; }

        /// <summary>实体时间流速 modifier。</summary>
        public EntityTimeScaleComponent TimeScale { get; private set; }

        #region 部分属性快捷获取

        public bool IsCanCauseHarm => !TagHost.HasIndex(TagHost.AttackDamageForbidIndex);
        /// <summary>
        /// 是否允许 Locomotion 读入并位移。
        /// 技能轴未真正结束（含 ParentFinish 后摇）期间默认锁移动；
        /// Buff.MoveForbid 额外禁止；仅 Idle/Moving 可走。
        /// </summary>
        public bool IsCanMove => !TagHost.HasIndex(TagHost.MoveForbidIndex)
            && ActiveExecution == null
            && (CurState == PlayerStateEnum.Idle || CurState == PlayerStateEnum.Moving);

        /// <summary>
        /// 是否允许 Locomotion 起跳（一段跳：须落地，由 Motor 再判）。
        /// 技能占轴、受击、死亡、禁移时不可跳。
        /// </summary>
        public bool IsCanJump
        {
            get
            {
                if (IsDead || ActiveExecution != null || CurState == PlayerStateEnum.Hit)
                    return false;
                if (TagHost.HasIndex(TagHost.MoveForbidIndex))
                    return false;
                if (!IsGrounded)
                    return false;
#if UNITY
                if (_inputMove == null || !_inputMove.Enable)
                    return false;
#endif
                return true;
            }
        }

        /// <summary>空中。无移动组件时视为地面。</summary>
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

        /// <summary>HP 归零或已进入死亡态。伤害结算后 CurState 可能尚未切到 Dead，以 Vital 为准。</summary>
        public bool IsDead => _stateDirector != null && _stateDirector.IsDead
            || (CurrentVital != null && CurrentVital.CheckDead());

        public bool IsCanSpellSkill => !IsDead
            && !TagHost.HasIndex(TagHost.UnStoppedIndex)
            && !TagHost.HasIndex(TagHost.SkillForbidIndex)
            && CurState != PlayerStateEnum.Hit;

        /// <summary>
        /// 高优先级自身取消（闪避/大招顶普攻）。
        /// 霸体 UnStopped 不挡自己取消；受击、死亡、禁技能仍挡。
        /// </summary>
        public bool IsCanSelfCancelSkill => !IsDead
            && !TagHost.HasIndex(TagHost.SkillForbidIndex)
            && CurState != PlayerStateEnum.Hit;

        /// <summary>霸体：带 Buff.UnStopped 时不进受击硬直、不打断技能。</summary>
        public bool IsUnstopped => TagHost != null && TagHost.HasIndex(TagHost.UnStoppedIndex);

        private AbilityComponent _abilityComponent;
        private CombatFormComponent _formComponent;
        ICombatTimelinePresenter _timelinePresenter;

#if UNITY
        private InputMoveComponent _inputMove;
        public bool IsGrounded => _inputMove.IsGrounded;

        public bool useAnimaRoot = false;
#endif

        #endregion

        public override void Awake(object initData)
        {
            var data = (GameObjectData)initData;
            isTruePlayer = data.agent == AgentTag.PlayerA;

            //这里未来记得调整与拓展 加入PlayerManager
            NetId = PlayerManager.GetID(isTruePlayer);

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
            _timelinePresenter = AddComponent<CombatTimelinePresenter>();

            CurrentVital = AddComponent<VitalComponent>();
            CurrentVital.InitVital();

#if UNITY
            //AddComponent<PlayableGraphComponent>(initData);
            AddComponent<AnimComponent>(initData);
            //输入移动组件
            _inputMove = AddComponent<InputMoveComponent>(initData);
#endif
            //能力
            DamageAbility = AttachAction<DamageActionAbility>();
            ResourceAbility = AttachAction<ResourceActionAbility>();
            AddStatusAbility = AttachAction<AddStatusActionAbility>();

#if UNITY
            useAnimaRoot = false;
#endif
        }

        public override void OnDestroy()
        {
            _stateDirector?.Unbind();
            _stateDirector = null;
            TagHost = null;
            ActionPoints = null;
            TimeScale = null;
            CurrentVital = null;
            _formComponent = null;
            _timelinePresenter = null;
#if UNITY
            _inputMove = null;
            useAnimaRoot = false;
#endif
        }

        public void ChangeInputMoveState(bool state)
        {
#if UNITY
            _inputMove.Enable = state;
#endif
        }

        /// <summary>是否允许 Locomotion 自动转向。</summary>
        public void ChangeInputRotateState(bool state)
        {
#if UNITY
            _inputMove?.SetRotationEnabled(state);
#endif
        }

        #region 行动点事件（兼容转发）

        public void ListenActionPoint(ActionPointType actionPointType, Action<Entity> action) =>
            ActionPoints?.ListenActionPoint(actionPointType, action);

        public void UnListenActionPoint(ActionPointType actionPointType, Action<Entity> action) =>
            ActionPoints?.UnListenActionPoint(actionPointType, action);

        public void TriggerActionPoint(ActionPointType actionPointType, Entity action) =>
            ActionPoints?.TriggerActionPoint(actionPointType, action);

        #endregion

        public T AttachAction<T>() where T : Entity, IActionAbility
        {
            var action = AddChild<T>();
            action.Enable = true;
            return action;
        }

        /// <summary>
        /// 按技能 ID 绑定输入（确保该技能已挂载 Ability）。
        /// 如需绑定已存在的 Ability 实例，请在外部自行管理引用。
        /// </summary>
        public void BindSkillInput(int skillId)
        {
            _abilityComponent?.AttachAbility(skillId);
        }

        /// <summary>
        /// 解除按技能 ID 绑定的输入。
        /// </summary>
        public void UnBindSkillInput(int skillId)
        {
            _abilityComponent?.RemoveAbility(skillId);
        }

        /// <summary>当前战斗形态，可能为 null（未 Awake 完）。</summary>
        public CombatFormComponent FormComponent => _formComponent;

        #region Tag（兼容转发）

        public bool HasTag(string tagName) => TagHost.HasTag(tagName);

        public bool CanSpellSkillWithTagLists(System.Collections.Generic.List<string> required, System.Collections.Generic.List<string> blocked) =>
            TagHost.CanSpellSkillWithTagLists(required, blocked);

        public void AddTag(string tagName) => TagHost.AddTag(tagName);

        public void RemoveTag(string tagName) => TagHost.RemoveTag(tagName);

        public void PushTag(TagSource source, string tagName) => TagHost.PushTag(source, tagName);

        public void PopTag(TagSource source, string tagName) => TagHost.PopTag(source, tagName);

        public void PopTagsFrom(TagSource source) => TagHost.PopTagsFrom(source);

        public void GrantTagFor(TagSource source, string tagName, float durationSeconds) =>
            TagHost.GrantTagFor(source, tagName, durationSeconds);

        public void GrantUnstoppedFor(float durationSeconds, TagSource source) =>
            TagHost.GrantUnstoppedFor(durationSeconds, source);

        #endregion

        /// <summary>
        /// 时间轴消息：战斗规则（EGamePlay）+ 表现（ACT Presenter）。
        /// </summary>
        public void HandleTimelineMessage(string msgName, float floatMsg, bool boolMsg, TagSource? source = null, string strMsg = null)
        {
            if (CombatTimelineRules.TryApply(this, msgName, floatMsg, source))
                return;

            _timelinePresenter?.ApplyPresentationMessage(msgName, floatMsg, boolMsg, strMsg);
        }

        /// <summary>Bool 型消息 OnFinish 反转（如 SetCanMove / SetCanRotate）。</summary>
        public void HandleTimelineMessageFinish(string msgName, bool boolMsg, bool setOppositeOnFinish)
        {
            if (!setOppositeOnFinish || string.IsNullOrEmpty(msgName))
                return;

            _timelinePresenter?.ApplyPresentationMessage(msgName, 0f, !boolMsg);
        }

        /// <summary>HP 归零后的统一死亡落地。</summary>
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

        /// <summary>
        /// 受击硬直 + 打断技能 + 受击动画。霸体/已死亡时返回 false。
        /// </summary>
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

        #region 时间流速（兼容转发）

        public float GetTimeScale() => TimeScale != null ? TimeScale.GetTimeScale() : 1f;

        public void AddTimeScaleModifier(int sourceId, float scale) =>
            TimeScale?.AddTimeScaleModifier(sourceId, scale);

        public void RemoveTimeScaleModifierBySource(int sourceId) =>
            TimeScale?.RemoveTimeScaleModifierBySource(sourceId);

        #endregion

        //更新部分
        public override void Update(float deltaTime)
        {
            //同步最新坐标
            Position = RootTransform.position;
            Rotation = RootTransform.rotation;

            _stateDirector?.Tick(GameTimeManager.PlayerTime);

            //正序遍历，保证按照加入的顺序进行更新
            for(int i = 0; i < UpdateComponents.Count; i++)
            {
                UpdateComponents[i].Update(deltaTime);
            }
        }

        public override void FixedUpdate(float fixDeltaTime)
        {
            //正序遍历，保证按照加入的顺序进行更新
            for (int i = 0; i < FixedUpdateComponents.Count; i++)
            {
                FixedUpdateComponents[i].FixedUpdate(fixDeltaTime);
            }
        }
    }
}
