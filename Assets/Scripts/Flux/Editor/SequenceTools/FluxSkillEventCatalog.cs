using Flux;
using System;
using System.Collections.Generic;

namespace FluxEditor
{
    /// <summary>
    /// 技能轴允许新增的事件类型，与 <see cref="SaveSequenceTrackExporter"/> 的映射一致。
    /// UnUse / 灯光 / 相机等不进菜单，避免加轨后保存失败。
    /// </summary>
    public static class FluxSkillEventCatalog
    {
        static readonly HashSet<Type> Allowed = new HashSet<Type>
        {
            typeof(FPlayAnimationEvent),
            typeof(FTweenPositionEvent),
            typeof(FTweenRotationEvent),
            typeof(FTweenScaleEvent),
            typeof(FPlayParticleEvent),
            typeof(FObjectEvent),
            typeof(FSwitchEvent),
            typeof(FPlayMsgEvent),
            typeof(FTriggerRangeEvent),
            typeof(FSkillInputEvent),
            typeof(FPlayTagEvent),
        };

        /// <summary>该事件类型可否从 Timeline 加轨菜单加入。</summary>
        public static bool CanAdd(Type eventType)
        {
            return eventType != null && Allowed.Contains(eventType);
        }
    }
}
