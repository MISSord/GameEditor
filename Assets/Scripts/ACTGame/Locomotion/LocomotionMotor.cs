using UnityEngine;

namespace ACTGameEditor.Locomotion
{
    /// <summary>
    /// 无 Unity 生命周期的移动核：输入→相机相对方向→转向/加速→CharacterController + 重力 + Animator。
    /// </summary>
    public sealed class LocomotionMotor
    {
        static readonly int MoveSpeedId = Animator.StringToHash("MoveSpeed");
        static readonly int IsRunId = Animator.StringToHash("IsRun");

        LocomotionTuning _tuning = LocomotionTuning.CreateDefault();
        CharacterController _controller;
        Transform _root;
        Animator _animator;
        IMoveInputProvider _input;
        IMoveCameraProvider _camera;
        IMoveGate _gate;
        ILocomotionTimeSource _time;
        ILocomotionStateSink _stateSink;

        float _currentSpeed;
        float _targetSpeed;
        float _velocityXSmoothing;
        float _curMoveSpeedAnim;
        float _disableGravityTimer;
        float _stepStartTime;
        Vector3 _moveDir;
        Vector2 _inputDir;
        Vector3 _velocity;
        bool _isFalling;
        bool _isPerformingStep;
        bool _enableGravity = true;
        bool _isRun;

        /// <summary>是否处理输入与水平移动（对应旧 Enable）。</summary>
        public bool LocomotionEnabled { get; set; } = true;

        /// <summary>落地检测结果。</summary>
        public bool IsGrounded { get; private set; } = true;

        /// <summary>当前水平移动方向（世界空间）。</summary>
        public Vector3 MoveDir => _moveDir;

        /// <summary>是否处于跑步态。</summary>
        public bool IsRun => _isRun;

        /// <summary>绑定场景对象与依赖。</summary>
        public void Bind(
            CharacterController controller,
            Transform root,
            Animator animator,
            IMoveInputProvider input,
            IMoveCameraProvider camera,
            IMoveGate gate,
            ILocomotionTimeSource time,
            ILocomotionStateSink stateSink = null)
        {
            _controller = controller;
            _root = root;
            _animator = animator;
            _input = input;
            _camera = camera;
            _gate = gate;
            _time = time;
            _stateSink = stateSink;
        }

        /// <summary>应用调参。</summary>
        public void SetTuning(in LocomotionTuning tuning) => _tuning = tuning;

        /// <summary>临时关闭重力。</summary>
        public void SetNoGravityT(float time)
        {
            if (time > _disableGravityTimer)
                _disableGravityTimer = time;
        }

        /// <summary>Update 阶段：落地检测 + 输入与状态。</summary>
        public void TickUpdate()
        {
            GroundedCheck();

            if (!LocomotionEnabled)
                return;

            if (_gate != null && !_gate.CanMove)
                return;

            if (_controller == null || !_controller.enabled)
                return;

            if (IsGrounded)
                _isFalling = false;
            else
                _isFalling = true;

            _inputDir = _input != null ? _input.MoveAxis : Vector2.zero;
            float dead = _tuning.InputDeadZone;

            if (_inputDir.magnitude > dead)
            {
                if (!_isPerformingStep)
                {
                    _isPerformingStep = true;
                    _stepStartTime = _time != null ? _time.PlayerTime : Time.time;
                }

                Vector3 forward = _camera != null ? _camera.PlanarForward : Vector3.forward;
                Vector3 right = _camera != null ? _camera.PlanarRight : Vector3.right;
                Vector3 relative = _inputDir.x * right + _inputDir.y * forward;
                relative.Normalize();
                _moveDir = relative;

                _isRun = true;
                _targetSpeed = _tuning.RunMoveSpeed;
                _stateSink?.SetLocomotionState(true, true);
            }
            else
            {
                if (_currentSpeed < dead)
                {
                    _moveDir = Vector3.zero;
                    _isRun = false;
                    _stateSink?.SetLocomotionState(false, false);
                }

                float now = _time != null ? _time.PlayerTime : Time.time;
                if (now - _stepStartTime >= _tuning.MinimumStepTime)
                {
                    _isPerformingStep = false;
                    _targetSpeed = 0f;
                }
            }
        }

        /// <summary>FixedUpdate 阶段：重力 + 转向 + 水平位移 + 动画。</summary>
        public void TickFixed()
        {
            CheckEnableGravity();
            UpdateGravity();

            bool canMove = _gate == null || _gate.CanMove;
            if (_controller == null || !canMove)
                return;

            if (!LocomotionEnabled)
            {
                ApplyAnimSpeed(0f, false);
                return;
            }

            AutoRotate(_moveDir);
            ApplyHorizontalMove(_moveDir, IsGrounded && canMove);
        }

