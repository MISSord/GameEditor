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
        /// <summary>SkillTimeStop 期间把该单位改走玩家钟；效果移除时释放。</summary>
        public ICombatUnit ClockHoldUnit;
        /// <summary>HitStop 攻击者（与 <see cref="Target"/> 受击者一起吃实体顿帧）。</summary>
        public ICombatUnit HitStopAttacker;
        /// <summary>HitStop 时是否联动镜头 CA/RadialBlur。</summary>
        public bool PlayCameraImpact;
        /// <summary>DeathDissolve 等异步效果完成回调。</summary>
        public Action OnComplete;
        public bool RespectGraphicsGate;

        public static CombatFxSpec SkillTimeStop(CombatFxSource source, float durationSeconds, ICombatUnit clockHoldUnit = null)
        {
            return new CombatFxSpec
            {
                Kind = CombatFxKind.SkillTimeStop,
                Source = source,
                Target = clockHoldUnit,
                Duration = durationSeconds,
                PlayerScale = 1f,
                CameraScale = 1f,
                TimePriority = 20,
                ClockHoldUnit = clockHoldUnit,
                RespectGraphicsGate = true,
            };
        }

        public static CombatFxSpec HitStop(
            CombatFxSource source,
            float durationSeconds,
            float entityScale = 0.1f,
            bool cameraImpact = true,
            ICombatUnit attacker = null,
            ICombatUnit defender = null,
            int timePriority = 10)
        {
            return new CombatFxSpec
            {
                Kind = CombatFxKind.HitStop,
                Source = source,
                Target = defender,
                HitStopAttacker = attacker,
                Duration = durationSeconds,
                WorldScale = entityScale,
                PlayerScale = 1f,
                CameraScale = 1f,
                TimePriority = timePriority,
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

        /// <summary>闪避残影。</summary>
        public static CombatFxSpec Afterimage(CombatFxSource source, ICombatUnit owner)
        {
            return new CombatFxSpec
            {
                Kind = CombatFxKind.Afterimage,
                Source = source,
                Target = owner,
                RespectGraphicsGate = true,
            };
        }

        /// <summary>Perfect Dodge 灰屏。</summary>
        public static CombatFxSpec ScreenDesaturate(CombatFxSource source, float durationSeconds = 0.5f)
        {
            return new CombatFxSpec
            {
                Kind = CombatFxKind.ScreenDesaturate,
                Source = source,
                Duration = durationSeconds,
                RespectGraphicsGate = true,
            };
        }
    }
}
