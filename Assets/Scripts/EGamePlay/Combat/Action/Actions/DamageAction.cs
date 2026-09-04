using System;
using UnityEngine;
using EGamePlay;

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
        /// <summary>目标免疫本次伤害（如无敌 Buff）；不扣血，仍触发后置行动点，不播受击表现。</summary>
        Immunity = 2,
        /// <summary>目标闪避本次伤害（如 Buff.Roll 无敌帧）；不扣血，仍触发后置行动点，不播受击表现。</summary>
        Dodge = 4,
    }

    /// <summary>
    /// 伤害行动：算值生成伤害单 → onHit/beHurt → 致死预判（beforeKilled/onKill/beKilled）→ 扣血 → 后置点。
    /// 流程内加 Buff 延后落地。
    /// </summary>
    public class DamageAction : Entity, IActionExecute
    {
        public TriggerContext TriggerContext { get; set; }
        public DamageSource DamageSource { get; set; }
        public int DamageValue { get; set; }
        /// <summary>本刀被护盾吸收的量；扣血后才有值，致死预判不要读。</summary>
        public int ShieldAbsorbed { get; set; }
        /// <summary>溢出盾后实际扣 HP 的量；扣血后才有值。</summary>
        public int HpDamageApplied { get; set; }
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

        /// <summary>
        /// 结算本张伤害单。顺序：
        /// 1. 乘区算值，写入 DamageValue / 暴击 / 元素；
        /// 2. 攻击者 PreCauseDamage（onHit），受击者 PreReceiveDamage（beHurt），Buff 按 Priority 改本单，随后系统规则（如翻滚闪避）；
        /// 3. Interrupt 直接结束且不走后置；Dodge / Immunity 不扣血，仍走后置；
        /// 4. 若当前数值足以致死：PreBeKilled（可改伤害免死）→ 再判一次 → PreCauseKill / PreReceiveKill；
        /// 5. 先吞盾再扣溢出 HP，确认死亡则 ApplyDeath（死亡只看 HP）；
        /// 6. PostCauseDamage / PostReceiveDamage（点燃、飘字、硬直等，数值已锁定）；
        /// 7. 死亡则发 EntityDeadEvent。
        /// 全程效果锁：流程内新加的 Buff 等结束才落地，避免套进同一刀。
        /// </summary>
        public void ApplyDamage()
        {
            Target = TriggerContext.Target as ICombatUnit;
            ShieldAbsorbed = 0;
            HpDamageApplied = 0;
            using (CombatBuffPipeline.Lock(Creator, Target))
            {
                // 1. 生成伤害单（公式 + ActionModify 乘区）
                FillDamageFromFormula();

                // 2. onHit / beHurt：改 DamageValue、暴击结果、闪避免疫等
                CombatBuffPipeline.Notify(Creator, ActionPointType.PreCauseDamage, this);
                CombatBuffPipeline.Notify(Target, ActionPointType.PreReceiveDamage, this);

                // 3. 施法取消：不扣血、不后置
                if (DamageActionEffect.HasFlag(DamageActionEffect.Interrupt))
                {
                    FinishAction();
                    return;
                }

                // 3. 闪避 / 免疫：不扣血，仍后置（飘字、行动点）
                if (!ShouldApplyDamageToTarget())
                {
                    PostProcess();
                    FinishAction();
                    return;
                }

                // 4. 致死窗口（扣血前）：beforeKilled → 再判 → onKill / beKilled
                ResolveLethalWindow();

                // beforeKilled 可能把本单改成不扣血（免死 / 免疫）
                if (!ShouldApplyDamageToTarget())
                {
                    PostProcess();
                    FinishAction();
                    return;
                }

                if (Target == null || Target.CurrentVital == null)
                {
                    FinishAction();
                    return;
                }

                // 5. 先吞盾再扣溢出 HP；确认死亡再切死亡态
                Target.CurrentVital.ReceiveDamage(this);

                Creator?.Status?.NotifyHitDealt();
                Target.Status?.NotifyHitTaken();

                bool isDead = Target.CurrentVital.CheckDead();
                if (isDead)
                    Target.ApplyDeath();

                // 6. 后置：数值已锁定，适合点燃、飘字、受击硬直
                PostProcess();

                // 7. 死亡演出 / 逻辑订阅，与击杀 Buff 回调分开
                if (isDead)
                {
                    var deadEvent = new EntityDeadEvent { DeadEntity = Target.Entity };
                    Target.Entity.Publish(deadEvent);
                    CombatContext.Instance.Publish(deadEvent);
                }

                FinishAction();
            }
        }

        /// <summary>
        /// 文章黄框：仅当当前伤害单仍足以致死时，先 beforeKilled，再判定一次后走 onKill/beKilled。
        /// 全部在扣血之前，Buff 可改 <see cref="DamageValue"/> 或 <see cref="DamageActionEffect"/> 免死。
        /// </summary>
        void ResolveLethalWindow()
        {
            if (!WouldKill())
                return;

            CombatBuffPipeline.Notify(Target, ActionPointType.PreBeKilled, this);
            if (!WouldKill())
                return;

            CombatBuffPipeline.Notify(Creator, ActionPointType.PreCauseKill, this);
            CombatBuffPipeline.Notify(Target, ActionPointType.PreReceiveKill, this);
        }

        /// <summary>尚未扣血时，按当前伤害值与生命+护盾预判是否致死（不消耗盾）。</summary>
        bool WouldKill()
        {
            if (!ShouldApplyDamageToTarget() || Target?.CurrentVital == null)
                return false;
            return Target.CurrentVital.WouldDieFrom(DamageValue);
        }

        void FillDamageFromFormula()
        {
            var segmentIndex = TriggerContext.DamageSegmentIndex;
            var skillId = TriggerContext.SourceAbility?.SkillID ?? 0;
            Entity creatorEntity = Creator?.Entity;
            Entity targetEntity = Target?.Entity;
            var effect = DamageEffect;

            DamageContext ctx;
            // ApplySkillSegment 已写入倍率时直接走乘区，避免再查一遍段表和等级。
            if (DamageSource == DamageSource.Skill && skillId > 0 && segmentIndex > 0
                && (effect == null || effect.DamageValueProperty <= 0f))
            {
                int skillLevel = SkillSettingMgr.Instance.GetSkillLevel(Creator, TriggerContext.SourceAbility);
                DamageValue = DamageCalcuFormula.CalculateBySkillConfig(
                    creatorEntity, targetEntity, skillId, segmentIndex, DamageSource, skillLevel, out ctx);
            }
            else
            {
                DamageValue = DamageCalcuFormula.Calculate(
                    creatorEntity, targetEntity, effect, skillId, DamageSource, out ctx);
            }

            IsCritical = ctx.IsCritical;
            AppliedDamageType = ctx.DamageType;
            HasHitWorldPosition = TriggerContext.HasHitWorldPosition;
            HitWorldPosition = TriggerContext.HitWorldPosition;
        }

        void PostProcess()
        {
            CombatBuffPipeline.Notify(Creator, ActionPointType.PostCauseDamage, this);
            if (Target != null && !Target.IsDisposed)
                CombatBuffPipeline.Notify(Target, ActionPointType.PostReceiveDamage, this);
        }

        bool ShouldApplyDamageToTarget()
        {
            return !DamageActionEffect.HasFlag(DamageActionEffect.Interrupt)
                && !DamageActionEffect.HasFlag(DamageActionEffect.Dodge)
                && !DamageActionEffect.HasFlag(DamageActionEffect.Immunity);
        }

        public override void OnReset()
        {
            TriggerContext = default;
            DamageSource = default;
            DamageValue = 0;
            ShieldAbsorbed = 0;
            HpDamageApplied = 0;
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
