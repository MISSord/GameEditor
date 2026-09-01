using UnityEngine;

namespace EGamePlay.Combat
{
    /// <summary>
    /// BuffModify 与技能内联 BuffEffect 共享的执行核心：
    /// 负责根据 BuffModifySetting 计算伤害或资源变动，不关心注册/撤销等生命周期。
    /// 槽位含义见 EffectModifyParamSlots.md。
    /// </summary>
    public static class BuffModifyExecutionCore
    {
        /// <summary>
        /// 执行 HP 伤害（SkillHpDamage / BuffHpDamage）。
        /// 槽位：ParamInt1=DamageCalcuFormulaType, ParamInt2=DamageType, ParamInt3=CanCrit, ParamFloat1=skillRate。
        /// </summary>
        public static void ExecuteHpDamage(
            BuffModifySetting setting,
            ICombatUnit caster,
            Entity target,
            Entity triggerSource,
            DamageSource damageSource,
            Ability sourceAbility = null,
            int damageSegmentIndex = 0,
            bool hasHitWorldPosition = false,
            Vector3 hitWorldPosition = default)
        {
            if (setting == null || caster == null || target == null)
                return;

            var formulaType = (DamageCalcuFormulaType)setting.ParamInt1;
            var damageType = (DamageType)setting.ParamInt2;
            bool canCrit = setting.ParamInt3 != 0;
            float skillRate = setting.ParamFloat1 > 0f ? setting.ParamFloat1 : 1f;

            var dmgEffect = new DamageEffect
            {
                DamageType = damageType,
                DamageValueProperty = skillRate,
                FormulaType = formulaType,
                CanCrit = canCrit,
            };

            var context = new TriggerContext
            {
                EffectConfig = dmgEffect,
                SourceAbility = sourceAbility,
                TriggerSource = triggerSource,
                Target = target,
                DamageSegmentIndex = damageSegmentIndex,
                HasHitWorldPosition = hasHitWorldPosition,
                HitWorldPosition = hitWorldPosition,
            };

            if (caster.DamageAbility != null && caster.DamageAbility.TryMakeAction(out var damageAction))
            {
                damageAction.TriggerContext = context;
                damageAction.DamageSource = damageSource;
                damageAction.ApplyDamage();
            }
        }

        /// <summary>
        /// 执行资源变动（SkillResource / BuffResource）。
        /// 目标由外部（技能/Buff 触发逻辑）选定后传入，Effect 不参与目标选择；ParamInt3 预留。
        /// 槽位：ParamInt1=ResourceFormulaType, ParamInt2=AttributeType, ParamInt3=预留, ParamFloat1=A, ParamFloat2=B。
        /// </summary>
        public static void ExecuteResource(
            BuffModifySetting setting,
            ICombatUnit caster,
            Entity target,
            Entity triggerSource,
            DamageSource damageSource)
        {
            if (setting == null || caster == null || target == null)
                return;

            var formulaType = (ResourceFormulaType)setting.ParamInt1;
            var attrType = (AttributeType)setting.ParamInt2;
            float a = setting.ParamFloat1;
            float b = setting.ParamFloat2;

            var ctx = new ResourceFormulaContext
            {
                Caster = caster.Entity,
                Target = target,
                FormulaType = formulaType,
                AttrOrVitalType = attrType,
                A = a,
                B = b,
                CeilResult = true,
            };

            int delta = ResourceFormula.Calculate(ctx);
            if (delta == 0) return;

            // 统一通过 ResourceAction 执行所有资源变动（生命/能量等、正负皆可），
            // 以便复用治疗流水线与行动点事件。
            if (caster.ResourceAbility != null)
            {
                if (caster.ResourceAbility.TryMakeAction(out var cureAction))
                {
                    var cureEffect = new CureEffect
                    {
                        AttributeType = attrType,
                        CureValueProperty = delta,
                    };

                    var triggerContext = new TriggerContext
                    {
                        EffectConfig = cureEffect,
                        SourceAbility = null,
                        TriggerSource = triggerSource,
                        Target = target,
                    };

                    cureAction.Target = target as ICombatUnit;
                    cureAction.TriggerContext = triggerContext;
                    cureAction.ApplyCure();
                    return;
                }
            }

            var vital = target.GetComponent<VitalComponent>();
            if (vital == null) return;

            if (delta > 0)
                vital.AddValue(attrType, delta);
            else
                vital.MinusValue(attrType, -delta);
        }

        ///// <summary>
        ///// [兼容旧配置] 执行 DamageEffect 的伤害或资源逻辑，Param 含义与拆分前一致。
        ///// </summary>
        //public static void ExecuteDamageOrResource(
        //    BuffModifySetting setting,
        //    CombatEntity caster,
        //    Entity target,
        //    Entity triggerSource,
        //    DamageSource damageSource)
        //{
        //    if (setting == null || caster == null || target is not CombatEntity targetEntity)
        //        return;

        //    var mode = (DamageEffectFormulaMode)setting.ParamInt1;
        //    int formulaCode = setting.ParamInt2;
        //    var attrType = (AttributeType)setting.ParamInt3;
        //    float a = setting.ParamFloat1;
        //    float b = setting.ParamFloat2;

        //    if (mode == DamageEffectFormulaMode.HpDamage && attrType == AttributeType.HealthPoint)
        //    {
        //        var formulaType = (DamageCalcuFormulaType)formulaCode;
        //        var damageType = DamageType.Real;
        //        bool canCrit = false;
        //        float skillRate = a > 0f ? a : 1f;

        //        var dmgEffect = new DamageEffect
        //        {
        //            DamageType = damageType,
        //            DamageValueProperty = skillRate,
        //            FormulaType = formulaType,
        //            CanCrit = canCrit,
        //        };

        //        var context = new TriggerContext
        //        {
        //            EffectConfig = dmgEffect,
        //            SourceAbility = null,
        //            TriggerSource = triggerSource,
        //            Target = targetEntity,
        //        };

        //        if (caster.DamageAbility.TryMakeAction(out var damageAction))
        //        {
        //            damageAction.TriggerContext = context;
        //            damageAction.DamageSource = damageSource;
        //            damageAction.ApplyDamage();
        //        }
        //        return;
        //    }

        //    var resFormulaType = (ResourceFormulaType)formulaCode;
        //    var resCtx = new ResourceFormulaContext
        //    {
        //        Caster = caster,
        //        Target = targetEntity,
        //        FormulaType = resFormulaType,
        //        AttrOrVitalType = attrType,
        //        A = a,
        //        B = b,
        //        CeilResult = true,
        //    };

        //    int delta = ResourceFormula.Calculate(resCtx);
        //    if (delta == 0) return;

        //    var vital = targetEntity.GetComponent<VitalComponent>();
        //    if (vital == null) return;

        //    if (delta > 0)
        //        vital.AddValue(attrType, delta);
        //    else
        //        vital.MinusValue(attrType, -delta);
        //}
    }
}
