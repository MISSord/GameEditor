using System;

namespace EGamePlay.Combat
{
    /// <summary>
    /// ActionModify 用表字段约定（复用 BuffModifySetting）：
    /// ParamInt1=FilterType；ParamInt2=FilterValue；ParamInt3=ApplySide（语义标注，运行时按实际挂载方扫描）；
    /// ParamFloat1=增伤百分点（20 表示 +20%）；ParamString1=目标 Tag 列表（FilterByTargetTag）。
    /// </summary>
    public static class DamageActionModifyConfig
    {
        public const int FilterAll = 0;
        public const int FilterBySkillId = 1;
        public const int FilterByDamageType = 2;
        public const int FilterByDamageSource = 3;
        /// <summary>受击者身上有指定 BuffId（ParamInt2）。</summary>
        public const int FilterByTargetBuffId = 4;
        /// <summary>受击者身上有 ParamString1 中任一 Tag。</summary>
        public const int FilterByTargetTag = 5;
        public const int ApplySideReceiver = 0;
        public const int ApplySideCreator = 1;
    }

        /// <summary>
        /// StatusApplyModify 用表字段约定（复用 BuffModifySetting）：
        /// ParamInt1=FilterType；ParamInt2=FilterValue；ParamInt3=行为（0=免疫，1=抵抗%，2=改写 Id 未落地）。
        /// 挂在承受者 Buff 上，不在 OnTrigger 执行，由 <see cref="StatusApplyResolver"/> 在上 Buff 前置扫描。
        /// </summary>
        public static class StatusApplyModifyConfig
        {
            public const int FilterAll = 0;
            /// <summary>按目标 Buff 的 BigBuffType（ParamInt2）。</summary>
            public const int FilterByBigBuffType = 1;
            /// <summary>按目标 BuffId（ParamInt2）。</summary>
            public const int FilterByBuffId = 2;

            /// <summary>本次施加免疫，不落地。</summary>
            public const int BehaviorImmunity = 0;
            /// <summary>按 ParamFloat1 抵抗%，可叠层累加，上限 100。</summary>
            public const int BehaviorResist = 1;
            /// <summary>改写成其它 BuffId。尚未落地。</summary>
            public const int BehaviorRewrite = 2;
        }

    /// <summary>
    /// BuffModify Processor 表：按类型执行，无 BuffModify 子 Entity。
    /// </summary>
    public static class BuffModifyProcessorTable
    {
        /// <summary>
        /// 触发时应用。PlayerControll / PlayerModify 为粘性路径（RevertOnDisable 撤销）；
        /// AddShield 也是粘性，但在 Modify 组件 OnEnable 挂上，不走周期 OnTrigger（避免跳伤把盾刷满）。
        /// 伤害/资源/上独立 Buff 走 EffectApplier。ActionModify / StatusApplyModify 不在触发时执行。
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
            else if (reg.Config.EffectModifyType == EffectModifyType.AddShield)
            {
                ApplyShield(reg, buff);
            }
            else if (reg.Config.EffectModifyType == EffectModifyType.BuffHpDamage
                     || reg.Config.EffectModifyType == EffectModifyType.BuffResource
                     || reg.Config.EffectModifyType == EffectModifyType.BuffAddStatus)
            {
                ApplyFireAndForget(reg, buff, target);
            }
        }

