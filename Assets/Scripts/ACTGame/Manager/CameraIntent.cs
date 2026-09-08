using UnityEngine;

namespace ACTGameEditor
{
    /// <summary>相机意图来源。</summary>
    public enum CameraIntentSource : byte
    {
        None = 0,
        /// <summary>技能时间轴 / 通用镜头请求（RequestDistance 也走这里）。</summary>
        SkillCamera = 1,
        /// <summary>过场演出（预留，优先级应高于战斗）。</summary>
        Cutscene = 2,
        /// <summary>调试入口（F7 等）。</summary>
        Debug = 3,
    }

    /// <summary>
    /// 相机意图：一个来源对镜头"想要的状态 + 话语权"。
    /// 每帧由 CameraManager 仲裁：同来源单槽（低优先级不覆盖），最高优先级角度意图接管朝向，
    /// 距离意图覆盖滚轮目标；接管与归还均按 BlendTime 平滑。
    /// 意图自带时长，过期自动归还（打断不会忘了还镜头），不持有跨帧可变状态。
    /// </summary>
    public readonly struct CameraIntent
    {
        public readonly CameraIntentSource Source;
        /// <summary>优先级；同源高者覆盖低者（语义同 TimeScaleEffectManager）。</summary>
        public readonly int Priority;
        /// <summary>接管/归还平滑时间（秒）；&lt;=0 用 CameraManager.IntentBlendTime。</summary>
        public readonly float BlendTime;
        /// <summary>存活时长（unscaled 秒，暂停冻结）；&lt;=0 常驻直到 CancelIntent。</summary>
        public readonly float Duration;
        /// <summary>看向的世界坐标；null = 不接管角度。</summary>
        public readonly Vector3? LookPoint;
        /// <summary>目标距离（米）；&lt;=0 = 不接管距离。</summary>
        public readonly float Distance;

        public CameraIntent(CameraIntentSource source,
            Vector3? lookPoint = null,
            float distance = 0f,
            int priority = 10,
            float blendTime = -1f,
            float duration = 0f)
        {
            Source = source;
            Priority = priority;
            BlendTime = blendTime;
            Duration = duration;
            LookPoint = lookPoint;
            Distance = distance;
        }
    }
}
