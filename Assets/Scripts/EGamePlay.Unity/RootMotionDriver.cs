using UnityEngine;

namespace EGamePlay.Unity
{
    /// <summary>
    /// Animator Root Motion 采样口。
    /// 技能 Token 走 <see cref="MotionSource.RootMotion"/>；Locomotion 急转借用走 <see cref="MotionSource.Locomotion"/>，不改 Policy、不占 Token。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public class RootMotionDriver : MonoBehaviour
    {
        Animator _animator;
        MotionDirector _motion;

        bool _tokenOwns;
        bool _locomotionOwns;
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

        /// <summary>Locomotion 急转是否正在借用本帧 delta。</summary>
        public bool LocomotionOwnsMotion => _locomotionOwns;

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

        /// <summary>由 AnimDirector 在 PlaySkill / Release 时调用。Token 优先于 Locomotion 借用。</summary>
        public void SetTokenOwnsMotion(bool owns)
        {
            _tokenOwns = owns;
            if (owns)
                _locomotionOwns = false;
            RefreshApplyRootMotion();
            if (!owns && !_locomotionOwns)
                ResetDeltas();
        }

        /// <summary>
        /// Locomotion 急转窗口借用 RM 增量。不切 MotionPolicy，不占技能 Token。
        /// 技能 Token 占用时调用会被忽略。
        /// </summary>
        public void SetLocomotionOwnsMotion(bool owns)
        {
            if (owns && _tokenOwns)
                return;

            _locomotionOwns = owns;
            RefreshApplyRootMotion();
            if (!owns && !_tokenOwns)
                ResetDeltas();
        }

        /// <summary>本段是否应用位移 / 旋转（默认只位移）。急转需要同时开旋转。</summary>
        public void SetApplyFlags(bool position, bool rotation)
        {
            _applyPosition = position;
            _applyRotation = rotation;
        }

        void RefreshApplyRootMotion()
        {
            if (_animator == null)
                _animator = GetComponent<Animator>();
            if (_animator != null)
                _animator.applyRootMotion = _tokenOwns || _locomotionOwns;
        }

        void OnAnimatorMove()
        {
            if (_animator == null)
                return;

            DeltaPosition = _animator.deltaPosition;
            DeltaRotation = _animator.deltaRotation;

            if (_motion == null)
                return;

            if (_tokenOwns)
            {
                ApplyMotion(MotionSource.RootMotion);
                return;
            }

            if (_locomotionOwns)
                ApplyMotion(MotionSource.Locomotion);
        }

        void ApplyMotion(MotionSource source)
        {
            if (_applyPosition)
            {
                bool flattenY = _motion.GravityEnabled;
                _motion.TryApply(source, DeltaPosition, flattenY);
            }

            if (!_applyRotation)
                return;

            Transform root = _animator.transform.parent != null
                ? _animator.transform.parent
                : _animator.transform;
            root.rotation = DeltaRotation * root.rotation;
        }

        /// <summary>清零缓存增量。</summary>
        public void ResetDeltas()
        {
            DeltaPosition = Vector3.zero;
            DeltaRotation = Quaternion.identity;
        }
    }
}
