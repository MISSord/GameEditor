using ACTGameEditor;
using EGamePlay.Unity;
using EGamePlay.Unity.Locomotion;
using System;
using UnityEngine;

namespace EGamePlay.Combat
{
    public class EntityDeadEvent { public Entity DeadEntity; }

    /// <summary>
    /// 最小战斗实体单位，目前暂无子弹这样更小的单位（或者说子弹走CombatEntity（虽然功能上是冗余的））
    /// </summary>
    public sealed class CombatEntity : Entity, IPosition
    {
        #region 基础标识与 Transform

        public uint NetId { get; private set; }
        // 是否为本地玩家实体
        public bool isTruePlayer = false;
        public Transform ModelTrans { get; set; }
        public Transform RootTransform { get; set; }
        public VitalComponent CurrentVital { get; private set; }
        public ActPlayer AttackPlayer { get; set; }
        //当前运行中的执行体
        public ActSkillRunner SpellingExecution { get; set; }

        //施法行动能力
        public SpellActionAbility SpellAbility { get; private set; }
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
        internal void SetCurStateFromDirector(PlayerStateEnum state) => _curState = state;

        /// <summary>行为态裁决。</summary>
        public CombatStateDirector StateDirector => _stateDirector;

        // 当前移动状态
        public MoveTypeEnum CurMoveState { get; set; } = MoveTypeEnum.Idle;
        // 状态机，影响决策
        public GameStateMachine stateMachine { get; private set; }

        #region 部分属性快捷获取

        private int _attackDamageForbidTagIndex;
        private int _moveForbidTagIndex;
        private int _skillForbidTagIndex;
        private int _unStoppedTagIndex;
        public bool IsCanCauseHarm => !_tagContainer.HasTag(_attackDamageForbidTagIndex);
        /// <summary>
        /// 是否允许 Locomotion 读入并位移。
        /// 技能轴未真正结束（含 ParentFinish 后摇）期间默认锁移动；
        /// Buff.MoveForbid 额外禁止；仅 Idle/Moving 可走。
        /// </summary>
        public bool IsCanMove => !_tagContainer.HasTag(_moveForbidTagIndex)
            && SpellingExecution == null
            && (CurState == PlayerStateEnum.Idle || CurState == PlayerStateEnum.Moving);

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
            && !_tagContainer.HasTag(_unStoppedTagIndex)
            && !_tagContainer.HasTag(_skillForbidTagIndex)
            && CurState != PlayerStateEnum.Hit;

        /// <summary>
        /// 高优先级自身取消（闪避/大招顶普攻）。
        /// 霸体 UnStopped 不挡自己取消；受击、死亡、禁技能仍挡。
        /// </summary>
        public bool IsCanSelfCancelSkill => !IsDead
            && !_tagContainer.HasTag(_skillForbidTagIndex)
            && CurState != PlayerStateEnum.Hit;

        private GameplayTagContainer _tagContainer;
        private ActionPointComponent _actionPointComponent;
        private AbilityComponent _abilityComponent;
        private CombatFormComponent _formComponent;
        private EntityTimeScaleComponent _timeScaleComponent;

        struct TimedTagRelease
        {
            public TagSource Source;
            public string TagName;
            public float EndTime;
        }

        readonly System.Collections.Generic.List<TimedTagRelease> _timedTagReleases =
            new System.Collections.Generic.List<TimedTagRelease>(4);

        /// <summary>霸体：带 Buff.UnStopped 时不进受击硬直、不打断技能。</summary>
        public bool IsUnstopped => _tagContainer != null && _tagContainer.HasTag(_unStoppedTagIndex);

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

            stateMachine = new GameStateMachine();
            stateMachine.AddState<NormalState>(GamePlayerState.NormalState);
            stateMachine.AddState<CombatState>(GamePlayerState.CombatState);

            //这里未来记得调整与拓展 加入PlayerManager
            NetId = PlayerManager.GetID(isTruePlayer);

