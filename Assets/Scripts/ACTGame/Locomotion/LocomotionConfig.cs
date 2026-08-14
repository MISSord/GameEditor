using UnityEngine;
using XiaoCao;

namespace ACTGameEditor.Locomotion
{
    /// <summary>
    /// 角色移动调参。可由 <see cref="LocomotionConfig"/> 或旧版 <see cref="PlayerMoveSettingSo"/> 填充。
    /// </summary>
    public struct LocomotionTuning
    {
        public LayerMask GroundLayers;
        public float MovingTurnSpeed;
        public float RunMoveSpeed;
        public float Gravity;
        public float GravityOnGroundRate;
        public float GravityOnAirAddRate;
        public float GravityMaxRate;
        public float Acceleration;
        public float Deceleration;
        public float AnimSpeedAcceleration;
        public float MinimumStepTime;
        public float GroundedOffset;
        public float GroundedRadius;
        public float InputDeadZone;

        /// <summary>与旧 InputMoveComponent 硬编码行为对齐的默认值。</summary>
        public static LocomotionTuning CreateDefault()
        {
            return new LocomotionTuning
            {
                GroundLayers = 1 << 9,
                MovingTurnSpeed = 720f,
                RunMoveSpeed = 5f,
                Gravity = -9.8f,
                GravityOnGroundRate = 0.8f,
                GravityOnAirAddRate = 1f,
                GravityMaxRate = 4f,
                Acceleration = 0.25f,
                Deceleration = 0.05f,
                AnimSpeedAcceleration = 0.2f,
                MinimumStepTime = 0.45f,
                GroundedOffset = -0.14f,
                GroundedRadius = 0.28f,
                InputDeadZone = 0.1f,
            };
        }

        /// <summary>
        /// 从旧 SO 填充；SO 缺失的跑速/加速度等保留默认硬编码值，转向改用 SO（真正生效）。
        /// </summary>
        public static LocomotionTuning FromPlayerMoveSetting(PlayerMoveSettingSo so)
        {
            LocomotionTuning t = CreateDefault();
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
                return CreateDefault();

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
