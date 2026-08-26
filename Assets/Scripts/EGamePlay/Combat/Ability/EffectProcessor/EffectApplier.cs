using System;

namespace EGamePlay.Combat
{
    /// <summary>
    /// 开火即忘的效果请求。只带引用，不拷贝表行。
    /// 粘性 PlayerModify / PlayerControll 不要走这里。
    /// </summary>
    public struct EffectApplyRequest
    {
        public BuffModifySetting Setting;
        public ICombatUnit Caster;
        public Entity Target;
        public Entity TriggerSource;
        public DamageSource DamageSource;
        public Ability SourceAbility;
        public int DamageSegmentIndex;
    }

    /// <summary>
    /// 效果分发器：按 BuffModifySetting 创建 Damage / Resource / AddStatus 事务。
    /// 不负责 Buff 生命周期上的加/撤修饰。
    /// </summary>
    public static class EffectApplier
    {
        /// <summary>技能命中/轨道效果入口。</summary>
        public static void ApplySkillInline(
            BuffModifySetting setting,
            ICombatUnit caster,
            Entity target,
            Ability sourceAbility,
            int damageSegmentIndex)
        {
            Apply(new EffectApplyRequest
            {
                Setting = setting,
                Caster = caster,
                Target = target,
                TriggerSource = caster?.Entity,
                DamageSource = DamageSource.Skill,
                SourceAbility = sourceAbility,
                DamageSegmentIndex = damageSegmentIndex,
            });
        }

        /// <summary>按类型分发。未知类型与粘性控制/修饰直接忽略。</summary>
        public static void Apply(in EffectApplyRequest request)
        {
            var setting = request.Setting;
            var caster = request.Caster;
            if (setting == null || caster == null || caster.IsDisposed)
                return;

            var type = setting.EffectModifyType;
            if (type == EffectModifyType.SkillHpDamage || type == EffectModifyType.BuffHpDamage)
            {
                BuffModifyExecutionCore.ExecuteHpDamage(
                    setting, caster, request.Target, request.TriggerSource,
                    request.DamageSource, request.SourceAbility, request.DamageSegmentIndex);
                return;
            }

            if (type == EffectModifyType.SkillResource || type == EffectModifyType.BuffResource)
            {
                BuffModifyExecutionCore.ExecuteResource(
                    setting, caster, request.Target, request.TriggerSource, request.DamageSource);
                return;
            }

            if (type == EffectModifyType.SkillAddStatus || type == EffectModifyType.BuffAddStatus)
            {
                ApplyAddStatus(setting, caster, request.Target);
                return;
            }

            if (type == EffectModifyType.PlayerModify)
                ApplyInstantPlayerModify(setting, caster, request.Target);
        }

        static void ApplyAddStatus(BuffModifySetting setting, ICombatUnit caster, Entity target)
        {
            if (target == null || caster?.AddStatusAbility == null)
                return;

            int buffId = setting.ParamInt1;
            if (buffId <= 0)
                return;

            if (caster.AddStatusAbility.TryMakeAction(out var action))
            {
                action.Creator = caster;
                action.Target = target as ICombatUnit;
                action.ApplyAddStatusBySetting(buffId, setting.ParamString1);
            }
        }

        /// <summary>技能侧一次性属性修饰：认 ApplySide，不记录、不撤销。</summary>
        static void ApplyInstantPlayerModify(BuffModifySetting setting, ICombatUnit caster, Entity target)
        {
            ICombatUnit applyUnit = setting.ParamInt3 == DamageActionModifyConfig.ApplySideReceiver
                ? (target as ICombatUnit ?? caster)
                : caster;
            if (applyUnit?.Entity == null)
                return;

            var attrType = (AttributeType)setting.ParamInt1;
            if (!Enum.IsDefined(typeof(AttributeType), attrType))
                return;

            var attrComp = applyUnit.Entity.GetComponent<AttributeComponent>();
            if (attrComp == null)
                return;

            var modifier = new FloatModifier { Value = setting.ParamFloat1 };
            attrComp.GetNumeric(attrType)?.AddModifier((ModifyType)setting.ParamInt2, modifier);
        }
    }
}
