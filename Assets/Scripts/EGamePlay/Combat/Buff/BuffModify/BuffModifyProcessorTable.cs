using System;
using UnityEngine;

namespace EGamePlay.Combat
{
    /// <summary>
    /// DamageEffect 的公式模式：血量走复杂伤害公式，其它资源走轻量 Resource 公式。
    /// </summary>
    public enum DamageEffectFormulaMode
    {
        /// <summary>按 HP 伤害处理：走 DamageCalcuFormula（带防御/抗性/暴击/增伤/易伤）。</summary>
        HpDamage = 0,
        /// <summary>按资源变化处理：走 ResourceFormula（不带防御/抗性/暴击）。</summary>
        Resource = 1,
    }

    /// <summary>
    /// ActionModify 用表字段约定（复用 BuffModifySetting）：
    /// BuffAttributeType=生效方：0=Buff拥有者为受击者 1=拥有者为攻击者；
    /// BuffBigTypeEnem=过滤类型：0=全部 1=技能ID 2=伤害类型 3=伤害来源。
    /// </summary>
    public static class DamageActionModifyConfig
    {
        public const int FilterAll = 0;
        public const int FilterBySkillId = 1;
        public const int FilterByDamageType = 2;
        public const int FilterByDamageSource = 3;
        public const int ApplySideReceiver = 0;
        public const int ApplySideCreator = 1;
    }

    /// <summary>
    /// BuffModify Processor 表：按类型执行，无 BuffModify 子 Entity。
    /// </summary>
    public static class BuffModifyProcessorTable
    {
        /// <summary>触发时应用（PlayerControll / PlayerModify / DamageEffect / CurveEffect）。</summary>
        public static void ApplyOnTrigger(ModifyRegistration reg, Buff buff, Entity target)
        {
            if (reg == null || reg.Config == null || buff == null || buff.IsDisposed) return;

            if (reg.Config.EffectModifyType == EffectModifyType.PlayerControll)
            {
                ApplyPlayerControl(reg, buff);
            }
            else if (reg.Config.EffectModifyType == EffectModifyType.PlayerModify)
            {
                ApplyPlayerAttribute(reg, buff);
            }
            else if (reg.Config.EffectModifyType == EffectModifyType.BuffHpDamage)
            {
                ApplyBuffHpDamage(reg, buff, target);
            }
            else if (reg.Config.EffectModifyType == EffectModifyType.BuffResource)
            {
                ApplyBuffResource(reg, buff, target);
            }
            else if (reg.Config.EffectModifyType == EffectModifyType.BuffAddStatus)
            {
                ApplyBuffAddStatus(reg, buff, target);
            }
            //else if (reg.Config.EffectModifyType == EffectModifyType.DamageEffect)
            //{
            //    ApplyDamageEffect(reg, buff, target);
            //}
            //else if (reg.Config.EffectModifyType == EffectModifyType.CurveEffect)
            //{
            //    ApplyCureOrResource(reg, buff, target);
            //}
        }

        /// <summary>Buff 反激活时撤销 PlayerControll / PlayerModify。</summary>
        public static void RevertOnDisable(ModifyRegistration reg, Buff buff)
        {
            if (reg == null || reg.Config == null || buff == null) return;

            if (reg.Config.EffectModifyType == EffectModifyType.PlayerControll && reg.ControlApplied)
            {
                var tagContainer = buff.OwnerEntity?.GetComponent<StatusComponent>()?.TagContainer;
                if (tagContainer != null && reg.Config.ParamString1 != null)
                {
                    foreach (var tag in reg.Config.ParamString1)
                    {
                        if (!string.IsNullOrEmpty(tag))
                        {
                            tagContainer.RemoveTag(tag);
                        }
                    }
                }
                reg.ControlApplied = false;
            }

            if (reg.Config.EffectModifyType == EffectModifyType.PlayerModify && reg.AttributeApplied && reg.AttributeModifier != null)
            {
                var attrComp = buff.OwnerEntity?.GetComponent<AttributeComponent>();
                if (attrComp != null)
                {
                    var attrType = (AttributeType)reg.Config.ParamInt1;
                    if (Enum.IsDefined(typeof(AttributeType), attrType))
                    {
                        var numeric = attrComp.GetNumeric(attrType);
                        numeric?.RemoveModifier((ModifyType)reg.Config.ParamInt2, reg.AttributeModifier);
                    }
                }
                reg.AttributeApplied = false;
            }
        }

        private static void ApplyPlayerControl(ModifyRegistration reg, Buff buff)
        {
            if (reg.ControlApplied) return;
            var tagContainer = buff.OwnerEntity?.GetComponent<StatusComponent>()?.TagContainer;
            if (tagContainer == null || reg.Config.ParamString1 == null) return;
            foreach (var tag in reg.Config.ParamString1)
            {
                if (!string.IsNullOrEmpty(tag))
                {
                    tagContainer.AddTag(tag);
                }
            }
            reg.ControlApplied = true;
        }

