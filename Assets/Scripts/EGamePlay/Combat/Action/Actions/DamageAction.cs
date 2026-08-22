using System;

namespace EGamePlay.Combat
{
    public class DamageActionAbility : Entity, IActionAbility
    {
        public CombatEntity OwnerEntity { get { return GetParent<CombatEntity>(); } set { } }
        public bool Enable { get; set; }

        public bool TryMakeAction(out DamageAction action)
        {
            if (!Enable) { action = null; return false; }
            action = (DamageAction)CombatContext.Instance.AddAction<DamageAction>();
            action.Creator = OwnerEntity;
            return true;
        }
    }

    /// <summary>本次伤害的命中结果，用于飘字与是否扣血。</summary>
    [Flags]
    public enum DamageActionEffect
    {
        None = 0,
        Interrupt = 1,
        Immunity = 2,
        Dodge = 4,
    }

    /// <summary>伤害行动：六段乘区算值 → 前置行动点（可改结果/易伤闪避等）→ 按结果扣血/飘字 → 后置行动点。</summary>
    public class DamageAction : Entity, IActionExecute
    {
        public TriggerContext TriggerContext { get; set; }
        public DamageSource DamageSource { get; set; }
        public int DamageValue { get; set; }
        public CombatEntity Creator { get; set; }
        public Entity Target { get; set; }
        public DamageActionEffect DamageActionEffect { get; set; }
        public DamageEffect DamageEffect => TriggerContext.EffectConfig as DamageEffect;

        public void FinishAction() => Entity.Destroy(this);

        private void PreProcess()
        {
            Target = TriggerContext.Target;
            var segmentIndex = TriggerContext.DamageSegmentIndex;
            var skillId = TriggerContext.SourceAbility?.SkillID ?? 0;

            if (skillId > 0 && segmentIndex > 0)
            {
                DamageValue = DamageCalcuFormula.CalculateBySkillConfig(Creator, Target, skillId, segmentIndex);
            }
            else
            {
                // 无技能 ID 或段索引时，说明应该是Buff造成的，仍按 DamageEffect 计算。
                DamageValue = DamageCalcuFormula.Calculate(Creator, Target, DamageEffect);
            }
            Creator.TriggerActionPoint(ActionPointType.PreCauseDamage, this);
            if (Target is CombatEntity target)
                target.TriggerActionPoint(ActionPointType.PreReceiveDamage, this);
        }

        public void ApplyDamage()
        {
            PreProcess();

            if (!ShouldApplyDamageToTarget())
            {
                if (!DamageActionEffect.HasFlag(DamageActionEffect.Interrupt))
                    PostProcess();
                FinishAction();
                return;
            }

            var healthComp = Target.GetComponent<VitalComponent>();
            healthComp.ReceiveDamage(this);

            bool isDead = healthComp.CheckDead();
            if (isDead && Target is CombatEntity combatTarget)
                combatTarget.ApplyDeath();

            PostProcess();

            if (isDead)
            {
                var deadEvent = new EntityDeadEvent() { DeadEntity = Target };
                Target.Publish(deadEvent);
                CombatContext.Instance.Publish(deadEvent);
            }

            FinishAction();
        }

        private bool ShouldApplyDamageToTarget()
        {
            return !DamageActionEffect.HasFlag(DamageActionEffect.Interrupt)
                && !DamageActionEffect.HasFlag(DamageActionEffect.Dodge)
                && !DamageActionEffect.HasFlag(DamageActionEffect.Immunity);
        }

        private void PostProcess()
        {
            Creator.TriggerActionPoint(ActionPointType.PostCauseDamage, this);
            if (!Target.IsDisposed && Target is CombatEntity target)
                target.TriggerActionPoint(ActionPointType.PostReceiveDamage, this);
        }
    }

    public enum DamageSource
    {
        Skill,/// 技能
        Buff,/// Buff
    }
}