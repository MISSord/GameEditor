using EGamePlay;

namespace EGamePlay.Combat
{
    /// <summary>
    /// 上 Buff 系统裁决：排在 PreGive / PreReceive Dispatch 之后，对齐伤害侧的闪避规则。
    /// 死亡 Interrupt；新建扫描 StatusApplyModify：免疫（行为 0）优先于抵抗%（行为 1）。改写 Id 未落地。
    /// </summary>
    public static class StatusApplyResolver
    {
        /// <summary>
        /// 按当前单上的 BuffId 裁决。已有同 Id（刷新）不扫免疫/抵抗。
        /// TriggerBuff 若已写入 Interrupt / Immunity / Resisted，则尊重并不覆盖。
        /// </summary>
        public static void Resolve(AddStatusAction action)
        {
            if (action == null)
                return;
            if (action.Effect.HasFlag(AddStatusActionEffect.Interrupt))
                return;

            ICombatUnit target = action.Target;
            if (target == null || target.IsDisposed || target.IsDead)
            {
                action.Effect |= AddStatusActionEffect.Interrupt;
                return;
            }

            if (action.Effect.HasFlag(AddStatusActionEffect.Immunity)
                || action.Effect.HasFlag(AddStatusActionEffect.Resisted))
                return;

            StatusComponent status = target.Status;
            if (status == null)
            {
                action.Effect |= AddStatusActionEffect.Interrupt;
                return;
            }

            // 已有同 Buff：刷新/叠层，不掷免疫/抵抗。
            if (status.HasBuffId(action.BuffId))
                return;

            BuffDemoSetting setting = SkillSettingMgr.Instance != null
                ? SkillSettingMgr.Instance.GetBuffDemoSetting(action.BuffId)
                : null;
            int bigBuffType = setting != null ? setting.BigBuffType : 0;

            BuffModifyProcessorTable.CollectStatusApplyScan(
                status, action.BuffId, bigBuffType, out bool immunity, out float resistPercent);

            if (immunity)
            {
                action.Effect |= AddStatusActionEffect.Immunity;
                return;
            }

            if (resistPercent <= 0f)
                return;

            if (resistPercent > 100f)
                resistPercent = 100f;

            action.ResistPercent = resistPercent;
            if (resistPercent >= 100f || RandomHelper.RandomRate() <= (int)resistPercent)
                action.Effect |= AddStatusActionEffect.Resisted;
        }
    }
}
