using System;
using UnityEngine;
using EGamePlay;

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
        public bool HasHitWorldPosition;
        public Vector3 HitWorldPosition;
    }

    /// <summary>
    /// 效果分发器：按 BuffModifySetting 创建 Damage / Resource / AddStatus 事务。
    /// 不负责 Buff 生命周期上的加/撤修饰。
    /// </summary>
    public static class EffectApplier
    {
        /// <summary>技能命中/轨道额外效果入口。主动技伤害请走 <see cref="ApplySkillSegment"/>。</summary>
        public static void ApplySkillInline(
            BuffModifySetting setting,
            ICombatUnit caster,
            Entity target,
            Ability sourceAbility,
            int damageSegmentIndex,
            bool hasHitWorldPosition = false,
            Vector3 hitWorldPosition = default)
        {
            if (IsHpDamageModify(setting))
                return;

            Apply(new EffectApplyRequest
            {
                Setting = setting,
                Caster = caster,
                Target = target,
                TriggerSource = caster?.Entity,
                DamageSource = DamageSource.Skill,
                SourceAbility = sourceAbility,
                DamageSegmentIndex = damageSegmentIndex,
                HasHitWorldPosition = hasHitWorldPosition,
                HitWorldPosition = hitWorldPosition,
            });
        }

        /// <summary>
        /// 主动技命中出伤：只查段表。段号 &lt; 1、无行、倍率 ≤ 0 时打日志并跳过，不静默 1 倍。
        /// 有行时再执行该段 <see cref="SkillDamageSetting.OnHitEffectIds"/>。
        /// </summary>
        public static void ApplySkillSegment(
            ICombatUnit caster,
            Entity target,
            Ability sourceAbility,
            int damageSegmentIndex,
            bool hasHitWorldPosition,
            Vector3 hitWorldPosition)
        {
            if (caster == null || caster.IsDisposed || target == null || sourceAbility == null)
                return;

            int skillId = sourceAbility.SkillID;
            if (damageSegmentIndex <= 0)
            {
                GameLog.CombatError($"[SkillDamage] 段号必须 ≥ 1 skillId={skillId} segment={damageSegmentIndex}");
                return;
            }

            var setting = SkillSettingMgr.Instance.GetSkillDamageSetting(skillId, damageSegmentIndex);
            if (setting == null)
            {
                GameLog.CombatError($"[SkillDamage] 没有段表行 skillId={skillId} segment={damageSegmentIndex}");
                return;
            }

            int skillLevel = SkillSettingMgr.Instance.GetSkillLevel(caster, sourceAbility);
            float ratio = setting.GetRatioAtLevel(skillLevel);
            if (ratio <= 0f)
            {
                GameLog.CombatError($"[SkillDamage] 倍率 ≤ 0，跳过 skillId={skillId} segment={damageSegmentIndex} level={skillLevel}");
                return;
            }

            var dmgEffect = new DamageEffect
            {
                DamageType = setting.DamageType,
                DamageValueProperty = ratio,
                FormulaType = (DamageCalcuFormulaType)setting.FormulaType,
                CanCrit = setting.CanCrit != 0,
            };

            var context = new TriggerContext
            {
                EffectConfig = dmgEffect,
                SourceAbility = sourceAbility,
                TriggerSource = caster.Entity,
                Target = target,
                DamageSegmentIndex = damageSegmentIndex,
                HasHitWorldPosition = hasHitWorldPosition,
                HitWorldPosition = hitWorldPosition,
            };

            if (caster.DamageAbility != null && caster.DamageAbility.TryMakeAction(out var damageAction))
            {
                damageAction.TriggerContext = context;
                damageAction.DamageSource = DamageSource.Skill;
                damageAction.ApplyDamage();
            }

            ApplyOnHitEffectIds(setting, caster, target, sourceAbility);
        }

        static void ApplyOnHitEffectIds(
            SkillDamageSetting damageSetting,
            ICombatUnit caster,
            Entity target,
            Ability sourceAbility)
        {
            var ids = damageSetting.OnHitEffectIds;
            if (ids == null || ids.Count == 0)
                return;

            for (int i = 0; i < ids.Count; i++)
            {
                var extra = SkillSettingMgr.Instance.GetBuffModifySettingOrNull(ids[i]);
                if (extra == null)
                    continue;
                ApplySkillInline(extra, caster, target, sourceAbility, 0);
            }
        }

        static bool IsHpDamageModify(BuffModifySetting setting)
        {
            if (setting == null)
                return false;
            var type = setting.EffectModifyType;
            return type == EffectModifyType.SkillHpDamage || type == EffectModifyType.BuffHpDamage;
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
                // 主动技命中不走这里；Buff 跳伤 / 非段表临时伤害仍读本行 Param。
                BuffModifyExecutionCore.ExecuteHpDamage(
                    setting, caster, request.Target, request.TriggerSource,
                    request.DamageSource, request.SourceAbility, request.DamageSegmentIndex,
                    request.HasHitWorldPosition, request.HitWorldPosition);
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
                action.Source = AddStatusSource.Combat;
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
