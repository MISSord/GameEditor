using System;
using System.Collections.Generic;

namespace EGamePlay.Combat
{
    /// <summary>本次施加 Buff 的来源。非 Combat 跳过 Pre / 免疫，避免被动被控抗挡掉。</summary>
    public enum AddStatusSource : byte
    {
        /// <summary>技能命中、Buff 点燃等战斗施加。</summary>
        Combat = 0,
        /// <summary>被动技能常驻 Buff。</summary>
        Passive = 1,
        /// <summary>系统 / 调试直接挂载。</summary>
        System = 2,
    }

    /// <summary>本次上 Buff 的裁决结果，对齐 <see cref="DamageActionEffect"/>。</summary>
    [Flags]
    public enum AddStatusActionEffect
    {
        /// <summary>正常施加（新建或刷新）。</summary>
        None = 0,
        /// <summary>流程中断（目标非法/已死亡等）；不落地，且跳过 PostGive/PostReceive。</summary>
        Interrupt = 1,
        /// <summary>目标免疫本次施加；不落地，仍走后置（抵抗飘字等）。</summary>
        Immunity = 2,
        /// <summary>抵抗掷骰失败；不落地，仍走后置。</summary>
        Resisted = 4,
    }

    public class AddStatusActionAbility : Entity, IActionAbility
    {
        public ICombatUnit OwnerEntity => GetParent<Entity>() as ICombatUnit;
        public bool Enable { get; set; }

        public bool TryMakeAction(out AddStatusAction action)
        {
            if (!Enable)
            {
                action = null;
                return false;
            }

            action = (AddStatusAction)CombatContext.Instance.AddAction<AddStatusAction>();
            action.Creator = OwnerEntity;
            return true;
        }
    }

    /// <summary>
    /// 施加 Buff 行动：填单 → PreGive/PreReceive → 免疫裁决 → 落地或入队。
    /// Combat 源全程效果锁：裁决后的新建在锁内入队，解锁才 Attach，避免免疫被 DelayAdd 绕过。
    /// </summary>
    public class AddStatusAction : Entity, IActionExecute, ICombatAddStatusContext
    {
        public TriggerContext TriggerContext { get; set; }
        /// <summary>释放者。</summary>
        public ICombatUnit Creator { get; set; }
        /// <summary>目标实体。</summary>
        public ICombatUnit Target { get; set; }

        /// <inheritdoc />
        public ICombatUnit Caster => Creator;

        /// <summary>本次要挂的 BuffId。</summary>
        public int BuffId { get; set; }

        /// <summary>请求原值。</summary>
        public int RequestedBuffId { get; private set; }

        /// <summary>施加来源。默认 Combat。</summary>
        public AddStatusSource Source { get; set; }

        /// <summary>裁决 Flags。</summary>
        public AddStatusActionEffect Effect { get; set; }

        /// <summary>本次累加的抵抗百分比（0–100）。未扫描或免疫时为 0。</summary>
        public float ResistPercent { get; set; }

        /// <summary>落地时写入 Buff 的 KV 参数。</summary>
        public List<string> ParamString1 { get; set; }

        public void FinishAction() => Entity.Destroy(this);

        /// <summary>
        /// 结算本张上 Buff 单。Combat 源顺序：
        /// 1. 填 BuffId / RequestedBuffId；
        /// 2. 施加者 PreGiveStatus，承受者 PreReceiveStatus（TriggerBuff 可改 BuffId / Effect）；
        /// 3. <see cref="StatusApplyResolver"/>：死亡 Interrupt；新建扫描免疫，再按抵抗%掷骰；
        /// 4. Interrupt 不落地不后置；Immunity / Resisted 不落地仍后置；
        /// 5. RequestAddStatus：锁内新建入队（带已裁决 BuffId），已有则立即刷新。
        /// 非 Combat 源跳过 2–3，直接落地。
        /// </summary>
        public void ApplyAddStatusBySetting(int statusId, List<string> paramString1)
        {
            BuffId = statusId;
            RequestedBuffId = statusId;
            ParamString1 = paramString1;
            Effect = AddStatusActionEffect.None;
            ResistPercent = 0f;

            if (statusId <= 0 || Target?.Status == null)
            {
                FinishAction();
                return;
            }

            if (Source != AddStatusSource.Combat)
            {
                CommitWithoutPipeline();
                return;
            }

            using (CombatBuffPipeline.Lock(Creator, Target))
            {
                CombatBuffPipeline.Notify(Creator, ActionPointType.PreGiveStatus, this);
                CombatBuffPipeline.Notify(Target, ActionPointType.PreReceiveStatus, this);

                StatusApplyResolver.Resolve(this);

                if (Effect.HasFlag(AddStatusActionEffect.Interrupt) || BuffId <= 0)
                {
                    FinishAction();
                    return;
                }

                if (!ShouldApplyStatus())
                {
                    PostProcess();
                    FinishAction();
                    return;
                }

                BuffAddRequestResult result = Target.Status.RequestAddStatus(BuffId, Creator, ParamString1);
                if (result != BuffAddRequestResult.Queued)
                    PostProcess();

                FinishAction();
            }
        }

        void CommitWithoutPipeline()
        {
            BuffAddRequestResult result = Target.Status.RequestAddStatus(BuffId, Creator, ParamString1);
            if (result != BuffAddRequestResult.Queued)
                PostProcess();
            FinishAction();
        }

        void PostProcess()
        {
            CombatBuffPipeline.Notify(Creator, ActionPointType.PostGiveStatus, this);
            CombatBuffPipeline.Notify(Target, ActionPointType.PostReceiveStatus, this);
        }

        bool ShouldApplyStatus()
        {
            return !Effect.HasFlag(AddStatusActionEffect.Interrupt)
                && !Effect.HasFlag(AddStatusActionEffect.Immunity)
                && !Effect.HasFlag(AddStatusActionEffect.Resisted);
        }

        public override void OnReset()
        {
            TriggerContext = default;
            Creator = null;
            Target = null;
            BuffId = 0;
            RequestedBuffId = 0;
            Source = AddStatusSource.Combat;
            Effect = AddStatusActionEffect.None;
            ParamString1 = null;
            ResistPercent = 0f;
        }
    }
}
