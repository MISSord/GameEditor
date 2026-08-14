using System;

namespace EGamePlay.Combat
{

    /// <summary>
    /// 技能内联 BuffEffect 执行器：复用 BuffModifySetting 的配置字段，
    /// 让技能能够直接触发 BuffModify 行，而无需真正挂载 Buff。
    /// 主要用于减抗/易伤/光环/协同等高阶效果的“一次性触发”版本。
    /// </summary>
    public static class SkillInlineBuffEffectProcessor
    {
        /// <summary>
        /// 执行一条内联 BuffEffect。
        /// </summary>
        public static void Execute(BuffModifySetting setting, CombatEntity caster, Entity target, Ability sourceAbility = null, int damageSegmentIndex = 0)
        {
            if (setting == null || caster == null) return;

            if (setting.EffectModifyType == EffectModifyType.PlayerModify)
            {
                ApplyPlayerAttribute(setting, caster, target);
            }
            else if (setting.EffectModifyType == EffectModifyType.SkillHpDamage)
            {
                BuffModifyExecutionCore.ExecuteHpDamage(setting, caster, target, caster, DamageSource.Skill, sourceAbility, damageSegmentIndex);
            }
            else if (setting.EffectModifyType == EffectModifyType.SkillResource)
            {
                BuffModifyExecutionCore.ExecuteResource(setting, caster, target, caster, DamageSource.Skill);
            }
            else if (setting.EffectModifyType == EffectModifyType.SkillAddStatus)
            {
                ApplyAddStatus(setting, caster, target);
            }
            //else if (setting.EffectModifyType == EffectModifyType.DamageEffect)
            //{
            //    BuffModifyExecutionCore.ExecuteDamageOrResource(setting, caster, target, caster, DamageSource.Skill);
            //}
        }

        private static void ApplyAddStatus(BuffModifySetting setting, CombatEntity caster, Entity target)
        {
            if (target == null) return;
            int buffId = setting.ParamInt1;
            if (buffId <= 0) return;

            if (caster.AddStatusAbility.TryMakeAction(out var action))
            {
                action.Creator = caster;
                action.Target = target;
                action.ApplyAddStatusBySetting(buffId, setting.ParamString1);
            }
        }

        private static void ApplyPlayerAttribute(BuffModifySetting setting, CombatEntity caster, Entity target)
        {
            // ParamInt3 用作 ApplySide：0=作用于目标 1=作用于施法者
            var applyEntity = GetApplySide(setting.ParamInt3, caster, target);
            if (applyEntity == null) return;

            var attrType = (AttributeType)setting.ParamInt1;
            if (!Enum.IsDefined(typeof(AttributeType), attrType)) return;

            var attrComp = applyEntity.GetComponent<AttributeComponent>();
            if (attrComp == null) return;

            float value = setting.ParamFloat1;
            var modifyType = (ModifyType)setting.ParamInt2;

            var modifier = new FloatModifier { Value = value };
            var numeric = attrComp.GetNumeric(attrType);
            numeric?.AddModifier(modifyType, modifier);
        }

        private static CombatEntity GetApplySide(int applySideType, CombatEntity caster, Entity target)
        {
            return applySideType == DamageActionModifyConfig.ApplySideReceiver
                ? target as CombatEntity ?? caster
                : caster;
        }
    }
}

