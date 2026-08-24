using UnityEngine;

namespace EGamePlay.Unity.Locomotion
{
    /// <summary>位移请求来源。</summary>
    public enum MotionSource : byte
    {
        Gravity = 0,
        Locomotion = 1,
        RootMotion = 2,
        SkillCurve = 3,
    }

    /// <summary>当前允许的水平位移通道（互斥）。</summary>
    public enum MotionPolicy : byte
    {
        /// <summary>摇杆 Locomotion + 重力。</summary>
        Locomotion = 0,
        /// <summary>动画 Root Motion（技能 UseRootMotion）。</summary>
        RootMotion = 1,
        /// <summary>时间轴 XCMove 等曲线位移。</summary>
        SkillCurve = 2,
    }

    /// <summary>
    /// 角色位移唯一落地口：按 Policy 筛选水平源，并门控重力。
    /// </summary>
    public sealed class MotionDirector
    {
        CharacterController _controller;
        MotionPolicy _policy = MotionPolicy.Locomotion;
        int _gravityPushCount;
        float _gravityTimer;
        bool _skillSuppressGravity;

        /// <summary>当前水平位移策略。</summary>
        public MotionPolicy Policy => _policy;

        /// <summary>重力是否允许写入 CC。</summary>
        public bool GravityEnabled =>
            !_skillSuppressGravity && _gravityPushCount <= 0 && _gravityTimer <= 0f;

        /// <summary>绑定 CharacterController。</summary>
        public void Bind(CharacterController controller)
        {
            _controller = controller;
        }

        /// <summary>解绑并复位。</summary>
        public void Unbind()
        {
            _controller = null;
            _policy = MotionPolicy.Locomotion;
            _gravityPushCount = 0;
            _gravityTimer = 0f;
            _skillSuppressGravity = false;
        }

        /// <summary>切换水平位移通道。</summary>
        public void SetPolicy(MotionPolicy policy)
        {
            _policy = policy;
        }

        /// <summary>技能占轴期间是否压制重力（随 PlaySkill / Release）。</summary>
        public void SetSkillSuppressGravity(bool suppress)
        {
            _skillSuppressGravity = suppress;
        }

        /// <summary>压入关重力（可嵌套）。</summary>
        public void PushSuppressGravity()
        {
            _gravityPushCount++;
        }

        /// <summary>弹出关重力。</summary>
        public void PopSuppressGravity()
        {
            if (_gravityPushCount > 0)
                _gravityPushCount--;
        }

        /// <summary>按秒关重力（兼容 SetNoGravityT；取较长剩余）。</summary>
        public void SuppressGravityFor(float seconds)
        {
            if (seconds <= 0f)
                return;
            if (seconds > _gravityTimer)
                _gravityTimer = seconds;
        }

        /// <summary>推进重力计时（FixedUpdate，已含 PlayerScale 的 delta）。</summary>
        public void TickGravity(float scaledDeltaTime)
        {
            if (_gravityTimer <= 0f)
                return;
            _gravityTimer -= scaledDeltaTime;
            if (_gravityTimer < 0f)
                _gravityTimer = 0f;
        }

        /// <summary>
        /// 申请写入位移。水平源受 Policy 约束；Gravity 受 GravityEnabled 约束。
        /// </summary>
        /// <param name="flattenY">true 时去掉 Y（有重力时 RM 常用）。</param>
        public bool TryApply(MotionSource source, Vector3 worldDelta, bool flattenY = true)
        {
            if (_controller == null || !_controller.enabled)
                return false;

            if (source == MotionSource.Gravity)
            {
                if (!GravityEnabled)
                    return false;
            }
            else if (!IsHorizontalSourceAllowed(source))
            {
                return false;
            }

            if (flattenY)
                worldDelta.y = 0f;

            if (worldDelta.sqrMagnitude <= 0f)
                return false;

            _controller.Move(worldDelta);
            return true;
        }

        /// <summary>重力专用（始终不 flatten）。</summary>
        public bool TryApplyGravity(Vector3 worldDelta)
        {
            return TryApply(MotionSource.Gravity, worldDelta, flattenY: false);
        }

        bool IsHorizontalSourceAllowed(MotionSource source)
        {
            return _policy switch
            {
                MotionPolicy.Locomotion => source == MotionSource.Locomotion,
                MotionPolicy.RootMotion => source == MotionSource.RootMotion,
                MotionPolicy.SkillCurve => source == MotionSource.SkillCurve,
                _ => false,
            };
        }
    }
}
