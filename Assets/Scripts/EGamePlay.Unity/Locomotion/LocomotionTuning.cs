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
        /// <summary>走路速度（米/秒）。Ctrl 切换。</summary>
        public float WalkMoveSpeed;
        /// <summary>快跑速度（米/秒）。慢跑中点 Shift 切入。</summary>
        public float SprintMoveSpeed;
        /// <summary>已废弃：走跑不再用摇杆阈值。</summary>
        public float WalkStickThreshold;
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

        /// <summary>起跳高度（米）。由 v = sqrt(2·|g|·h) 换算初速度。</summary>
        public float JumpHeight;

        /// <summary>空中方向控制权重（0~1）。鸣潮/绝区零类：保留起跳惯性 + 较低加减速。</summary>
        public float AirControl;

        /// <summary>空中最高水平速相对地面的倍率（1=与跑步相同）。</summary>
        public float AirMoveSpeedScale;

        /// <summary>离地后仍允许起跳的窗口（秒）。鸣潮/ACT 约 0.1。</summary>
        public float CoyoteTime;

        /// <summary>落地前提前按跳，落地后补跳的窗口（秒）。</summary>
        public float JumpBufferTime;

        /// <summary>落地后水平移速打折的时长（秒）。</summary>
        public float LandSlowTime;

        /// <summary>落地顿时的水平移速倍率（0~1）。</summary>
        public float LandSlowScale;

        /// <summary>与旧 InputMoveComponent 硬编码行为对齐的默认值。</summary>
        public static LocomotionTuning CreateDefault()
        {
            return new LocomotionTuning
            {
                GroundLayers = 1 << 9,
                MovingTurnSpeed = 720f,
                RunMoveSpeed = 5f,
                WalkMoveSpeed = 2.5f,
                SprintMoveSpeed = 7.5f,
                WalkStickThreshold = 0.55f,
                Gravity = -9.8f,
                GravityOnGroundRate = 0.8f,
                GravityOnAirAddRate = 1f,
                GravityMaxRate = 4f,
                Acceleration = 0.08f,
                Deceleration = 0.05f,
                AnimSpeedAcceleration = 0.08f,
                MinimumStepTime = 0.08f,
                GroundedOffset = -0.14f,
                GroundedRadius = 0.28f,
                InputDeadZone = 0.1f,
                JumpHeight = 1.2f,
                AirControl = 0.65f,
                AirMoveSpeedScale = 1f,
                CoyoteTime = 0.1f,
                JumpBufferTime = 0.12f,
                LandSlowTime = 0.1f,
                LandSlowScale = 0.55f,
            };
        }
    }
}
