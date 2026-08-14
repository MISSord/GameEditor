using ACTGameEditor;
using System;
using UnityEngine;
using XiaoCao;
#if EGAMEPLAY_ET
using Unity.Mathematics;
using Vector3 = Unity.Mathematics.float3;
using Quaternion = Unity.Mathematics.quaternion;
#endif

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
        //执行中的执行体
        public ActSkillRunner SpellingExecution { get; set; }

        //施法行动能力
        public SpellActionAbility SpellAbility { get; private set; }
        ////移动行动能力
        //public MotionActionAbility MotionAbility { get; private set; }
        //伤害行动能力
        public DamageActionAbility DamageAbility { get; private set; }
        //资源变动行动能力（治疗/资源回复等）
        public ResourceActionAbility ResourceAbility { get; private set; }
        //施加状态行动能力
        public AddStatusActionAbility AddStatusAbility { get; private set; }
        //碰撞能力
        public CollisionActionAbility CollisionAbility { get; private set; }

        public Vector3 Position { get; set; }
        public Quaternion Rotation { get; set; }

        #endregion

        // 阵营信息
        public AgentTag CurAgent { get; set; }
        // 当前状态
        public PlayerStateEnum CurState { get; set; } = PlayerStateEnum.Idle;
        // 当前移动状态
        public MoveTypeEnum CurMoveState { get; set; } = MoveTypeEnum.Idle;
        // 状态机，影响决策
        public GameStateMachine stateMachine { get; private set; }

        #region 部分属性快捷获取

        private int _attackDamageForbidTagIndex;
        private int _moveForbidTagIndex;
        private int _skillForbidTagIndex;
        private int _unStoppedTagIndex;
        //private int _rollTagIndex;

        //public bool IsCanRollTag => !_tagContainer.HasTag(_rollTagIndex) && (CurState != PlayerStateEnum.Dead);
        public bool IsCanCauseHarm => !_tagContainer.HasTag(_attackDamageForbidTagIndex);
        public bool IsCanMove => !_tagContainer.HasTag(_moveForbidTagIndex) && (CurState == PlayerStateEnum.Idle || CurState == PlayerStateEnum.Moving);
        public bool IsCanSpellSkill => !_tagContainer.HasTag(_unStoppedTagIndex) && !_tagContainer.HasTag(_skillForbidTagIndex)
            && CurState != PlayerStateEnum.Hit && CurState != PlayerStateEnum.Dead;

        private GameplayTagContainer _tagContainer;
        private ActionPointComponent _actionPointComponent;
        private AbilityComponent _abilityComponent;
        private EntityTimeScaleComponent _timeScaleComponent;

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

            _attackDamageForbidTagIndex = TagCollection.TagToIndexDic[CombatTags.BuffAttackDamageForbid];
            _moveForbidTagIndex = TagCollection.TagToIndexDic[CombatTags.BuffMoveForbid];
            _skillForbidTagIndex = TagCollection.TagToIndexDic[CombatTags.BuffSkillForbid];
            _unStoppedTagIndex = TagCollection.TagToIndexDic[CombatTags.BuffUnStopped];
            //_rollTagIndex = TagCollection.TagToIndexDic[CombatTags.BuffRoll];

            AddComponent<AttributeComponent>().InitializeCharacter(data.CharacterId, data.Level);
            _timeScaleComponent = AddComponent<EntityTimeScaleComponent>();
            _actionPointComponent = AddComponent<ActionPointComponent>();
            _abilityComponent = AddComponent<AbilityComponent>();
            AddComponent<SpellComponent>();

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
            CollisionAbility = AttachAction<CollisionActionAbility>();

#if UNITY
            useAnimaRoot = false;
#endif
        }

        public override void OnDestroy()
        {
            _tagContainer = null;
            stateMachine = null;
            CurrentVital = null;
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

        public void AddTag(string tagName)
        {
            _tagContainer.AddTag(tagName);
        }

        public void RemoveTag(string tagName)
        {
            _tagContainer.RemoveTag(tagName);
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