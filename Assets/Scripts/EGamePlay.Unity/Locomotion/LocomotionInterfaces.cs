using ACTGameEditor.Combat;
using EGamePlay;
using EGamePlay.Combat;
using UnityEngine;

namespace EGamePlay.Unity
{
    /// <summary>移动输入源。</summary>
    public interface IMoveInputProvider
    {
        /// <summary>平面移动轴（x=右, y=前），通常为 -1~1。</summary>
        Vector2 MoveAxis { get; }

        /// <summary>Ctrl：切换走路 / 慢跑。</summary>
        bool WalkTogglePressed { get; }

        /// <summary>Shift：慢跑中点按切入快跑。</summary>
        bool SprintPressed { get; }
    }

    /// <summary>用于计算相机相对移动方向。</summary>
    public interface IMoveCameraProvider
    {
        /// <summary>相机在 XZ 平面的前方向（已归一化，Y=0）。</summary>
        Vector3 PlanarForward { get; }

        /// <summary>相机在 XZ 平面的右方向（已归一化，Y=0）。</summary>
        Vector3 PlanarRight { get; }
    }

    /// <summary>可移动门控（战斗禁移、技能覆盖等）。</summary>
    public interface IMoveGate
    {
        /// <summary>水平走跑倍率，0 为站桩，1 为满权。</summary>
        float MoveWeight { get; }
    }

    /// <summary>
    /// 锁定朝向：有点则转向该世界坐标（绕圈 strafing），无则仍朝移动方向。
    /// </summary>
    public interface IMoveFacingProvider
    {
        /// <summary>锁定目标的世界坐标；无锁或目标无效时返回 false。</summary>
        bool TryGetFacingPoint(out Vector3 worldPoint);
    }

    /// <summary>跳跃门控（技能占轴、禁移、空中等）。</summary>
    public interface IJumpGate
    {
        /// <summary>当前是否允许起跳（战斗禁跳、技能占轴等；着地/土狼由电机判断）。</summary>
        bool CanJump { get; }
    }

    /// <summary>移动时间源（支持时间缩放）。</summary>
    public interface ILocomotionTimeSource
    {
        /// <summary>玩家层累计时间。</summary>
        float PlayerTime { get; }

        /// <summary>玩家层本帧 delta（Update）。</summary>
        float PlayerDelta { get; }

        /// <summary>玩家层时间缩放。</summary>
        float PlayerScale { get; }

        /// <summary>玩家层 Fixed delta。</summary>
        float FixedPlayerDelta { get; }
    }

    /// <summary>移动状态回调（地面移动 / 跳跃 / 空中）。</summary>
    public interface ILocomotionStateSink
    {
        /// <summary>同步地面移动意图。</summary>
        void SetLocomotionState(bool isMoving, bool isRun, bool isWalk);

        /// <summary>Locomotion 一段跳成功。</summary>
        void NotifyJumpStarted();

        /// <summary>每帧同步是否落地；空中时区分 Jump（主动起跳）与 Falling（滑落）。</summary>
        void SyncAirborneState(bool isGrounded, bool isFalling);
    }

    /// <summary>桥接 <see cref="GameTimeManager"/> 玩家层（仅本地玩家默认）。</summary>
    public sealed class GameTimeLocomotionTimeSource : ILocomotionTimeSource
    {
        public float PlayerTime => GameTimeManager.PlayerTime;
        public float PlayerDelta => GameTimeManager.PlayerDelta;
        public float PlayerScale => GameTimeManager.PlayerScale;
        public float FixedPlayerDelta => Time.fixedDeltaTime * GameTimeManager.PlayerScale;
    }

    /// <summary>按战斗实体选层：本地玩家走玩家钟，其余走世界钟。</summary>
    public sealed class CombatUnitLocomotionTimeSource : ILocomotionTimeSource
    {
        readonly CombatEntity _owner;

        /// <summary>绑定宿主，电机每帧读当前层 × 实体钟。</summary>
        public CombatUnitLocomotionTimeSource(CombatEntity owner)
        {
            _owner = owner;
        }

        public float PlayerTime => CombatTimeClock.GetLayerTime(_owner);
        public float PlayerDelta => CombatTimeClock.GetDelta(_owner);
        public float PlayerScale => CombatTimeClock.GetLayerScale(_owner) * Mathf.Max(0f, _owner != null ? _owner.GetTimeScale() : 1f);
        public float FixedPlayerDelta => CombatTimeClock.GetFixedDelta(_owner);
    }

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
