using EGamePlay;

namespace EGamePlay.Combat
{
    public class ResourceActionAbility : Entity, IActionAbility
    {
        public ICombatUnit OwnerEntity => GetParent<Entity>() as ICombatUnit;
        public bool Enable { get; set; }

        public bool TryMakeAction(out ResourceAction action)
        {
            if (!Enable)
            {
                action = null;
                return false;
            }

            action = (ResourceAction)CombatContext.Instance.AddAction<ResourceAction>();
            action.Creator = OwnerEntity;
            return true;
        }
    }

    /// <summary>
    /// 资源变动行动（治疗/回蓝/扣能量等统一在此）
    /// </summary>
    public class ResourceAction : Entity, IActionExecute
    {
        public CureEffect CureEffect => TriggerContext.EffectConfig as CureEffect;
        /// <summary>治疗数值。</summary>
        public int CureValue { get; set; }
        /// <summary>行动实体。</summary>
        public ICombatUnit Creator { get; set; }
        /// <summary>目标对象。</summary>
        public ICombatUnit Target { get; set; }
        public TriggerContext TriggerContext { get; set; }

        public void FinishAction() => Entity.Destroy(this);

        void PreProcess()
        {
            var cureEff = CureEffect;
            CureValue = cureEff != null ? (int)cureEff.CureValueProperty : 0;
        }

        public void ApplyCure()
        {
            PreProcess();

            if (Target?.CurrentVital == null)
            {
                FinishAction();
                return;
            }

            Target.CurrentVital.ReceiveCure(this);
            PostProcess();
            FinishAction();
        }

        void PostProcess()
        {
            using (CombatBuffPipeline.Lock(Creator, Target))
            {
                CombatBuffPipeline.Notify(Creator, ActionPointType.PostGiveCure, this);
                CombatBuffPipeline.Notify(Target, ActionPointType.PostReceiveCure, this);
            }
        }
    }
}
