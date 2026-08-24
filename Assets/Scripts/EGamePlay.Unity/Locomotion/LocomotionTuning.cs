using UnityEngine;

namespace EGamePlay.Unity.Locomotion
{
    /// <summary>
    /// 角色移动调参。默认由 <see cref="LocomotionTuning.CreateDefault"/> 填充；
    /// 业务层可通过 ACTGameEditor.Locomotion.LocomotionTuningBuilder 从 SO 映射。
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
    }
}
