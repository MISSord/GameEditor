using EGamePlay;
using UnityEngine;

namespace EGamePlay.Combat
{
    /// <summary>伤害计算公式类型（仅影响基础区与防御区的具体公式）。</summary>
    public enum DamageCalcuFormulaType
    {
        Default,  // 攻击力 * 技能倍率
        Simple,   // 攻击力 - 防御力
        Flat,     // 固定数值（如 Ignite），SkillRate 直接作为伤害值，无视攻击力
    }

    /// <summary>单次伤害计算上下文，按乘区顺序写入中间结果；CreatorAttri/TargetAttri 在入口填充，避免各段重复 GetComponent。</summary>
    public struct DamageContext
    {
        public Entity Creator;
        public Entity Target;
        public DamageType DamageType;
        public bool CanCrit;
        public DamageCalcuFormulaType FormulaType;
        public float SkillRate;
        /// <summary>来源技能 Id；Buff 跳伤为 0。供 ActionModify 按技能过滤。</summary>
        public int SkillId;
        /// <summary>技能伤害或 Buff 跳伤。供 ActionModify 按来源过滤。</summary>
        public DamageSource DamageSource;

        public AttributeComponent CreatorAttri;
        public AttributeComponent TargetAttri;
        public StatusComponent CreatorStatus;
        public StatusComponent TargetStatus;

        public float BaseDamage;
        public float AfterDefense;
        public float AfterResist;
        public float AfterCrit;
        public float AfterBonus;
        public float FinalDamage;
        public bool IsCritical;
    }

    /// <summary>伤害计算：最终伤害 = 基础区 × 防御区 × 抗性区 × 暴击区 × 增伤区 × 易伤区。</summary>
    public static class DamageCalcuFormula
    {
        private const float DefaultDefenceK = 300f;
        private const float MinDamage = 1f;

        public static int Calculate(Entity creator, Entity target, DamageEffect effect)
        {
            return Calculate(creator, target, effect, out _);
        }

        /// <summary>计算伤害并带回乘区上下文（含暴击与属性类型，供飘字使用）。</summary>
        public static int Calculate(Entity creator, Entity target, DamageEffect effect, out DamageContext ctx)
        {
            return Calculate(creator, target, effect, 0, DamageSource.Skill, out ctx);
        }

        /// <summary>按 DamageEffect 算值，并带上技能 Id / 伤害来源供条件增伤过滤。</summary>
        public static int Calculate(Entity creator, Entity target, DamageEffect effect, int skillId,
            DamageSource damageSource, out DamageContext ctx)
        {
            if (effect == null)
                return Calculate(creator, target, DamageType.Physic, false, DamageCalcuFormulaType.Default, 1f,
                    skillId, damageSource, out ctx);
            float rate = effect.DamageValueProperty > 0 ? effect.DamageValueProperty : 1f;
            return Calculate(creator, target, effect.DamageType, effect.CanCrit, effect.FormulaType, rate,
                skillId, damageSource, out ctx);
        }

        /// <summary>
        /// 按段表计算伤害。无行或倍率 ≤ 0 返回 0，不回退 1 倍攻击。
        /// </summary>
        public static int CalculateBySkillConfig(Entity creator, Entity target, int skillId, int segmentIndex)
        {
            return CalculateBySkillConfig(creator, target, skillId, segmentIndex, out _);
        }

        /// <summary>按技能伤害段计算，并带回暴击/属性等上下文。</summary>
        public static int CalculateBySkillConfig(Entity creator, Entity target, int skillId, int segmentIndex, out DamageContext ctx)
        {
            return CalculateBySkillConfig(creator, target, skillId, segmentIndex, DamageSource.Skill, out ctx);
        }

        /// <summary>按技能伤害段计算；技能等级从施法者 <c>SkillLevelComponent</c> 读取。</summary>
        public static int CalculateBySkillConfig(Entity creator, Entity target, int skillId, int segmentIndex,
            DamageSource damageSource, out DamageContext ctx)
        {
            int skillLevel = SkillSettingMgr.Instance.GetSkillLevel(creator as ICombatUnit, skillId);
            return CalculateBySkillConfig(creator, target, skillId, segmentIndex, damageSource, skillLevel, out ctx);
        }

