using System.Collections.Generic;
using UnityEngine;
using ACTGameEditor;

namespace EGamePlay.Combat
{
    /// <summary>
    /// 战局上下文
    /// </summary>
    public class CombatContext : Entity
    {
        public static CombatContext Instance { get; private set; }
#if !SERVER
        public Dictionary<GameObject, CombatEntity> Object2Entities { get; set; } = new Dictionary<GameObject, CombatEntity>();
#endif

        private List<Entity> _spellActions = new List<Entity>(4);
        private List<Entity> _combatEntities = new List<Entity>(4);

        public override void Awake()
        {
            base.Awake();
            Instance = this;
        }

        public Entity AddAction<T>() where T : Entity
        {
            Entity action = AddChild<T>();
            _spellActions.Add(action);
            return action;
        }

        public CombatEntity AddCombatEntity(object initData)
        {
            CombatEntity combat = AddChild<CombatEntity>(initData);
            _combatEntities.Add(combat);
            return combat;
        }

        public override void Update(float deltaTime)
        {
            Entity action;
            //先更新行动Action，因为有可能会有Buff加到CombatEntity上
            //技能部分也是，先跑，完成要释放的技能的添加，后SpellComponent遍历释放最高优先级的技能
            for (int i = _spellActions.Count - 1; i >= 0; i--)
            {
                action = _spellActions[i];
                if (action.IsDisposed == true)
                {
                    _spellActions.RemoveAt(i);
                    continue;
                }
                float actionDelta = deltaTime;
                if (action is SpellAction spellAction && spellAction.Creator != null)
                    actionDelta *= spellAction.Creator.GetTimeScale();
                action.Update(actionDelta);
            }

            //后更新CombatEntity，使用实体专属时间流速
            for (int i = _combatEntities.Count - 1; i >= 0; i--)
            {
                action = _combatEntities[i];
                if (action.IsDisposed == true)
                {
                    _combatEntities.RemoveAt(i);
                    continue;
                }
                if (action.IsNeedUpdate == true)
                {
                    float entityDelta = deltaTime * (action as CombatEntity).GetTimeScale();
                    action.Update(entityDelta);
                }
            }
        }

        public override void FixedUpdate(float fixDeltaTime)
        {
            Entity entity;
            for (int i = _combatEntities.Count - 1; i >= 0; i--)
            {
                entity = _combatEntities[i];
                if (entity.IsDisposed == true)
                {
                    _combatEntities.RemoveAt(i);
                    continue;
                }
                if (entity.IsNeedFixUpdate == true)
                {
                    float entityFixedDelta = fixDeltaTime * (entity as CombatEntity).GetTimeScale();
                    entity.FixedUpdate(entityFixedDelta);
                }
            }
        }
    }
}