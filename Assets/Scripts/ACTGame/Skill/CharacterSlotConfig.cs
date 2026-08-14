using System;
using System.Collections.Generic;
using UnityEngine;

namespace ACTGameEditor
{
    /// <summary>
    /// 角色/武器槽位覆盖：SlotId → SkillId。
    /// 换角色/武器时加载此配置，覆盖 SkillSlotConfig 的默认技能。
    /// </summary>
    [CreateAssetMenu(fileName = "CharacterSlotConfig", menuName = "ACTGame/CharacterSlotConfig")]
    public class CharacterSlotConfig : ScriptableObject
    {
        [Serializable]
        public class SlotOverride
        {
            public SkillSlotId SlotId;
            public int SkillId;
            /// <summary>技能链（工具自动生成），用于 AttachAbility。</summary>
            public List<int> ComboSkillIds = new List<int>();
        }

        [Tooltip("槽位 → 技能覆盖")]
        public List<SlotOverride> Overrides = new List<SlotOverride>();

        /// <summary>获取指定槽位的技能 ID，未配置则返回 0。</summary>
        public int GetSkillId(SkillSlotId slotId)
        {
            for (int i = 0; i < Overrides.Count; i++)
            {
                if (Overrides[i].SlotId == slotId) return Overrides[i].SkillId;
            }
            return 0;
        }

        /// <summary>获取指定槽位的技能链，未配置或为空则返回 null。</summary>
        public List<int> GetComboSkillIds(SkillSlotId slotId)
        {
            for (int i = 0; i < Overrides.Count; i++)
            {
                if (Overrides[i].SlotId == slotId && Overrides[i].ComboSkillIds != null && Overrides[i].ComboSkillIds.Count > 0)
                    return Overrides[i].ComboSkillIds;
            }
            return null;
        }
    }
}
