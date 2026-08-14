using System.Collections.Generic;

namespace Flux
{
    /// <summary>玩家标签加入事件。</summary>
    [FEvent("Sequence/FPlayTagEvent", typeof(FInputTrack))]
    public class FPlayTagEvent : FEvent
    {
        public List<string> SkillTagList = new List<string>();
        /// <summary>只触发一次的效果 ID；为空时触发全部。</summary>
        public List<int> NormalEffectIds = new List<int>();
        /// <summary>跟随轨道生命周期的效果 ID；为空时触发全部。</summary>
        public List<int> SkillEffectIds = new List<int>();

        public override string Text => "Tag";
    }
}