        /// <summary>按技能伤害段与指定技能等级计算。</summary>
        public static int CalculateBySkillConfig(Entity creator, Entity target, int skillId, int segmentIndex,
            DamageSource damageSource, int skillLevel, out DamageContext ctx)
        {
            var setting = SkillSettingMgr.Instance.GetSkillDamageSetting(skillId, segmentIndex);
            if (setting == null)
            {
                ctx = default;
                return 0;
            }

            var damageType = setting.DamageType;
            var canCrit = setting.CanCrit != 0;
            var formulaType = (DamageCalcuFormulaType)setting.FormulaType;
            float ratio = setting.GetRatioAtLevel(skillLevel);
            if (ratio <= 0f)
            {
                ctx = default;
                return 0;
            }

            return Calculate(creator, target, damageType, canCrit, formulaType, ratio, skillId, damageSource, out ctx);
        }

        public static int Calculate(Entity creator, Entity target, DamageType damageType, bool canCrit,
            DamageCalcuFormulaType formulaType, float skillRate = 1f)
        {
            return Calculate(creator, target, damageType, canCrit, formulaType, skillRate, out _);
        }

        /// <summary>填充伤害上下文后走六段乘区，结果写在 <paramref name="ctx"/>。</summary>
        public static int Calculate(Entity creator, Entity target, DamageType damageType, bool canCrit,
            DamageCalcuFormulaType formulaType, float skillRate, out DamageContext ctx)
        {
            return Calculate(creator, target, damageType, canCrit, formulaType, skillRate, 0, DamageSource.Skill, out ctx);
        }

        /// <summary>填充伤害上下文后走六段乘区，结果写在 <paramref name="ctx"/>。</summary>
        public static int Calculate(Entity creator, Entity target, DamageType damageType, bool canCrit,
            DamageCalcuFormulaType formulaType, float skillRate, int skillId, DamageSource damageSource,
            out DamageContext ctx)
        {
            ctx = new DamageContext
            {
                Creator = creator,
                Target = target,
                DamageType = damageType,
                CanCrit = canCrit,
                FormulaType = formulaType,
                SkillRate = skillRate,
                SkillId = skillId,
                DamageSource = damageSource,
                CreatorAttri = creator?.GetComponent<AttributeComponent>(),
                TargetAttri = target?.GetComponent<AttributeComponent>(),
                CreatorStatus = creator?.GetComponent<StatusComponent>(),
                TargetStatus = target?.GetComponent<StatusComponent>(),
            };
            return Calculate(ref ctx);
        }

        public static int Calculate(ref DamageContext ctx)
        {
            if (ctx.CreatorAttri == null) ctx.CreatorAttri = ctx.Creator?.GetComponent<AttributeComponent>();
            if (ctx.TargetAttri == null) ctx.TargetAttri = ctx.Target?.GetComponent<AttributeComponent>();
            if (ctx.CreatorStatus == null) ctx.CreatorStatus = ctx.Creator?.GetComponent<StatusComponent>();
            if (ctx.TargetStatus == null) ctx.TargetStatus = ctx.Target?.GetComponent<StatusComponent>();

            CalcBaseZone(ref ctx);
            CalcDefenseZone(ref ctx);
            CalcResistZone(ref ctx);
            CalcCritZone(ref ctx);
            CalcBonusZone(ref ctx);
            CalcVulnerabilityZone(ref ctx);
            ctx.FinalDamage = Mathf.Max(ctx.FinalDamage, MinDamage);
            return Mathf.CeilToInt(ctx.FinalDamage);
        }

        //计算基础数值
        private static void CalcBaseZone(ref DamageContext ctx)
        {
            if (ctx.FormulaType == DamageCalcuFormulaType.Flat)
            {
                ctx.BaseDamage = ctx.SkillRate;
                return;
            }
            if (ctx.CreatorAttri == null || !ctx.CreatorAttri.TryGetNumeric(AttributeType.Attack, out var attack))
            {
                ctx.BaseDamage = 0;
                return;
            }
            ctx.BaseDamage = attack.Value * ctx.SkillRate;
        }