        void UpdateGravity()
        {
            if (_controller == null)
                return;

            float playerDelta = _time != null ? _time.PlayerDelta : Time.deltaTime;

            if (_enableGravity)
            {
                if (IsGrounded)
                {
                    float groundY = _tuning.Gravity * _tuning.GravityOnGroundRate;
                    if (_velocity.y > groundY)
                        _velocity.y = Mathf.Lerp(_velocity.y, groundY, 0.1f);
                    else
                        _velocity.y = groundY;
                }
                else
                {
                    float maxDown = _tuning.Gravity * _tuning.GravityMaxRate;
                    _velocity.y = Mathf.Clamp(
                        _velocity.y + _tuning.Gravity * _tuning.GravityOnAirAddRate * playerDelta,
                        maxDown,
                        40f);
                }

                _controller.Move(_velocity * playerDelta);
            }
            else
            {
                _velocity.y = _tuning.Gravity * _tuning.GravityOnGroundRate;
            }
        }

        void CheckEnableGravity()
        {
            if (_disableGravityTimer > 0f)
            {
                _enableGravity = false;
                float scale = _time != null ? _time.PlayerScale : 1f;
                _disableGravityTimer -= Time.fixedDeltaTime * scale;
            }
            else
            {
                _enableGravity = true;
            }
        }

        void AutoRotate(Vector3 worldMove)
        {
            if (_root == null)
                return;

            Vector3 local = _root.InverseTransformDirection(worldMove);
            float turnAmount = Mathf.Atan2(local.x, local.z);
            float scale = _time != null ? _time.PlayerScale : 1f;
            _root.Rotate(0f, turnAmount * _tuning.MovingTurnSpeed * Time.fixedDeltaTime * scale, 0f);
        }

        void ApplyHorizontalMove(Vector3 worldMove, bool canMove)
        {
            if (canMove)
            {
                UpdateAnimSpeedParam();
                float scale = _time != null ? _time.PlayerScale : 1f;
                float smoothTime = _currentSpeed < _targetSpeed ? _tuning.Acceleration : _tuning.Deceleration;
                _currentSpeed = Mathf.SmoothDamp(_currentSpeed, _targetSpeed, ref _velocityXSmoothing, smoothTime);
                _controller.Move(worldMove * _currentSpeed * Time.fixedDeltaTime * scale);
                ApplyAnimSpeed(_curMoveSpeedAnim, _isRun);
            }
            else
            {
                _curMoveSpeedAnim = 0f;
                ApplyAnimSpeed(0f, false);
            }
        }

        void UpdateAnimSpeedParam()
        {
            float tmpMoveLen = _inputDir.magnitude;
            if (_isPerformingStep)
                tmpMoveLen = _moveDir.magnitude;

            float max = _isRun ? 1f : 0f;
            if (tmpMoveLen > _tuning.InputDeadZone)
                _curMoveSpeedAnim = Mathf.Lerp(_curMoveSpeedAnim, max, _tuning.AnimSpeedAcceleration);
            else
                _curMoveSpeedAnim = Mathf.Lerp(_curMoveSpeedAnim, 0f, _tuning.AnimSpeedAcceleration);
        }

        void ApplyAnimSpeed(float moveSpeed, bool isRun)
        {
            if (_animator == null)
                return;

            _animator.SetFloat(MoveSpeedId, moveSpeed);
            _animator.SetBool(IsRunId, isRun);
        }

        void GroundedCheck()
        {
            if (_root == null)
            {
                IsGrounded = true;
                return;
            }

            // Unity Capsule / CC 的 pivot 在中心，必须按脚底取样，否则永远判空中 → WASD 无效
            float feetY = _root.position.y;
            if (_controller != null)
                feetY += _controller.center.y - _controller.height * 0.5f;

            Vector3 spherePosition = new Vector3(_root.position.x, feetY - _tuning.GroundedOffset, _root.position.z);
            bool sphereHit = Physics.CheckSphere(
                spherePosition,
                _tuning.GroundedRadius,
                _tuning.GroundLayers,
                QueryTriggerInteraction.Ignore);

            bool ccGrounded = _controller != null && _controller.isGrounded;
            IsGrounded = sphereHit || ccGrounded;
        }
    }
}
