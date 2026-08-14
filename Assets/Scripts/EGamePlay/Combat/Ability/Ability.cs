using UnityEngine;
using ACTGameEditor;
#if EGAMEPLAY_ET
using SkillConfig = cfg.Skill.SkillCfg;
using AO;
#endif

namespace EGamePlay.Combat
{
    /// <summary>
    /// 能力统一接口。Ability（技能）与 Buff 均为此能力的具象，具备挂载实体与激活/反激活生命周期。
    /// </summary>
    public interface IAbility
    {
        CombatEntity OwnerEntity { get; }
        bool Enable { get; }
        void Activate();
        void Deactivate();
    }

    /// <summary>
    /// 能力实体基类，统一 Ability 与 Buff 的共有结构：归属实体、启用状态、激活/反激活生命周期。
    /// 子类通过重写 OnActivate/OnDeactivate 实现各自逻辑（如技能启用效果与触发器、Buff 加标签等）。
    /// </summary>
    public abstract class AbilityBase : Entity, IAbility
    {
        public CombatEntity OwnerEntity => GetParent<CombatEntity>();
        public bool Enable { get; protected set; }

        public virtual void Activate()
        {
            if (Enable) return;
            OnActivate();
            Enable = true;
        }

        public virtual void Deactivate()
        {
            if (!Enable) return;
            OnDeactivate();
            Enable = false;
        }

        /// <summary>激活时调用，子类在此启用组件或添加标签等。</summary>
        protected virtual void OnActivate()
        {
            foreach (var kv in Components)
                kv.Value.Enable = true;
        }

        /// <summary>反激活时调用，子类在此禁用组件或移除标签等。</summary>
        protected virtual void OnDeactivate()
        {
            foreach (var kv in Components)
                kv.Value.Enable = false;
        }
    }


    /// <summary>
    /// 主动技能，继承 AbilityBase 与 Buff 共享统一生命周期。
    /// 配置数据从 AbilityDefinitionManager 获取，同一技能只加载一次。
    /// </summary>
    public class Ability : AbilityBase
    {
        /// <summary>共享定义，同一 skillId 全局复用。</summary>
        public AbilityDefinition Definition { get; private set; }
        public SkillAllEventData SkillData => Definition?.SkillData;
        public int SkillID => Definition?.SkillID ?? 0;

        public override void Awake(object initData)
        {
            base.Awake(initData);
            int skillId = (int)initData;
            Definition = AbilityDefinitionManager.Instance.GetOrLoad(skillId);
            if (Definition == null)
            {
                GameLog.CombatError($"[Ability] 加载技能定义失败 skillId={skillId}");
                return;
            }

            Name = Definition.Name;
            // 技能触发器仍然可以基于 TriggerConfig 工作，此处不再挂载 AbilityEffectComponent，
            // 效果数据已提升到 AbilityDefinition.EffectDatas 上统一管理。
            //AddComponent<AbilityTriggerComponent>(Definition.ConfigObject.TriggerActions);
        }

        /// <summary>尝试激活能力（挂载时调用）。</summary>
        public void TryActivateAbility()
        {
            Activate();
        }

        /// <summary>结束能力并销毁实体。</summary>
        public void EndAbility()
        {
            Deactivate();
            Entity.Destroy(this);
        }
    }

}