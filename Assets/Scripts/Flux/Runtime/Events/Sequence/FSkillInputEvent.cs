using EGamePlay.Combat;
using System.Collections.Generic;

namespace Flux
{
    //输入信息
    [System.Serializable]
    public class SkillInputData
    {
        //监听操作类型
        public InputListernType ListernType;
        public PressType PressType;
        public InputCallBackType InputCallBackType;
        //技能ID
        public int SkillId;
        //技能优先级
        public SkillSort SkillSort;
        //偏移
        public int Offset = 0;

        public List<string> RequiredTags;
        public List<string> BlockedTags;
        /// <summary>窗边预输入寿命（秒）。≤0 时用 NormalActPlayer.ComboBufferTimeout。</summary>
        public float InputTimeout;

    }

    //玩家输入监听
    [FEvent("Sequence/FSkillInputEvent", typeof(FInputTrack))]
    public class FSkillInputEvent : FEvent
    {
        public List<SkillInputData> InputList;
    }
}