        private static void ApplyPlayerAttribute(ModifyRegistration reg, Buff buff)
        {
            if (reg.AttributeApplied) return;
            var attrType = (AttributeType)reg.Config.ParamInt1;
            if (!Enum.IsDefined(typeof(AttributeType), attrType)) return;

            var attrComp = buff.OwnerEntity?.GetComponent<AttributeComponent>();
            if (attrComp == null) return;

            float value = reg.Config.ParamFloat1;
            var modifyType = (ModifyType)reg.Config.ParamInt2;
            if (modifyType == ModifyType.PctAdd)
            {
                // 这里约定 ParamFloat1 已经是比例（例如 0.2 表示 20%）；如果你想用 20 表示 20%，在这里除以 100。
            }

            reg.AttributeModifier = new FloatModifier { Value = value };
            var numeric = attrComp.GetNumeric(attrType);
            numeric?.AddModifier(modifyType, reg.AttributeModifier);
            reg.AttributeApplied = true;
        }

        private static void ApplyBuffHpDamage(ModifyRegistration reg, Buff buff, Entity target)
        {
            if (buff.Caster is not CombatEntity caster) return;
            BuffModifyExecutionCore.ExecuteHpDamage(reg.Config, caster, target, buff, DamageSource.Buff);
        }

        private static void ApplyBuffResource(ModifyRegistration reg, Buff buff, Entity target)
        {
            if (buff.Caster is not CombatEntity caster) return;
            BuffModifyExecutionCore.ExecuteResource(reg.Config, caster, target, buff, DamageSource.Buff);
        }

        private static void ApplyBuffAddStatus(ModifyRegistration reg, Buff buff, Entity target)
        {
            if (target == null) return;
            int buffId = reg.Config.ParamInt1;
            if (buffId <= 0) return;

            var caster = buff.Caster as CombatEntity;
            var creator = caster ?? buff.OwnerEntity;
            if (creator == null || creator.IsDisposed) return;

            if (creator.AddStatusAbility.TryMakeAction(out var action))
            {
                action.Creator = creator;
                action.Target = target;
                action.ApplyAddStatusBySetting(buffId, reg.Config.ParamString1);
            }
        }

        ///// <summary>[兼容旧配置] Buff 造成的伤害或资源变动，走 DamageEffect 的旧 Param 约定。</summary>
        //private static void ApplyDamageEffect(ModifyRegistration reg, Buff buff, Entity target)
        //{
        //    if (buff.Caster is not CombatEntity caster) return;
        //    BuffModifyExecutionCore.ExecuteDamageOrResource(reg.Config, caster, target, buff, DamageSource.Buff);
        //}

        ///// <summary>资源型效果（治疗/回复能量等），通过 ResourceFormula 计算并作用于 VitalComponent。</summary>
        //private static void ApplyCureOrResource(ModifyRegistration reg, Buff buff, Entity target)
        //{
        //    if (buff.Caster is not CombatEntity caster) return;

        //    // ParamInt1 = ResourceFormulaType
        //    // ParamInt2 = AttributeType(资源类型：HealthPoint/Mana/特殊条)
        //    // ParamInt3 = TargetSide(0=目标 1=施法者)
        //    // ParamFloat1 = A, ParamFloat2 = B
        //    var formulaType = (ResourceFormulaType)reg.Config.ParamInt1;
        //    var vitalType = (AttributeType)reg.Config.ParamInt2;
        //    int side = reg.Config.ParamInt3;

        //    Entity actualTarget = null;
        //    if (side == 1)
        //    {
        //        actualTarget = caster;
        //    }
        //    else
        //    {
        //        if (target != null)
        //        {
        //            actualTarget = target;
        //        }
        //        else
        //        {
        //            actualTarget = caster;
        //        }
        //    }

        //    if (actualTarget == null) return;

        //    var ctx = new ResourceFormulaContext
        //    {
        //        Caster = caster,
        //        Target = actualTarget,
        //        FormulaType = formulaType,
        //        AttrOrVitalType = vitalType,
        //        A = reg.Config.ParamFloat1,
        //        B = reg.Config.ParamFloat2,
        //        CeilResult = true,
        //    };

        //    int delta = ResourceFormula.Calculate(ctx);
        //    if (delta == 0) return;

        //    var vital = actualTarget.GetComponent<VitalComponent>();
        //    if (vital == null) return;

        //    if (delta > 0)
        //    {
        //        vital.AddValue(vitalType, delta);
        //    }
        //    else
        //    {
        //        vital.MinusValue(vitalType, -delta);
        //    }
        //}

        //private static bool MatchFilter(DamageAction action, int filterType, int filterValue)
        //{
        //    if (filterType == DamageActionModifyConfig.FilterAll) return true;
        //    if (filterType == DamageActionModifyConfig.FilterBySkillId) return TryGetSkillId(action, out int sid) && sid == filterValue;
        //    if (filterType == DamageActionModifyConfig.FilterByDamageType) return action.DamageEffect != null && (int)action.DamageEffect.DamageType == filterValue;
        //    if (filterType == DamageActionModifyConfig.FilterByDamageSource) return (int)action.DamageSource == filterValue;
        //    return false;
        //}

        private static bool TryGetSkillId(DamageAction action, out int skillId)
        {
            skillId = 0;
            var ability = action.TriggerContext.SourceAbility;
            if (ability == null) return false;
            skillId = ability.SkillID;
            return true;
        }
    }
}
