using UnityEngine;

namespace EGamePlay.Combat
{
    /// <summary>
    /// 通用资源/属性计算公式（比 DamageCalcuFormula 简化，不走防御/抗性/暴击等乘区）。
    /// 用于 Mana、特殊条、护盾、韧性等。
    /// </summary>
    public enum ResourceFormulaType
    {
        /// <summary>固定值：value = A</summary>
        Flat = 0,
        /// <summary>施法者属性缩放：value = Attr(caster, AttrType) * A</summary>
        CasterAttrMul = 1,
        /// <summary>目标属性缩放：value = Attr(target, AttrType) * A</summary>
        TargetAttrMul = 2,
        /// <summary>施法者属性缩放 + 固定值：value = Attr(caster, AttrType) * A + B</summary>
        CasterAttrMulAddFlat = 3,
        /// <summary>目标最大资源百分比：value = MaxVital(target, VitalType) * A</summary>
        TargetMaxVitalPct = 4,
        /// <summary>目标当前资源百分比：value = CurVital(target, VitalType) * A</summary>
        TargetCurVitalPct = 5,
        /// <summary>目标缺失资源百分比：value = (Max - Cur) * A</summary>
        TargetMissingVitalPct = 6,
        /// <summary>施法者最大资源百分比：value = MaxVital(caster, VitalType) * A</summary>
        CasterMaxVitalPct = 7,
        /// <summary>施法者当前资源百分比：value = CurVital(caster, VitalType) * A</summary>
        CasterCurVitalPct = 8,
    }

    /// <summary>
    /// 通用资源/属性计算上下文。
    /// </summary>
    public struct ResourceFormulaContext
    {
        public Entity Caster;
        public Entity Target;
        /// <summary>公式类型。</summary>
        public ResourceFormulaType FormulaType;
        /// <summary>资源/属性类型（既可表示 Vital，也可表示一般属性）。</summary>
        public AttributeType AttrOrVitalType;
        /// <summary>主系数 A（倍率或百分比）。</summary>
        public float A;
        /// <summary>附加值 B（可选，加算）。</summary>
        public float B;
        /// <summary>向上取整/向下取整等策略（这里简单用 int）。</summary>
        public bool CeilResult;
    }

    public static class ResourceFormula
    {
        /// <summary>
        /// 计算通用资源/属性变化的整数值（正数=增加，负数=减少）。
        /// </summary>
        public static int Calculate(ResourceFormulaContext ctx)
        {
            float value = 0f;

            switch (ctx.FormulaType)
            {
                case ResourceFormulaType.Flat:
                    value = ctx.A;
                    break;

                case ResourceFormulaType.CasterAttrMul:
                    {
                        var attr = GetAttribute(ctx.Caster, ctx.AttrOrVitalType);
                        value = attr * ctx.A;
                        break;
                    }

                case ResourceFormulaType.TargetAttrMul:
                    {
                        var attr = GetAttribute(ctx.Target, ctx.AttrOrVitalType);
                        value = attr * ctx.A;
                        break;
                    }

                case ResourceFormulaType.CasterAttrMulAddFlat:
                    {
                        var attr = GetAttribute(ctx.Caster, ctx.AttrOrVitalType);
                        value = attr * ctx.A + ctx.B;
                        break;
                    }

                case ResourceFormulaType.TargetMaxVitalPct:
                    {
                        var max = GetMaxVital(ctx.Target, ctx.AttrOrVitalType);
                        value = max * ctx.A;
                        break;
                    }

                case ResourceFormulaType.TargetCurVitalPct:
                    {
                        var cur = GetCurVital(ctx.Target, ctx.AttrOrVitalType);
                        value = cur * ctx.A;
                        break;
                    }

                case ResourceFormulaType.TargetMissingVitalPct:
                    {
                        var max = GetMaxVital(ctx.Target, ctx.AttrOrVitalType);
                        var cur = GetCurVital(ctx.Target, ctx.AttrOrVitalType);
                        value = Mathf.Max(0f, (max - cur) * ctx.A);
                        break;
                    }

                case ResourceFormulaType.CasterMaxVitalPct:
                    {
                        var max = GetMaxVital(ctx.Caster, ctx.AttrOrVitalType);
                        value = max * ctx.A;
                        break;
                    }

                case ResourceFormulaType.CasterCurVitalPct:
                    {
                        var cur = GetCurVital(ctx.Caster, ctx.AttrOrVitalType);
                        value = cur * ctx.A;
                        break;
                    }
            }

            return ctx.CeilResult ? Mathf.CeilToInt(value) : Mathf.FloorToInt(value);
        }

        private static float GetAttribute(Entity entity, AttributeType type)
        {
            if (entity == null) return 0f;
            var attrComp = entity.GetComponent<AttributeComponent>();
            if (attrComp == null) return 0f;
            if (!attrComp.TryGetNumeric(type, out var numeric)) return 0f;
            return numeric.Value;
        }

        private static float GetMaxVital(Entity entity, AttributeType vitalType)
        {
            if (entity == null) return 0f;
            var attrComp = entity.GetComponent<AttributeComponent>();
            if (attrComp == null) return 0f;

            // VitalComponent 里用 AttributeMaxType 映射：HealthPoint -> HealthPointMax, Mana -> ManaMax
            // 这里直接通过约定：vitalType=HealthPoint => Max=HealthPointMax，Mana=>ManaMax，其他可以按需扩展
            AttributeType maxType = vitalType switch
            {
                AttributeType.HealthPoint => AttributeType.HealthPointMax,
                AttributeType.Mana => AttributeType.ManaMax,
                _ => vitalType, // 如果某些特殊条也用自身 Max 存在属性里，就直接用自身
            };

            if (!attrComp.TryGetNumeric(maxType, out var numeric)) return 0f;
            return numeric.Value;
        }

        private static float GetCurVital(Entity entity, AttributeType vitalType)
        {
            if (entity == null) return 0f;
            var vital = entity.GetComponent<VitalComponent>();
            if (vital == null) return 0f;
            return vital.GetVitalValue(vitalType);
        }
    }
}