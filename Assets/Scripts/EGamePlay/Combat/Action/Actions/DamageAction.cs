using System;
using UnityEngine;

namespace EGamePlay.Combat
{
    public class DamageActionAbility : Entity, IActionAbility
    {
        public ICombatUnit OwnerEntity => GetParent<Entity>() as ICombatUnit;
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
        /// <summary>正常命中，按算值扣血并走后置行动点。</summary>
        None = 0,
        /// <summary>伤害流程被中断（如施法/效果取消）；不扣血，且通常跳过 PostCause/PostReceive 行动点。</summary>
        Interrupt = 1,
        /// <summary>目标免疫本次伤害（如无敌 Buff）；不扣血，仍触发后置行动点。</summary>
        Immunity = 2,
        /// <summary>目标闪避本次伤害（如 Buff.Roll 无敌帧）；不扣血，仍触发后置行动点，不播受击表现。</summary>
        Dodge = 4,
    }

    /// <summary>伤害行动：六段乘区算值 → 前置行动点（可改结果/易伤闪避等）→ 按结果扣血/飘字 → 后置行动点。</summary>
    public class DamageAction : Entity, IActionExecute
    {
        public TriggerContext TriggerContext { get; set; }
        public DamageSource DamageSource { get; set; }
        public int DamageValue { get; set; }
        /// <summary>本次结算是否暴击；供飘字放大，不参与扣血。</summary>
        public bool IsCritical { get; set; }
        /// <summary>本次结算使用的属性类型；供飘字染色。</summary>
        public DamageType AppliedDamageType { get; set; }
        /// <summary>攻击盒与受击体接触点；无盒体采样（如 DoT）时为 false，飘字回退胸口。</summary>
        public bool HasHitWorldPosition { get; set; }
        public Vector3 HitWorldPosition { get; set; }
        public ICombatUnit Creator { get; set; }
        public ICombatUnit Target { get; set; }
        public DamageActionEffect DamageActionEffect { get; set; }
        public DamageEffect DamageEffect => TriggerContext.EffectConfig as DamageEffect;

        public void FinishAction() => Entity.Destroy(this);

        void PreProcess()
        {
            Target = TriggerContext.Target as ICombatUnit;
            var segmentIndex = TriggerContext.DamageSegmentIndex;
            var skillId = TriggerContext.SourceAbility?.SkillID ?? 0;
            Entity creatorEntity = Creator?.Entity;
            Entity targetEntity = Target?.Entity;

            DamageContext ctx;
            if (skillId > 0 && segmentIndex > 0)
            {
                DamageValue = DamageCalcuFormula.CalculateBySkillConfig(
                    creatorEntity, targetEntity, skillId, segmentIndex, out ctx);
            }
            else
            {
                DamageValue = DamageCalcuFormula.Calculate(creatorEntity, targetEntity, DamageEffect, out ctx);
            }

            IsCritical = ctx.IsCritical;
            AppliedDamageType = ctx.DamageType;
            HasHitWorldPosition = TriggerContext.HasHitWorldPosition;
            HitWorldPosition = TriggerContext.HitWorldPosition;

            Creator?.TriggerActionPoint(ActionPointType.PreCauseDamage, this);
            Target?.TriggerActionPoint(ActionPointType.PreReceiveDamage, this);
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

            if (Target == null || Target.CurrentVital == null)
            {
                FinishAction();
                return;
            }

            Target.CurrentVital.ReceiveDamage(this);

            bool isDead = Target.CurrentVital.CheckDead();
            if (isDead)
                Target.ApplyDeath();

            PostProcess();

            if (isDead)
            {
                var deadEvent = new EntityDeadEvent { DeadEntity = Target.Entity };
                Target.Entity.Publish(deadEvent);
                CombatContext.Instance.Publish(deadEvent);
            }

            FinishAction();
        }

        bool ShouldApplyDamageToTarget()
        {
            return !DamageActionEffect.HasFlag(DamageActionEffect.Interrupt)
                && !DamageActionEffect.HasFlag(DamageActionEffect.Dodge)
                && !DamageActionEffect.HasFlag(DamageActionEffect.Immunity);
        }

        void PostProcess()
        {
            Creator?.TriggerActionPoint(ActionPointType.PostCauseDamage, this);
            if (Target != null && !Target.IsDisposed)
                Target.TriggerActionPoint(ActionPointType.PostReceiveDamage, this);
        }

        public override void OnReset()
        {
            TriggerContext = default;
            DamageSource = default;
            DamageValue = 0;
            IsCritical = false;
            AppliedDamageType = DamageType.Physic;
            HasHitWorldPosition = false;
            HitWorldPosition = default;
            Creator = null;
            Target = null;
            DamageActionEffect = DamageActionEffect.None;
        }
    }

    public enum DamageSource
    {
        Skill, /// 技能
        Buff,  /// Buff
    }
}
