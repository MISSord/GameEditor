using EGamePlay.Combat;
using System.Collections.Generic;

namespace Flux
{
    [FEvent("GamoObject/TriggerRangeEvent", typeof(FTriggerRangeTrack))]
    public class FTriggerRangeEvent : FEvent
    {
        public CubeRange cubeRange;
        /// <summary>该触发对应技能的第几段伤害（从 1 开始），0 表示未指定。</summary>
        public int DamageSegmentIndex = 0;
        /// <summary>同一击多盒共用的组。0 表示按该触发事件实例去重。</summary>
        [UnityEngine.Tooltip("同一击多个判定盒填相同正整数；0=按本事件实例去重")]
        public int HitGroupId = 0;
        /// <summary>触发时执行的效果 ID；为空时触发该技能下全部效果。</summary>
        public List<int> EffectIds = new List<int>();
    }
}
