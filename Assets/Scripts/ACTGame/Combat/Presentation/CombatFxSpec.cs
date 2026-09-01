using System;
using EGamePlay.Combat;

namespace ACTGameEditor.Combat
{
    /// <summary>单次表现播放请求。</summary>
    public struct CombatFxSpec
    {
        public CombatFxKind Kind;
        public CombatFxSource Source;
        public ICombatUnit Target;
        public float Duration;
        public float WorldScale;
        public float PlayerScale;
        public float CameraScale;
        public int TimePriority;
        /// <summary>HitStop 时是否联动镜头 CA/RadialBlur。</summary>
        public bool PlayCameraImpact;
        /// <summary>DeathDissolve 等异步效果完成回调。</summary>
        public Action OnComplete;
        public bool RespectGraphicsGate;

        public static CombatFxSpec SkillTimeStop(CombatFxSource source, float durationSeconds)
        {
            return new CombatFxSpec
            {
                Kind = CombatFxKind.SkillTimeStop,
                Source = source,
                Duration = durationSeconds,
                PlayerScale = 1f,
                CameraScale = 1f,
                TimePriority = 20,
                RespectGraphicsGate = true,
            };
        }

        public static CombatFxSpec HitStop(CombatFxSource source, float durationSeconds, float worldScale = 0.1f, bool cameraImpact = true)
        {
            return new CombatFxSpec
            {
                Kind = CombatFxKind.HitStop,
                Source = source,
                Duration = durationSeconds,
                WorldScale = worldScale,
                PlayerScale = 1f,
                CameraScale = 1f,
                PlayCameraImpact = cameraImpact,
                RespectGraphicsGate = true,
            };
        }

        public static CombatFxSpec HitFlash(CombatFxSource source, ICombatUnit target, float durationSeconds = 0.12f)
        {
            return new CombatFxSpec
            {
                Kind = CombatFxKind.HitFlash,
                Source = source,
                Target = target,
                Duration = durationSeconds,
                RespectGraphicsGate = true,
            };
        }

        public static CombatFxSpec TimeFracture(CombatFxSource source, float durationSeconds, float worldScale = 0.3f)
        {
            return new CombatFxSpec
            {
                Kind = CombatFxKind.TimeFracture,
                Source = source,
                Duration = durationSeconds,
                WorldScale = worldScale,
                PlayerScale = 1f,
                CameraScale = 1f,
                TimePriority = 50,
                RespectGraphicsGate = true,
            };
        }
    }
}
