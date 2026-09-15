using System;
using System.Collections.Generic;
using EGamePlay.Combat;
using UnityEngine;

namespace ACTGameEditor
{
    /// <summary>槽位配置：只绑 4 战斗键的输入。技能 Id 来自 CharacterSlot 表。</summary>
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
            [Tooltip("已废弃：技能 Id 只来自 CharacterSlot 表，保持 0。")]
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
            [Tooltip("已废弃：连招链运行时从轴收集，不必填。")]
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

        /// <summary>按硬件键找绑定行。优先 Click 行，用作槽位身份；实际按法写在 InputBuffer。</summary>
        public SlotEntry FindBinding(InputListernType input, InputCallBackType cb)
        {
            SlotEntry fallback = null;
            for (int i = 0; i < Slots.Count; i++)
            {
                SlotEntry s = Slots[i];
                if (s == null || s.InputType != input || s.InputCallBackType != cb)
                    continue;
                if (s.PressType == PressType.Click)
                    return s;
                if (fallback == null)
                    fallback = s;
            }
            return fallback;
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
