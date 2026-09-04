using EGamePlay.Combat;
using System.Collections.Generic;

namespace Flux
{
    [FEvent("GamoObject/TriggerRangeEvent", typeof(FTriggerRangeTrack))]
    public class FTriggerRangeEvent : FEvent
    {
        public CubeRange cubeRange;
        /// <summary>该触发对应技能的第几段伤害（从 1 开始）。</summary>
        [UnityEngine.Tooltip("段号从 1 起，须在 SkillDamage 表有 (本技能, 段号) 行")]
        [UnityEngine.Min(1)]
        public int DamageSegmentIndex = 1;
        /// <summary>同一击多盒共用的组。0 表示按该触发事件实例去重。</summary>
        [UnityEngine.Tooltip("同一击多个判定盒填相同正整数；0=按本事件实例去重")]
        public int HitGroupId = 0;
        /// <summary>命中额外效果 ID；为空时只结算该段伤害。</summary>
        [UnityEngine.Tooltip("空=只出该段伤害；非空=再执行技能定义里点名的额外效果")]
        public List<int> EffectIds = new List<int>();
    }
}