            _tagContainer = AddComponent<StatusComponent>().TagContainer;

            _stateDirector = new CombatStateDirector();
            _stateDirector.Bind(this);

            _attackDamageForbidTagIndex = TagCollection.TagToIndexDic[CombatTags.BuffAttackDamageForbid];
            _moveForbidTagIndex = TagCollection.TagToIndexDic[CombatTags.BuffMoveForbid];
            _skillForbidTagIndex = TagCollection.TagToIndexDic[CombatTags.BuffSkillForbid];
            _unStoppedTagIndex = TagCollection.TagToIndexDic[CombatTags.BuffUnStopped];

            AddComponent<AttributeComponent>().InitializeCharacter(data.CharacterId, data.Level);
            _timeScaleComponent = AddComponent<EntityTimeScaleComponent>();
            _actionPointComponent = AddComponent<ActionPointComponent>();
            _abilityComponent = AddComponent<AbilityComponent>();
            AddComponent<SpellComponent>();
            _formComponent = AddComponent<CombatFormComponent>();

            CurrentVital = AddComponent<VitalComponent>();
            CurrentVital.InitVital();

#if UNITY
            //AddComponent<PlayableGraphComponent>(initData);
            AddComponent<AnimComponent>(initData);
            //输入移动组件
            _inputMove = AddComponent<InputMoveComponent>(initData);
#endif
            //能力
            SpellAbility = AttachAction<SpellActionAbility>();
            //MotionAbility = AttachAction<MotionActionAbility>();
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
            _tagContainer = null;
            _timedTagReleases.Clear();
            stateMachine = null;
            CurrentVital = null;
            _formComponent = null;
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

        #region 行动点事件
        public void ListenActionPoint(ActionPointType actionPointType, Action<Entity> action)
        {
            _actionPointComponent?.AddListener(actionPointType, action);
        }

        public void UnListenActionPoint(ActionPointType actionPointType, Action<Entity> action)
        {
            _actionPointComponent?.RemoveListener(actionPointType, action);
        }

        public void TriggerActionPoint(ActionPointType actionPointType, Entity action)
        {
            _actionPointComponent?.TriggerActionPoint(actionPointType, action);
        }
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

        //标签操作
        public bool HasTag(string tagName)
        {
            return _tagContainer.HasTag(tagName);
        }

        /// <summary>按 RequiredTags/BlockedTags 判断是否可释放技能，供 XCSkillInputEvent 等使用。</summary>
        public bool CanSpellSkillWithTagLists(System.Collections.Generic.List<string> required, System.Collections.Generic.List<string> blocked)
        {
            return _tagContainer != null && _tagContainer.CanSpellSkillWithTagLists(required, blocked);
        }

        /// <summary>兼容旧调用：Manual 源 Push。</summary>
        public void AddTag(string tagName)
        {
            _tagContainer?.Push(TagSource.Manual(), tagName);
        }

        /// <summary>兼容旧调用：无源 Remove（防负数）。</summary>
        public void RemoveTag(string tagName)
        {
            _tagContainer?.RemoveTag(tagName);
        }

        /// <summary>带来源压入 Tag。</summary>
        public void PushTag(TagSource source, string tagName)
        {
            _tagContainer?.Push(source, tagName);
        }

        /// <summary>配对弹出 Tag。</summary>
        public void PopTag(TagSource source, string tagName)
        {
            _tagContainer?.Pop(source, tagName);
        }

        /// <summary>移除某来源的全部 Tag（技能 Break / 切形态）。</summary>
        public void PopTagsFrom(TagSource source)
        {
            _tagContainer?.PopAll(source);
            for (int i = _timedTagReleases.Count - 1; i >= 0; i--)
            {
                if (_timedTagReleases[i].Source.Equals(source))
                    _timedTagReleases.RemoveAt(i);
            }
        }

