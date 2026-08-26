using EGamePlay.Unity.Locomotion;
using UnityEngine;
namespace ACTGameEditor.Locomotion
{
    /// <summary>始终允许移动。</summary>
    public sealed class AlwaysAllowMoveGate : IMoveGate
    {
        public static readonly AlwaysAllowMoveGate Instance = new();
        public bool CanMove => true;
    }

    /// <summary>使用 Unity 默认时间（独立测试场景）。</summary>
    public sealed class UnityLocomotionTimeSource : ILocomotionTimeSource
    {
        float _playerTime;

        public float PlayerTime => _playerTime;
        public float PlayerDelta => Time.deltaTime;
        public float PlayerScale => 1f;
        public float FixedPlayerDelta => Time.fixedDeltaTime;

        /// <summary>在 Bootstrap Update 中推进累计时间。</summary>
        public void Tick() => _playerTime += Time.deltaTime;
    }

    /// <summary>从指定 Camera 取平面朝向；未指定时回退 Camera.main。</summary>
    public sealed class TransformCameraProvider : IMoveCameraProvider
    {
        Camera _camera;

        public TransformCameraProvider(Camera camera = null)
        {
            _camera = camera;
        }

        public void SetCamera(Camera camera) => _camera = camera;

        public Vector3 PlanarForward
        {
            get
            {
                Transform t = Resolve();
                if (t == null)
                    return Vector3.forward;

                Vector3 f = t.forward;
                f.y = 0f;
                return f.sqrMagnitude > 0.0001f ? f.normalized : Vector3.forward;
            }
        }

        public Vector3 PlanarRight
        {
            get
            {
                Vector3 f = PlanarForward;
                return new Vector3(f.z, 0f, -f.x);
            }
        }

        Transform Resolve()
        {
            if (_camera != null)
                return _camera.transform;
            return Camera.main != null ? Camera.main.transform : null;
        }
    }

    /// <summary>直接提供轴值（可被 InputAction / 摇杆写入）。</summary>
    public sealed class MutableMoveInputProvider : IMoveInputProvider
    {
        public Vector2 MoveAxis { get; set; }
    }

    /// <summary>空状态回调。</summary>
    public sealed class NullLocomotionStateSink : ILocomotionStateSink
    {
        public static readonly NullLocomotionStateSink Instance = new();
        public void SetLocomotionState(bool isMoving, bool isRun) { }
        public void NotifyJumpStarted() { }
        public void SyncAirborneState(bool isGrounded, bool isFalling) { }
    }
}
