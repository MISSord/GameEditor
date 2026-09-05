using System.Collections.Generic;
using UnityEngine;

namespace EGamePlay.Combat
{
    /// <summary>
    /// 战局上下文
    /// </summary>
    public class CombatContext : Entity
    {
        public static CombatContext Instance { get; private set; }
#if !SERVER
        public Dictionary<GameObject, ICombatUnit> Object2Entities { get; set; } = new Dictionary<GameObject, ICombatUnit>();

        /// <summary>从碰撞体 GameObject 向上查找已注册战斗单位（子 Collider 也能命中）。</summary>
        public bool TryResolveCombatUnit(GameObject colliderObject, out ICombatUnit unit)
        {
            unit = null;
            if (colliderObject == null)
                return false;

            Transform current = colliderObject.transform;
            while (current != null)
            {
                if (Object2Entities.TryGetValue(current.gameObject, out unit) && unit != null && !unit.IsDisposed)
                    return true;
                current = current.parent;
            }

            unit = null;
            return false;
        }
#endif

        /// <summary>
        /// true：入队不扣 CD，时间轴启动后才转；Evaluate 检查 CD/资源。
        /// false：Idle 入队仍立刻扣 CD（对照旧路径）。
        /// </summary>
        public bool UseAbilityGate { get; set; } = true;

        /// <summary>命中申报队列，由盒体入队、Context Flush。</summary>
        public HitPipeline HitPipeline { get; private set; }

        private List<Entity> _spellActions = new List<Entity>(4);
        private List<Entity> _combatEntities = new List<Entity>(4);

        public override void Awake()
        {
            base.Awake();
            Instance = this;
            HitPipeline = new HitPipeline();
        }

        public override void OnDestroy()
        {
            HitPipeline?.Clear();
            HitPipeline = null;
            base.OnDestroy();
        }

        public Entity AddAction<T>() where T : Entity
        {
            Entity action = AddChild<T>();
            _spellActions.Add(action);
            return action;
        }

        /// <summary>创建战斗单位。具体类型由 ACT 层提供（如 CombatEntity）。</summary>
        public T AddCombatUnit<T>(object initData) where T : Entity, ICombatUnit
        {
            T combat = AddChild<T>(initData);
            _combatEntities.Add(combat);
            return combat;
        }

        /// <summary>卸所有单位上绑在该技能轴的 Buff（含打到敌人身上的）。</summary>
        public void RemoveBuffsBoundToRunner(long runnerId)
        {
            if (runnerId == 0)
                return;
            for (int i = 0; i < _combatEntities.Count; i++)
            {
                Entity entity = _combatEntities[i];
                if (entity == null || entity.IsDisposed)
                    continue;
                if (entity is ICombatUnit unit)
                    unit.Status?.RemoveBoundToRunner(runnerId);
            }
        }

        public override void Update(float deltaTime)
        {
            Entity action;
            for (int i = _spellActions.Count - 1; i >= 0; i--)
            {
                action = _spellActions[i];
                if (action.IsDisposed == true)
                {
                    _spellActions.RemoveAt(i);
                    continue;
                }
                float actionDelta = deltaTime;
                if (action is ICombatSpellActionContext spellCtx)
                    actionDelta = CombatTimeClock.GetDelta(spellCtx.Caster);
                action.Update(actionDelta);
            }

            HitPipeline?.Flush();

            for (int i = _combatEntities.Count - 1; i >= 0; i--)
            {
                action = _combatEntities[i];
                if (action.IsDisposed == true)
                {
                    _combatEntities.RemoveAt(i);
                    continue;
                }
                if (action.IsNeedUpdate == true && action is ICombatUnit unit)
                    action.Update(CombatTimeClock.GetDelta(unit));
            }
        }

        public override void FixedUpdate(float fixDeltaTime)
        {
            HitPipeline?.Flush();

            Entity entity;
            for (int i = _combatEntities.Count - 1; i >= 0; i--)
            {
                entity = _combatEntities[i];
                if (entity.IsDisposed == true)
                {
                    _combatEntities.RemoveAt(i);
                    continue;
                }
                if (entity.IsNeedFixUpdate == true && entity is ICombatUnit unit)
                    entity.FixedUpdate(CombatTimeClock.GetFixedDelta(unit));
            }
        }
    }
}
