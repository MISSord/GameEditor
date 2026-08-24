using EGamePlay.Unity.Locomotion;
using UnityEngine;

namespace EGamePlay.Unity
{
    /// <summary>
    /// Animator Root Motion 采样口：仅 Token 占有时向 MotionDirector 提交 RootMotion。
    /// 挂在与 Animator 同一物体上。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public class RootMotionDriver : MonoBehaviour
    {
        Animator _animator;
        MotionDirector _motion;

        bool _tokenOwns;
        bool _applyPosition = true;
        bool _applyRotation;

        /// <summary>本帧动画算出的位移增量（无论是否应用）。</summary>
        public Vector3 DeltaPosition { get; private set; }

        /// <summary>本帧动画算出的旋转增量。</summary>
        public Quaternion DeltaRotation { get; private set; } = Quaternion.identity;

        /// <summary>为 true 时 AnimStateMachine 等不应再自行 Move，避免双推。</summary>
        public bool ConsumesRootMotion => true;

        /// <summary>当前是否由技能 Token 允许采样 RM。</summary>
        public bool TokenOwnsMotion => _tokenOwns;

        void Awake()
        {
            _animator = GetComponent<Animator>();
        }

        /// <summary>绑定位移裁决。</summary>
        public void Bind(MotionDirector motion)
        {
            _motion = motion;
            if (_animator == null)
                _animator = GetComponent<Animator>();
        }

        /// <summary>由 AnimDirector 在 PlaySkill / Release 时调用。</summary>
        public void SetTokenOwnsMotion(bool owns)
        {
            _tokenOwns = owns;
            if (_animator == null)
                _animator = GetComponent<Animator>();
            if (_animator != null)
                _animator.applyRootMotion = owns;
            if (!owns)
                ResetDeltas();
        }

        /// <summary>本段是否应用位移 / 旋转（默认只位移）。</summary>
        public void SetApplyFlags(bool position, bool rotation)
        {
            _applyPosition = position;
            _applyRotation = rotation;
        }

        void OnAnimatorMove()
        {
            if (_animator == null)
                return;

            DeltaPosition = _animator.deltaPosition;
            DeltaRotation = _animator.deltaRotation;

            if (!_tokenOwns || _motion == null)
                return;

            if (_applyPosition)
            {
                // 有重力时只吃水平，避免和重力抢 Y；关重力时整段交给 RM
                bool flattenY = _motion.GravityEnabled;
                _motion.TryApply(MotionSource.RootMotion, DeltaPosition, flattenY);
            }

            if (_applyRotation)
            {
                // 旋转仍写在 Animator 所在层级的父根；无 CC 引用时用 animator 根
                Transform root = _animator.transform.parent != null
                    ? _animator.transform.parent
                    : _animator.transform;
                root.rotation = DeltaRotation * root.rotation;
            }
        }

        /// <summary>清零缓存增量。</summary>
        public void ResetDeltas()
        {
            DeltaPosition = Vector3.zero;
            DeltaRotation = Quaternion.identity;
        }
    }
}
