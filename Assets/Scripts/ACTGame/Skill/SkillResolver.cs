using EGamePlay.Combat;

namespace ACTGameEditor
{
    /// <summary>
    /// Idle 槽位 → 技能。形态 × 地面/空中优先，否则角色槽位表。
    /// </summary>
    public static class SkillResolver
    {
        /// <summary>
        /// 解析 Idle 时该槽位应对应的技能 ID。无法解析时返回 0。
        /// </summary>
        public static int ResolveIdle(CombatEntity actor, SkillSlotRuntime slots, SkillSlotId slotId)
        {
            if (actor != null && !actor.IsDisposed)
            {
                CombatFormComponent form = actor.FormComponent;
                SkillFormConfig config = form?.ActiveForm;
                if (config != null)
                {
                    int formSkillId = config.GetSkillId(slotId, actor.IsAirborne);
                    if (formSkillId > 0)
                        return formSkillId;
                }
            }

            return slots != null ? slots.GetSkillId(slotId) : 0;
        }
    }
}
