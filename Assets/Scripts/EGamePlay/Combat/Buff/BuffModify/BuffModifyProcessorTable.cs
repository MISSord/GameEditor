using System;

namespace EGamePlay.Combat
{
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
        /// <summary>
        /// 触发时应用。PlayerControll / PlayerModify 为粘性路径（RevertOnDisable 撤销）；
        /// 伤害/资源/上独立 Buff 走 EffectApplier。
        /// </summary>
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
            else if (reg.Config.EffectModifyType == EffectModifyType.BuffHpDamage
                     || reg.Config.EffectModifyType == EffectModifyType.BuffResource
                     || reg.Config.EffectModifyType == EffectModifyType.BuffAddStatus)
            {
                ApplyFireAndForget(reg, buff, target);
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
                var tagContainer = buff.OwnerEntity?.Entity?.GetComponent<StatusComponent>()?.TagContainer;
                if (tagContainer != null && reg.Config.ParamString1 != null)
                {
                    var src = TagSource.Modify(buff.Id);
                    foreach (var tag in reg.Config.ParamString1)
                    {
                        if (!string.IsNullOrEmpty(tag))
                            tagContainer.Pop(src, tag);
                    }
                }
                reg.ControlApplied = false;
            }

            if (reg.Config.EffectModifyType == EffectModifyType.PlayerModify && reg.AttributeApplied && reg.AttributeModifier != null)
            {
                var attrComp = buff.OwnerEntity?.Entity?.GetComponent<AttributeComponent>();
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
            var tagContainer = buff.OwnerEntity?.Entity?.GetComponent<StatusComponent>()?.TagContainer;
            if (tagContainer == null || reg.Config.ParamString1 == null) return;
            var src = TagSource.Modify(buff.Id);
            foreach (var tag in reg.Config.ParamString1)
            {
                if (!string.IsNullOrEmpty(tag))
                    tagContainer.Push(src, tag);
            }
            reg.ControlApplied = true;
        }

        private static void ApplyPlayerAttribute(ModifyRegistration reg, Buff buff)
        {
            if (reg.AttributeApplied) return;
            var attrType = (AttributeType)reg.Config.ParamInt1;
            if (!Enum.IsDefined(typeof(AttributeType), attrType)) return;

            var attrComp = buff.OwnerEntity?.Entity?.GetComponent<AttributeComponent>();
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

        /// <summary>
        /// Buff 触发的伤害/资源/上独立 Buff。粘性 PlayerModify / PlayerControll 不走这里。
        /// </summary>
        private static void ApplyFireAndForget(ModifyRegistration reg, Buff buff, Entity target)
        {
            var caster = buff.Caster as ICombatUnit;
            var creator = caster ?? buff.OwnerEntity;
            if (reg.Config.EffectModifyType == EffectModifyType.BuffAddStatus)
            {
                if (creator == null || creator.IsDisposed) return;
            }
            else if (caster == null)
            {
                return;
            }

            EffectApplier.Apply(new EffectApplyRequest
            {
                Setting = reg.Config,
                Caster = reg.Config.EffectModifyType == EffectModifyType.BuffAddStatus ? creator : caster,
                Target = target,
                TriggerSource = buff,
                DamageSource = DamageSource.Buff,
                SourceAbility = null,
                DamageSegmentIndex = 0,
            });
        }

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
