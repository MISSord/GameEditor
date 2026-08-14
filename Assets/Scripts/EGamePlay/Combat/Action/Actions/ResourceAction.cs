using EGamePlay;

namespace EGamePlay.Combat
{
    public class ResourceActionAbility : Entity, IActionAbility
    {
        public CombatEntity OwnerEntity { get { return GetParent<CombatEntity>(); } set { } }
        public bool Enable { get; set; }

        public bool TryMakeAction(out ResourceAction action)
        {
            if (Enable == false)
            {
                action = null;
            }
            else
            {
                action = (ResourceAction)CombatContext.Instance.AddAction<ResourceAction>();
                action.Creator = OwnerEntity;
            }
            return Enable;
        }
    }

    /// <summary>
    /// 资源变动行动（治疗/回蓝/扣能量等统一在此）
    /// </summary>
    public class ResourceAction : Entity, IActionExecute
    {
        public CureEffect CureEffect => TriggerContext.EffectConfig as CureEffect;
        /// 治疗数值
        public int CureValue { get; set; }
        /// 行动实体
        public CombatEntity Creator { get; set; }
        /// 目标对象
        public Entity Target { get; set; }
        public TriggerContext TriggerContext { get; set; }

        public void FinishAction()
        {
            Entity.Destroy(this);
        }

        //前置处理
        private void PreProcess()
        {
            //这里未来补上接受治疗前和给予治疗前的广播

            var cureEff = CureEffect;
            // 统一走配置/调用方传入的数值（可为正/负），不在这里做额外裁剪。
            CureValue = cureEff != null ? (int)cureEff.CureValueProperty : 0;
        }

        public void ApplyCure()
        {
            PreProcess();

            var healthComp = Target.GetComponent<VitalComponent>();
            healthComp.ReceiveCure(this);

            PostProcess();

            FinishAction();
        }

        //后置处理
        private void PostProcess()
        {
            Creator.TriggerActionPoint(ActionPointType.PostGiveCure, this);
            if (Target.GetType() == typeof(CombatEntity))
            {
                CombatEntity target = (CombatEntity)Target;
                target.TriggerActionPoint(ActionPointType.PostReceiveCure, this);
            }
        }
    }
}