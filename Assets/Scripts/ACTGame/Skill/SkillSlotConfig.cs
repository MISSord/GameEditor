using System;
using System.Collections.Generic;
using EGamePlay.Combat;
using UnityEngine;

namespace ACTGameEditor
{
    /// <summary>
    /// 槽位配置：定义每个槽位的输入绑定、默认技能、释放条件等。
    /// 输入层与技能层分离，支持多套键位方案。
    /// </summary>
    [CreateAssetMenu(fileName = "SkillSlotConfig", menuName = "ACTGame/SkillSlotConfig")]
    public class SkillSlotConfig : ScriptableObject
    {
        /// <summary>
        /// 槽位条目：仅负责槽位身份、输入绑定、默认技能、优先级、预输入时长。
        /// 释放条件（RequireRollTag、RequiredTags、BlockedTags）由技能配置（AbilityConfigObject）提供。
        /// </summary>
        [Serializable]
        public class SlotEntry
        {
            [Header("槽位身份")]
            [Tooltip("槽位 ID")]
            public SkillSlotId SlotId;
            [Tooltip("默认技能 ID（角色未覆盖时使用）")]
            public int DefaultSkillId;
            [Header("释放优先级")]
            [Tooltip("基础分类（普攻/武器技能/闪避/大招等）")]
            public SkillSort SortBase;
            [Tooltip("偏移量，同一分类下的多槽位")]
            public int SortOffset;

            /// <summary>释放优先级数值，多槽位同时输入时取最高。= (int)SortBase + SortOffset</summary>
            public int Sort => (int)SortBase + SortOffset;

            [Header("输入绑定")]
            [Tooltip("触发输入")]
            public InputListernType InputType;
            [Tooltip("按下类型（点按/长按）")]
            public PressType PressType;
            [Tooltip("回调类型")]
            public InputCallBackType InputCallBackType = InputCallBackType.Performed;
            [Tooltip("预输入有效时长（秒），0=使用玩家 InputTimeout")]
            [Range(0f, 2f)]
            public float InputTimeout;

            [Header("技能链（工具自动生成）")]
            [Tooltip("从 DefaultSkillId 沿 SkillInputEvents 遍历得到的技能链，用于 AttachAbility")]
            public List<int> ComboSkillIds = new List<int>();
        }

        [Tooltip("按优先级从高到低，先匹配到的先释放")]
        public List<SlotEntry> Slots = new List<SlotEntry>();

        /// <summary>根据输入查找槽位条目，返回最先匹配的。</summary>
        public SlotEntry FindByInput(InputListernType input, PressType press, InputCallBackType cb)
        {
            for (int i = 0; i < Slots.Count; i++)
            {
                var s = Slots[i];
                if (s.InputType == input && s.PressType == press && s.InputCallBackType == cb)
                    return s;
            }
            return null;
        }

        /// <summary>根据槽位 ID 查找条目。</summary>
        public SlotEntry FindBySlot(SkillSlotId slotId)
        {
            for (int i = 0; i < Slots.Count; i++)
            {
                if (Slots[i].SlotId == slotId) return Slots[i];
            }
            return null;
        }
    }
}
