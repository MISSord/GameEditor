using EGamePlay;
using EGamePlay.Unity;

namespace ACTGameEditor.Locomotion
{
    /// <summary>从业务配置映射到 <see cref="LocomotionTuning"/>。</summary>
    public static class LocomotionTuningBuilder
    {
        /// <summary>
        /// 从 PlayerMoveSettingSo 填充。转向、走跑、加减速、最短迈步均走 SO，不再只靠 CreateDefault。
        /// </summary>
        public static LocomotionTuning FromPlayerMoveSetting(PlayerMoveSettingSo so)
        {
            LocomotionTuning t = LocomotionTuning.CreateDefault();
            if (so == null)
                return t;

            t.GroundLayers = so.GroundLayers;
            t.MovingTurnSpeed = so.m_MovingTurnSpeed;
            t.RunMoveSpeed = so.RunMoveSpeed > 0.01f ? so.RunMoveSpeed : t.RunMoveSpeed;
            t.WalkMoveSpeed = so.NorMoveSpeed >= 0.4f ? so.NorMoveSpeed : t.RunMoveSpeed * 0.5f;
            t.SprintMoveSpeed = so.SprintMoveSpeed > t.RunMoveSpeed
                ? so.SprintMoveSpeed
                : t.RunMoveSpeed * 1.5f;
            t.Acceleration = so.Acceleration > 0.001f ? so.Acceleration : t.Acceleration;
            t.Deceleration = so.Deceleration > 0.001f ? so.Deceleration : t.Deceleration;
            t.MinimumStepTime = so.MinimumStepTime;
            t.Gravity = so.Gravity;
            t.GravityOnGroundRate = so.GravityOnGrondRate;
            t.GravityOnAirAddRate = so.GravityOnAirAddRate;
            t.GravityMaxRate = so.GravityMaxRate;
            t.JumpHeight = so.JumpHeight;
            t.AirControl = so.AirControl;
            t.AirMoveSpeedScale = so.AirMoveSpeedScale;
            if (so.CoyoteTime > 0.001f)
                t.CoyoteTime = so.CoyoteTime;
            if (so.JumpBufferTime > 0.001f)
                t.JumpBufferTime = so.JumpBufferTime;
            if (so.LandSlowTime > 0.001f)
                t.LandSlowTime = so.LandSlowTime;
            if (so.LandSlowScale > 0.01f)
                t.LandSlowScale = so.LandSlowScale;
            return t;
        }
    }
}
