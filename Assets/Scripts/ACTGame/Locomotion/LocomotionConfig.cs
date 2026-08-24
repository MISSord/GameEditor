using EGamePlay;
using EGamePlay.Unity.Locomotion;
using UnityEngine;

namespace ACTGameEditor.Locomotion
{
    /// <summary>从业务配置映射到 <see cref="LocomotionTuning"/>。</summary>
    public static class LocomotionTuningBuilder
    {
        /// <summary>
        /// 从旧 SO 填充；SO 缺失的跑速/加速度等保留默认硬编码值，转向改用 SO（真正生效）。
        /// </summary>
        public static LocomotionTuning FromPlayerMoveSetting(PlayerMoveSettingSo so)
        {
            LocomotionTuning t = LocomotionTuning.CreateDefault();
            if (so == null)
                return t;

            t.GroundLayers = so.GroundLayers;
            t.MovingTurnSpeed = so.m_MovingTurnSpeed;
            t.Gravity = so.Gravity;
            t.GravityOnGroundRate = so.GravityOnGrondRate;
            t.GravityOnAirAddRate = so.GravityOnAirAddRate;
            t.GravityMaxRate = so.GravityMaxRate;
            return t;
        }

        /// <summary>从独立 LocomotionConfig 填充。</summary>
        public static LocomotionTuning FromConfig(LocomotionConfig config)
        {
            if (config == null)
                return LocomotionTuning.CreateDefault();

            return new LocomotionTuning
            {
                GroundLayers = config.GroundLayers,
                MovingTurnSpeed = config.MovingTurnSpeed,
                RunMoveSpeed = config.RunMoveSpeed,
                Gravity = config.Gravity,
                GravityOnGroundRate = config.GravityOnGroundRate,
                GravityOnAirAddRate = config.GravityOnAirAddRate,
                GravityMaxRate = config.GravityMaxRate,
                Acceleration = config.Acceleration,
                Deceleration = config.Deceleration,
                AnimSpeedAcceleration = config.AnimSpeedAcceleration,
                MinimumStepTime = config.MinimumStepTime,
                GroundedOffset = config.GroundedOffset,
                GroundedRadius = config.GroundedRadius,
                InputDeadZone = config.InputDeadZone,
            };
        }
    }

    /// <summary>独立移动配置（测试场景 / 新关卡使用）。</summary>
    [CreateAssetMenu(fileName = "LocomotionConfig", menuName = "ACTGame/Locomotion Config", order = 50)]
    public sealed class LocomotionConfig : ScriptableObject
    {
        [Header("Ground")]
        public LayerMask GroundLayers = 1 << 9;
        public float GroundedOffset = -0.14f;
        public float GroundedRadius = 0.28f;

        [Header("Move")]
        public float MovingTurnSpeed = 720f;
        public float RunMoveSpeed = 5f;
        public float Acceleration = 0.25f;
        public float Deceleration = 0.05f;
        public float MinimumStepTime = 0.45f;
        public float InputDeadZone = 0.1f;

        [Header("Gravity")]
        public float Gravity = -9.8f;
        public float GravityOnGroundRate = 0.8f;
        public float GravityOnAirAddRate = 1f;
        public float GravityMaxRate = 4f;

        [Header("Animation")]
        public float AnimSpeedAcceleration = 0.2f;
    }
}
