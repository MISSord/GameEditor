using System;
using UnityEngine;
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
        /// <summary>镜头震动参数（CombatFxKind.CameraShake 专用）。</summary>
        public CameraShakeProfile ShakeProfile;
        /// <summary>方向性 Kick 世界方向（攻击者→受击者，水平投影；零向量 = 无 Kick）。</summary>
        public Vector3 KickDirectionWorld;
        /// <summary>方向性 Kick 位移幅度（米）。</summary>
        public float KickAmplitude;
        /// <summary>方向性 Kick 回落时长（unscaled 秒）。</summary>
        public float KickDuration;
        /// <summary>TelegraphFlash 专用：<see cref="TelegraphKind"/> 字节。</summary>
        public byte TelegraphKind;
        /// <summary>TelegraphFlash 专用：头顶高度偏移（米），0 用默认。</summary>
        public float TelegraphHeight;

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

        /// <summary>Trauma 震屏 + FOV 冲击（镜头域；轻重段分级由 Package 决定）。</summary>
        public static CombatFxSpec CameraShake(CombatFxSource source, CameraShakeProfile profile)
        {
            return new CombatFxSpec
            {
                Kind = CombatFxKind.CameraShake,
                Source = source,
                ShakeProfile = profile,
                RespectGraphicsGate = true,
            };
        }

        /// <summary>敌人出手预警闪光（头顶十字）。玩法可读性，不走画质门控。</summary>
        public static CombatFxSpec Telegraph(CombatFxSource source, ICombatUnit target, byte telegraphKind, float durationSeconds, float height)
        {
            return new CombatFxSpec
            {
                Kind = CombatFxKind.TelegraphFlash,
                Source = source,
                Target = target,
                Duration = durationSeconds,
                TelegraphKind = telegraphKind,
                TelegraphHeight = height,
                RespectGraphicsGate = false,
            };
        }
    }
}