        /// <summary>Buff 反激活时撤销 PlayerControll / PlayerModify / AddShield。</summary>
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
                    if (Enum.IsDefined(typeof(AttributeType), attrType)
                        && attrComp.TryGetNumeric(attrType, out var numeric)
                        && numeric != null)
                    {
                        numeric.RemoveModifier((ModifyType)reg.Config.ParamInt2, reg.AttributeModifier);
                    }
                }
                reg.AttributeApplied = false;
            }

            if (reg.Config.EffectModifyType == EffectModifyType.AddShield && reg.ShieldApplied)
            {
                VitalComponent vital = GetOwnerVital(buff);
                vital?.RemoveShield(buff.BuffID);
                reg.ShieldApplied = false;
            }
        }

        /// <summary>
        /// 刷新 / 叠层时把该 Buff 的盾段重置为 ParamFloat1 × 当前层数。部分吃掉的盾也会回满。
        /// </summary>
        public static void RefreshStickyShields(Buff buff)
        {
            if (buff == null || buff.IsDisposed || !buff.TryGet(out BuffModifyComponent modify))
                return;
            if (modify.Registrations == null)
                return;

            var list = modify.Registrations;
            for (int i = 0; i < list.Count; i++)
            {
                var reg = list[i];
                if (reg?.Config == null || reg.Config.EffectModifyType != EffectModifyType.AddShield)
                    continue;
                ApplyShield(reg, buff);
            }
        }

        /// <summary>
        /// 叠层变化时按「单层值 × 当前层数」重写已贴上的 PlayerModify，不重复 AddModifier。
        /// </summary>
        public static void RefreshStickyAttributes(BuffModifyComponent modify, Buff buff)
        {
            if (modify?.Registrations == null || buff == null || buff.IsDisposed)
                return;

            var attrComp = buff.OwnerEntity?.Entity?.GetComponent<AttributeComponent>();
            if (attrComp == null)
                return;

            float stacks = buff.GetStackCount();
            var list = modify.Registrations;
            for (int i = 0; i < list.Count; i++)
            {
                var reg = list[i];
                if (reg?.Config == null || reg.Config.EffectModifyType != EffectModifyType.PlayerModify)
                    continue;
                if (!reg.AttributeApplied || reg.AttributeModifier == null)
                    continue;

                float value = reg.Config.ParamFloat1 * stacks;
                if (Math.Abs(reg.AttributeModifier.Value - value) < 0.0001f)
                    continue;

                reg.AttributeModifier.Value = value;
                var attrType = (AttributeType)reg.Config.ParamInt1;
                if (!Enum.IsDefined(typeof(AttributeType), attrType))
                    continue;
                if (!attrComp.TryGetNumeric(attrType, out var numeric) || numeric == null)
                    continue;
                numeric.RefreshModifier((ModifyType)reg.Config.ParamInt2);
            }
        }

        /// <summary>
        /// 扫描承受者 StatusApplyModify：免疫（行为 0）或累加抵抗%（行为 1，单层值 × 层数）。热路径无 LINQ、无分配。
        /// </summary>
        public static void CollectStatusApplyScan(
            StatusComponent status,
            int buffId,
            int bigBuffType,
            out bool immunity,
            out float resistPercent)
        {
            immunity = false;
            resistPercent = 0f;
            if (status?.Statuses == null || buffId <= 0)
                return;

            var statuses = status.Statuses;
            for (int i = 0; i < statuses.Count; i++)
            {
                var buff = statuses[i];
                if (buff == null || !buff.Enable || buff.IsRemoving || buff.IsDisposed)
                    continue;
                if (!buff.TryGet(out BuffModifyComponent modify) || modify.Registrations == null)
                    continue;

                var regs = modify.Registrations;
                for (int r = 0; r < regs.Count; r++)
                {
                    var cfg = regs[r]?.Config;
                    if (cfg == null || cfg.EffectModifyType != EffectModifyType.StatusApplyModify)
                        continue;
                    if (!StatusApplyModifyMatches(cfg, buffId, bigBuffType))
                        continue;

                    int behavior = cfg.ParamInt3;
                    if (behavior == StatusApplyModifyConfig.BehaviorImmunity)
                    {
                        immunity = true;
                        return;
                    }

                    if (behavior == StatusApplyModifyConfig.BehaviorResist)
                    {
                        float pct = cfg.ParamFloat1;
                        if (pct > 0f)
                            resistPercent += pct * buff.GetStackCount();
                    }
                }
            }
        }

        static bool StatusApplyModifyMatches(BuffModifySetting cfg, int buffId, int bigBuffType)
        {
            int filter = cfg.ParamInt1;
            if (filter == StatusApplyModifyConfig.FilterAll)
                return true;
            if (filter == StatusApplyModifyConfig.FilterByBigBuffType)
                return bigBuffType == cfg.ParamInt2;
            if (filter == StatusApplyModifyConfig.FilterByBuffId)
                return buffId == cfg.ParamInt2;
            return false;
        }

        /// <summary>
        /// 扫描攻受双方已激活 Buff 上的 ActionModify，把匹配项的百分点加进增伤区。热路径无 LINQ、无分配。
        /// </summary>
        public static float CollectConditionalDamageBonus(ref DamageContext ctx)
        {
            float extra = 0f;
            extra += CollectActionModifyFrom(ctx.CreatorStatus, ref ctx);
            extra += CollectActionModifyFrom(ctx.TargetStatus, ref ctx);
            return extra;
        }

        static float CollectActionModifyFrom(StatusComponent status, ref DamageContext ctx)
        {
            if (status?.Statuses == null)
                return 0f;

            float extra = 0f;
            var statuses = status.Statuses;
            for (int i = 0; i < statuses.Count; i++)
            {
                var buff = statuses[i];
                if (buff == null || !buff.Enable || buff.IsDisposed)
                    continue;
                if (!buff.TryGet(out BuffModifyComponent modify) || modify.Registrations == null)
                    continue;

                var regs = modify.Registrations;
                for (int r = 0; r < regs.Count; r++)
                {
                    var cfg = regs[r]?.Config;
                    if (cfg == null || cfg.EffectModifyType != EffectModifyType.ActionModify)
                        continue;
                    if (!ActionModifyMatches(cfg, ref ctx))
                        continue;
                    extra += cfg.ParamFloat1 * 0.01f;
                }
            }
            return extra;
        }

        static bool ActionModifyMatches(BuffModifySetting cfg, ref DamageContext ctx)
        {
            int filter = cfg.ParamInt1;
            if (filter == DamageActionModifyConfig.FilterAll)
                return true;
            if (filter == DamageActionModifyConfig.FilterBySkillId)
                return ctx.SkillId != 0 && ctx.SkillId == cfg.ParamInt2;
            if (filter == DamageActionModifyConfig.FilterByDamageType)
                return (int)ctx.DamageType == cfg.ParamInt2;
            if (filter == DamageActionModifyConfig.FilterByDamageSource)
                return (int)ctx.DamageSource == cfg.ParamInt2;
            if (filter == DamageActionModifyConfig.FilterByTargetBuffId)
                return TargetHasBuffId(ref ctx, cfg.ParamInt2);
            if (filter == DamageActionModifyConfig.FilterByTargetTag)
                return TargetHasAnyTag(ref ctx, cfg.ParamString1);
            return false;
        }

        static bool TargetHasBuffId(ref DamageContext ctx, int buffId)
        {
            if (buffId <= 0)
                return false;
            var status = ctx.TargetStatus;
            if (status == null || !status.TryGetBuffById(buffId, out var buff) || buff == null)
                return false;
            return buff.Enable;
        }

        static bool TargetHasAnyTag(ref DamageContext ctx, System.Collections.Generic.List<string> tags)
        {
            var container = ctx.TargetStatus?.TagContainer;
            if (container == null || tags == null)
                return false;
            for (int i = 0; i < tags.Count; i++)
            {
                string tag = tags[i];
                if (!string.IsNullOrEmpty(tag) && container.HasTag(tag))
                    return true;
            }
            return false;
        }

        static void ApplyPlayerControl(ModifyRegistration reg, Buff buff)
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

        static void ApplyPlayerAttribute(ModifyRegistration reg, Buff buff)
        {
            if (reg.AttributeApplied) return;
            var attrType = (AttributeType)reg.Config.ParamInt1;
            if (!Enum.IsDefined(typeof(AttributeType), attrType)) return;

            var attrComp = buff.OwnerEntity?.Entity?.GetComponent<AttributeComponent>();
            if (attrComp == null || !attrComp.TryGetNumeric(attrType, out var numeric) || numeric == null)
                return;

            float value = reg.Config.ParamFloat1 * buff.GetStackCount();
            var modifyType = (ModifyType)reg.Config.ParamInt2;

            reg.AttributeModifier = new FloatModifier { Value = value };
            numeric.AddModifier(modifyType, reg.AttributeModifier);
            reg.AttributeApplied = true;
        }

        static void ApplyShield(ModifyRegistration reg, Buff buff)
        {
            VitalComponent vital = GetOwnerVital(buff);
            if (vital == null)
                return;

            int value = (int)(reg.Config.ParamFloat1 * buff.GetStackCount());
            if (value <= 0)
                return;

            vital.AddOrReplaceShield(buff.BuffID, value);
            reg.ShieldApplied = true;
        }

        static VitalComponent GetOwnerVital(Buff buff)
        {
            if (buff?.OwnerEntity == null)
                return null;
            return buff.OwnerEntity.CurrentVital
                ?? buff.OwnerEntity.Entity?.GetComponent<VitalComponent>();
        }

        /// <summary>
        /// Buff 触发的伤害/资源/上独立 Buff。粘性 PlayerModify / PlayerControll 不走这里。
        /// </summary>
        static void ApplyFireAndForget(ModifyRegistration reg, Buff buff, Entity target)
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
    }
}
