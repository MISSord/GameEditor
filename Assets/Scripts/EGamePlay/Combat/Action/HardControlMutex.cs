using System.Collections.Generic;
using EGamePlay;

namespace EGamePlay.Combat
{
    /// <summary>
    /// 硬控互斥：问 <see cref="CombatTags.BuffMoveForbid"/>，不问技能名。
    /// 新硬控 Priority 不低于已有则卸旧（<see cref="BuffRemoveReason.Replaced"/>）；更低则本次不落地。
    /// 沉默只有 SkillForbid，不进本组。
    /// </summary>
    public static class HardControlMutex
    {
        const int ReplaceCap = 8;
        static readonly int[] ReplaceIds = new int[ReplaceCap];

        /// <summary>
        /// 为即将落地的 Buff 腾出硬控槽。同 Id 刷新不要调。
        /// </summary>
        /// <returns>false = 已有更高 Priority 硬控，调用方不要挂新 Buff。</returns>
        public static bool Admit(StatusComponent status, int incomingBuffId)
        {
            if (status == null || incomingBuffId <= 0)
                return true;

            BuffDemoSetting incoming = SkillSettingMgr.Instance != null
                ? SkillSettingMgr.Instance.GetBuffDemoSetting(incomingBuffId)
                : null;
            if (!IsHardControl(incoming))
                return true;

            List<Buff> list = status.Statuses;
            if (list == null || list.Count == 0)
                return true;

            int incomingPrio = incoming.Priority;
            int strongest = int.MinValue;
            int replaceCount = 0;
            for (int i = 0; i < list.Count; i++)
            {
                Buff buff = list[i];
                if (buff == null || buff.IsDisposed || buff.IsRemoving)
                    continue;
                if (buff.BuffID == incomingBuffId)
                    continue;
                if (!IsHardControl(buff.Setting))
                    continue;

                int prio = buff.Setting != null ? buff.Setting.Priority : 0;
                if (prio > strongest)
                    strongest = prio;
                if (replaceCount < ReplaceCap)
                    ReplaceIds[replaceCount++] = buff.BuffID;
            }

            if (replaceCount == 0)
                return true;

            if (strongest > incomingPrio)
                return false;

            for (int i = 0; i < replaceCount; i++)
                status.RemoveStatus(ReplaceIds[i], BuffRemoveReason.Replaced);
            return true;
        }

        /// <summary>
        /// 是否硬控：表 BuffTag 或 PlayerControll 修饰带 <see cref="CombatTags.BuffMoveForbid"/>。
        /// </summary>
        public static bool IsHardControl(BuffDemoSetting setting)
        {
            if (setting == null)
                return false;
            if (ContainsMoveForbid(setting.BuffTag))
                return true;

            List<int> mods = setting.BuffModifyList;
            if (mods == null || mods.Count == 0)
                return false;

            SkillSettingMgr mgr = SkillSettingMgr.Instance;
            if (mgr == null)
                return false;

            for (int i = 0; i < mods.Count; i++)
            {
                BuffModifySetting mod = mgr.GetBuffModifySettingOrNull(mods[i]);
                if (mod == null || mod.EffectModifyType != EffectModifyType.PlayerControll)
                    continue;
                if (ContainsMoveForbid(mod.ParamString1))
                    return true;
            }

            return false;
        }

        static bool ContainsMoveForbid(List<string> tags)
        {
            if (tags == null || tags.Count == 0)
                return false;
            for (int i = 0; i < tags.Count; i++)
            {
                if (tags[i] == CombatTags.BuffMoveForbid)
                    return true;
            }

            return false;
        }
    }
}
