using EGamePlay.Combat;
using NaughtyAttributes;
using System.Collections.Generic;
using UnityEngine;

namespace Flux
{
    /// <summary>
    /// 连招窗边：只描述「这扇窗听哪个键、接到哪一发」。
    /// 打断档 / 技能 Tag 读目标 <c>SkillDemo</c>，不要在边上重复填。
    /// </summary>
    [System.Serializable]
    public class SkillInputData
    {
        [Tooltip("连招窗监听的战斗键（ButtonX=普攻 …）。")]
        public InputListernType ListernType;
        [Tooltip("点按或长按。与 Idle 的 CharacterSlot.Press 无关。")]
        public PressType PressType;
        [HideInInspector]
        public InputCallBackType InputCallBackType = InputCallBackType.Performed;
        [Tooltip("窗边命中后要放的下一发。连招链不进 Excel。")]
        public int SkillId;
        [HideInInspector]
        public SkillSort SkillSort;
        [HideInInspector]
        public int Offset;
        [HideInInspector]
        public List<string> RequiredTags;
        [HideInInspector]
        public List<string> BlockedTags;
        [Tooltip("窗边预输入寿命（秒）。≤0 用玩家 ComboBufferTimeout。")]
        public float InputTimeout;
    }

    [FEvent("Sequence/FSkillInputEvent", typeof(FInputTrack))]
    public class FSkillInputEvent : FEvent
    {
        [InfoBox("只填键、按法、下一发 SkillId、窗边超时。Skill Sort / Offset / Tag / CallBack 已隐藏：打断档和释放 Tag 读目标技能 SkillDemo。")]
        public List<SkillInputData> InputList;
    }
}
