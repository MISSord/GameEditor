using System;
using System.Collections.Generic;
using UnityEngine;

namespace ACTGameEditor
{
    /// <summary>
    /// 战斗形态入口表：Idle 时按地面/空中解析槽位 → 技能。
    /// 不配或未 SetForm 时，Resolver 回退 SkillSlotRuntime。
    /// GrantedTags 必须已在 TagCollection.AllTags 中注册。
    /// </summary>
    [CreateAssetMenu(fileName = "SkillFormConfig", menuName = "ACTGame/SkillFormConfig")]
    public class SkillFormConfig : ScriptableObject
    {
        [Tooltip("形态 ID，须 > 0 且同一角色内唯一")]
        public int FormId;

        [Tooltip("进入此形态时添加、退出时移除")]
        public List<string> GrantedTags = new List<string>();

        [Serializable]
        public class FormSlotEntry
        {
            public SkillSlotId SlotId;
            public int SkillId;
            [Tooltip("该入口沿连招链需要 Attach 的技能 ID")]
            public List<int> ComboSkillIds = new List<int>();
        }

        [Tooltip("地面 Idle 入口")]
        public List<FormSlotEntry> GroundEntries = new List<FormSlotEntry>();

        [Tooltip("空中 Idle 入口；某槽未填则回退地面表")]
        public List<FormSlotEntry> AirEntries = new List<FormSlotEntry>();

        /// <summary>按槽位取 Idle 技能。空中优先 Air，没有则地面。</summary>
        public int GetSkillId(SkillSlotId slotId, bool airborne)
        {
            if (airborne)
            {
                int airId = FindSkillId(AirEntries, slotId);
                if (airId > 0)
                    return airId;
            }

            return FindSkillId(GroundEntries, slotId);
        }

        /// <summary>收集此形态全部入口及连招链技能 ID，供 AttachAbility。</summary>
        public void CollectSkillIds(HashSet<int> outIds)
        {
            if (outIds == null)
                return;
            CollectFrom(GroundEntries, outIds);
            CollectFrom(AirEntries, outIds);
        }

        private static int FindSkillId(List<FormSlotEntry> entries, SkillSlotId slotId)
        {
            if (entries == null)
                return 0;
            for (int i = 0; i < entries.Count; i++)
            {
                FormSlotEntry e = entries[i];
                if (e != null && e.SlotId == slotId && e.SkillId > 0)
                    return e.SkillId;
            }

            return 0;
        }

        private static void CollectFrom(List<FormSlotEntry> entries, HashSet<int> outIds)
        {
            if (entries == null)
                return;
            for (int i = 0; i < entries.Count; i++)
            {
                FormSlotEntry e = entries[i];
                if (e == null)
                    continue;
                if (e.SkillId > 0)
                    outIds.Add(e.SkillId);
                if (e.ComboSkillIds == null)
                    continue;
                for (int c = 0; c < e.ComboSkillIds.Count; c++)
                {
                    if (e.ComboSkillIds[c] > 0)
                        outIds.Add(e.ComboSkillIds[c]);
                }
            }
        }
    }
}