        //计算防御减免
        private static void CalcDefenseZone(ref DamageContext ctx)
        {
            ctx.AfterDefense = ctx.BaseDamage;
            if (ctx.DamageType == DamageType.Real || ctx.TargetAttri == null)
                return;

            float def = 0f;
            if (ctx.DamageType == DamageType.Physic)
            {
                if (ctx.TargetAttri.TryGetNumeric(AttributeType.PhysicalDefense, out var pd))
                    def = pd.Value;
                else if (ctx.TargetAttri.TryGetNumeric(AttributeType.Defense, out var d))
                    def = d.Value;
            }

            if (ctx.FormulaType == DamageCalcuFormulaType.Simple)
                ctx.AfterDefense = Mathf.Max(ctx.BaseDamage - def, 0f);
            else
                ctx.AfterDefense = ctx.BaseDamage * (1f - def / (def + DefaultDefenceK));
        }

        //计算抗性减免
        private static void CalcResistZone(ref DamageContext ctx)
        {
            ctx.AfterResist = ctx.AfterDefense;
            if (ctx.DamageType == DamageType.Real || ctx.TargetAttri == null)
                return;

            AttributeType resistType = GetResistAttributeType(ctx.DamageType);
            float resist = ctx.TargetAttri.TryGetNumeric(resistType, out var r) ? Mathf.Clamp01(r.Value) : 0f;
            ctx.AfterResist = ctx.AfterDefense * (1f - resist);
        }

        //计算暴击
        private static void CalcCritZone(ref DamageContext ctx)
        {
            ctx.AfterCrit = ctx.AfterResist;
            ctx.IsCritical = false;
            if (!ctx.CanCrit || ctx.CreatorAttri == null) return;
            if (!ctx.CreatorAttri.TryGetNumeric(AttributeType.CriticalProbability, out var critRate) ||
                !ctx.CreatorAttri.TryGetNumeric(AttributeType.CriticalValue, out var critValue))
                return;

            if (RandomHelper.RandomRate() / 100f < critRate.Value)
            {
                ctx.IsCritical = true;
                ctx.AfterCrit = ctx.AfterResist * critValue.Value;
            }
        }

        //计算增伤
        private static void CalcBonusZone(ref DamageContext ctx)
        {
            float bonus = 0f;
            if (ctx.CreatorAttri != null)
            {
                if (ctx.CreatorAttri.TryGetNumeric(AttributeType.DamageBonus, out var db))
                    bonus += db.Value;
                AttributeType bonusType = GetBonusAttributeType(ctx.DamageType);
                if (ctx.CreatorAttri.TryGetNumeric(bonusType, out var typeBonus))
                    bonus += typeBonus.Value;
            }
            bonus += BuffModifyProcessorTable.CollectConditionalDamageBonus(ref ctx);
            ctx.AfterBonus = ctx.AfterCrit * (1f + bonus);
        }

        //计算易伤
        private static void CalcVulnerabilityZone(ref DamageContext ctx)
        {
            ctx.FinalDamage = ctx.AfterBonus;
            if (ctx.TargetAttri == null || !ctx.TargetAttri.TryGetNumeric(AttributeType.Vulnerability, out var vuln))
                return;
            ctx.FinalDamage = ctx.AfterBonus * (1f + vuln.Value);
        }

        private static AttributeType GetResistAttributeType(DamageType damageType)
        {
            if (damageType == DamageType.Physic) return AttributeType.PhysicResist;
            if (damageType == DamageType.Fire) return AttributeType.FireResist;
            if (damageType == DamageType.Ice) return AttributeType.IceResist;
            if (damageType == DamageType.Electric) return AttributeType.ElectricResist;
            return AttributeType.PhysicResist;
        }

        private static AttributeType GetBonusAttributeType(DamageType damageType)
        {
            if (damageType == DamageType.Physic) return AttributeType.PhysicDamageBonus;
            if (damageType == DamageType.Fire) return AttributeType.FireDamageBonus;
            if (damageType == DamageType.Ice) return AttributeType.IceDamageBonus;
            if (damageType == DamageType.Electric) return AttributeType.ElectricDamageBonus;
            return AttributeType.PhysicDamageBonus;
        }
    }
}