        /// <summary>按秒授予 Tag，到期自动 Pop 一条同 Source 授予。</summary>
        public void GrantTagFor(TagSource source, string tagName, float durationSeconds)
        {
            if (_tagContainer == null || string.IsNullOrEmpty(tagName))
                return;
            PushTag(source, tagName);
            if (durationSeconds <= 0f)
                return;
            float end = GameTimeManager.PlayerTime + durationSeconds;
            _timedTagReleases.Add(new TimedTagRelease
            {
                Source = source,
                TagName = tagName,
                EndTime = end,
            });
        }

        /// <summary>技能时间轴：短时霸体。</summary>
        public void GrantUnstoppedFor(float durationSeconds, TagSource source)
        {
            GrantTagFor(source, CombatTags.BuffUnStopped, durationSeconds);
        }

        /// <summary>
        /// 时间轴消息本地落地（与 PlayerManager 并行；单机编辑器可直接生效）。
        /// </summary>
        public void HandleTimelineMessage(string msgName, float floatMsg, bool boolMsg, TagSource? source = null)
        {
            if (string.IsNullOrEmpty(msgName))
                return;

            TagSource src = source ?? TagSource.Manual();

            if (msgName == PlayEventMsg.SetNoBreakTime)
            {
                // 按秒计时，不跟技能轴 PopAll 绑定
                GrantUnstoppedFor(floatMsg, TagSource.Manual());
                return;
            }

            if (msgName == PlayEventMsg.SetNoGravityT)
            {
#if UNITY
                GetComponent<AnimComponent>()?.Motion?.SuppressGravityFor(floatMsg);
#endif
                return;
            }

            if (msgName == PlayEventMsg.SetCanMove)
            {
                ChangeInputMoveState(boolMsg);
            }
        }

        /// <summary>HP 归零后的统一死亡落地。</summary>
        public void ApplyDeath()
        {
            if (_curState == PlayerStateEnum.Dead)
                return;

            _stateDirector?.EnterDead();

            var runner = SpellingExecution;
            SpellingExecution = null;
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

            var runner = SpellingExecution;
            if (runner != null)
            {
                SpellingExecution = null;
                runner.BreakSkill();
            }

#if UNITY
            GetComponent<AnimComponent>()?.Director?.PlayDamageReaction();
#endif
            return true;
        }

        void TickTimedTags(float playerTime)
        {
            for (int i = _timedTagReleases.Count - 1; i >= 0; i--)
            {
                if (playerTime < _timedTagReleases[i].EndTime)
                    continue;
                TimedTagRelease entry = _timedTagReleases[i];
                _timedTagReleases.RemoveAt(i);
                PopTag(entry.Source, entry.TagName);
            }
        }

        /// <summary>当前实体时间流速乘数（不含世界 scale）。1 表示正常流速。</summary>
        public float GetTimeScale()
        {
            return _timeScaleComponent != null ? _timeScaleComponent.EntityScale : 1f;
        }

        /// <summary>添加时间流速 modifier，可叠加。如 Buff 减速 50% 则 AddTimeScaleModifier(buffId, 0.5f)。</summary>
        public void AddTimeScaleModifier(int sourceId, float scale)
        {
            _timeScaleComponent?.AddModifier(sourceId, scale);
        }

        /// <summary>移除指定来源的所有时间流速 modifier。</summary>
        public void RemoveTimeScaleModifierBySource(int sourceId)
        {
            _timeScaleComponent?.RemoveBySource(sourceId);
        }

        //更新部分
        public override void Update(float deltaTime)
        {
            //同步最新坐标
            Position = RootTransform.position;
            Rotation = RootTransform.rotation;

            _stateDirector?.Tick(GameTimeManager.PlayerTime);
            TickTimedTags(GameTimeManager.PlayerTime);

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

    public class RemoveStatusEvent
    {
        public Entity Entity { get; set; }
        public Buff buff { get; set; }
        public long BuffId { get; set; }
    }

    public class AddStatusEvent
    {
        public Entity Entity { get; set; }
        public Buff buff { get; set; }
        public long BuffId { get; set; }
    }
}