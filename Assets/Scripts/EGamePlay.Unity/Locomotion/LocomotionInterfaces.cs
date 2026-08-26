using UnityEngine;

namespace EGamePlay.Unity.Locomotion
{
    /// <summary>移动输入源。</summary>
    public interface IMoveInputProvider
    {
        /// <summary>平面移动轴（x=右, y=前），通常为 -1~1。</summary>
        Vector2 MoveAxis { get; }
    }

    /// <summary>用于计算相机相对移动方向。</summary>
    public interface IMoveCameraProvider
    {
        /// <summary>相机在 XZ 平面的前方向（已归一化，Y=0）。</summary>
        Vector3 PlanarForward { get; }

        /// <summary>相机在 XZ 平面的右方向（已归一化，Y=0）。</summary>
        Vector3 PlanarRight { get; }
    }

    /// <summary>可移动门控（战斗禁移等）。</summary>
    public interface IMoveGate
    {
        /// <summary>当前是否允许水平移动。</summary>
        bool CanMove { get; }
    }

    /// <summary>跳跃门控（技能占轴、禁移、空中等）。</summary>
    public interface IJumpGate
    {
        /// <summary>当前是否允许起跳（仅一段跳，落地后才能再次跳）。</summary>
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
        /// <summary>同步地面移动意图（Run/Idle）。</summary>
        void SetLocomotionState(bool isMoving, bool isRun);

        /// <summary>Locomotion 一段跳成功。</summary>
        void NotifyJumpStarted();

        /// <summary>每帧同步是否落地；空中时区分 Jump（主动起跳）与 Falling（滑落）。</summary>
        void SyncAirborneState(bool isGrounded, bool isFalling);
    }
}
