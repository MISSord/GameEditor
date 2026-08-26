using UnityEngine;

namespace EGamePlay.Unity.Locomotion
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
        IJumpGate _jumpGate;
        ILocomotionTimeSource _time;
        ILocomotionStateSink _stateSink;
        System.Func<bool> _canWriteAnimParams;
        MotionDirector _motion;

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
        Vector3 _airborneMoveDir;

        /// <summary>是否处于空中 Locomotion（跳跃/滑落，不含技能浮空）。</summary>
        bool IsAirborneLocomotion => !IsGrounded || _velocity.y > 0.05f;
        public bool LocomotionEnabled { get; set; } = true;

        /// <summary>是否允许 AutoRotate 跟随移动方向。</summary>
        public bool RotationEnabled { get; set; } = true;

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

        /// <summary>动画参数写门控。返回 false 时不写 MoveSpeed/IsRun（技能占轴期间）。</summary>
        public void SetAnimParamWriteGate(System.Func<bool> canWrite) => _canWriteAnimParams = canWrite;

        /// <summary>跳跃门控（战斗禁跳、技能占轴等）。</summary>
        public void SetJumpGate(IJumpGate jumpGate) => _jumpGate = jumpGate;

        /// <summary>绑定位移裁决；有则水平/重力均走 MotionDirector。</summary>
        public void BindMotion(MotionDirector motion) => _motion = motion;

        /// <summary>临时关闭重力（优先交 MotionDirector）。</summary>
        public void SetNoGravityT(float time)
        {
            if (_motion != null)
            {
                _motion.SuppressGravityFor(time);
                return;
            }

            if (time > _disableGravityTimer)
                _disableGravityTimer = time;
        }

        /// <summary>
        /// 尝试一段跳：须落地、未上升、通过门控。成功则写入垂直初速度。
        /// </summary>
        public bool TryJump()
        {
            if (!LocomotionEnabled)
                return false;
            if (_jumpGate != null && !_jumpGate.CanJump)
                return false;
            if (_controller == null || !_controller.enabled)
                return false;
            if (!IsGrounded || _velocity.y > 0.05f)
                return false;

            float height = _tuning.JumpHeight;
            if (height <= 0f)
                return false;

            float gravity = Mathf.Abs(_tuning.Gravity);
            if (gravity < 0.01f)
                return false;

            _velocity.y = Mathf.Sqrt(2f * gravity * height);
            _isFalling = false;

            // 保留起跳方向与水平速度（鸣潮/绝区零：跑跳不停步）
            if (_moveDir.sqrMagnitude > 0.0001f)
                _airborneMoveDir = _moveDir;
            else if (_root != null)
                _airborneMoveDir = Vector3.ProjectOnPlane(_root.forward, Vector3.up).normalized;

            _stateSink?.NotifyJumpStarted();

            // 输入在 Update、重力在 FixedUpdate：立刻抬离地面，避免本帧仍判 grounded
            float stepDelta = _time != null ? _time.FixedPlayerDelta : Time.fixedDeltaTime;
            Vector3 lift = new Vector3(0f, _velocity.y * stepDelta, 0f);
            if (_motion != null)
                _motion.TryApplyGravity(lift);
            else
                _controller.Move(lift);

            return true;
        }

        /// <summary>Update 阶段：落地检测 + 输入与状态。</summary>
        public void TickUpdate()
        {
            GroundedCheck();
            bool isFalling = !IsGrounded;
            _stateSink?.SyncAirborneState(IsGrounded, isFalling);

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
                if (_currentSpeed < dead && !IsAirborneLocomotion)
                {
                    _moveDir = Vector3.zero;
                    _isRun = false;
                    _stateSink?.SetLocomotionState(false, false);
                }

                float now = _time != null ? _time.PlayerTime : Time.time;
                if (now - _stepStartTime >= _tuning.MinimumStepTime && !IsAirborneLocomotion)
                {
                    _isPerformingStep = false;
                    _targetSpeed = 0f;
                }
            }
        }

        /// <summary>FixedUpdate 阶段：重力 + 转向 + 水平位移 + 动画。</summary>
        public void TickFixed()
        {
            UpdateGravity();

            bool canMove = _gate == null || _gate.CanMove;
            if (_controller == null || !canMove)
                return;

            if (!LocomotionEnabled)
            {
                ApplyAnimSpeed(0f, false);
                return;
            }

            if (RotationEnabled)
            {
                Vector3 faceDir = ResolveHorizontalMoveDir(IsAirborneLocomotion);
                if (faceDir.sqrMagnitude > 0.0001f)
                    AutoRotate(faceDir);
            }

            ApplyHorizontalMove(canMove, IsAirborneLocomotion);
        }

        void UpdateGravity()
        {
            if (_controller == null)
                return;

            float playerDelta = _time != null ? _time.FixedPlayerDelta : Time.fixedDeltaTime;
            bool gravityOn = ResolveGravityEnabled(playerDelta);

            if (gravityOn)
            {
                // 起跳后几帧 IsGrounded 仍为 true，但 vy>0 时必须走空中分支，否则会 Lerp 掉跳跃初速
                bool stickToGround = IsGrounded && _velocity.y <= 0f;
                if (stickToGround)
                {
                    float groundY = _tuning.Gravity * _tuning.GravityOnGroundRate;
                    if (_velocity.y > groundY)
                        _velocity.y = Mathf.Lerp(_velocity.y, groundY, 0.1f);
                    else
                        _velocity.y = groundY;

                    if (_moveDir.sqrMagnitude > 0.0001f)
                        _airborneMoveDir = _moveDir;
                }
                else
                {
                    float maxDown = _tuning.Gravity * _tuning.GravityMaxRate;
                    _velocity.y = Mathf.Clamp(
                        _velocity.y + _tuning.Gravity * _tuning.GravityOnAirAddRate * playerDelta,
                        maxDown,
                        40f);
                }

                Vector3 delta = _velocity * playerDelta;
                if (_motion != null)
                    _motion.TryApplyGravity(delta);
                else
                    _controller.Move(delta);
            }
            else
            {
                _velocity.y = _tuning.Gravity * _tuning.GravityOnGroundRate;
            }
        }

        bool ResolveGravityEnabled(float playerDelta)
        {
            if (_motion != null)
            {
                _motion.TickGravity(playerDelta);
                return _motion.GravityEnabled;
            }

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

            return _enableGravity;
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

        Vector3 ResolveHorizontalMoveDir(bool airborne)
        {
            if (_inputDir.magnitude > _tuning.InputDeadZone && _moveDir.sqrMagnitude > 0.0001f)
            {
                if (airborne)
                    _airborneMoveDir = _moveDir;
                return _moveDir;
            }

            if (airborne && _currentSpeed > 0.05f && _airborneMoveDir.sqrMagnitude > 0.0001f)
                return _airborneMoveDir;

            return _moveDir;
        }

        void ApplyHorizontalMove(bool gateOpen, bool airborne)
        {
            if (_controller == null || !LocomotionEnabled)
                return;

            float airControl = airborne ? Mathf.Clamp01(_tuning.AirControl) : 1f;
            if (airborne && airControl <= 0f)
                return;

            if (!gateOpen)
            {
                if (!airborne)
                {
                    _curMoveSpeedAnim = 0f;
                    ApplyAnimSpeed(0f, false);
                }
                return;
            }

            Vector3 worldMoveDir = ResolveHorizontalMoveDir(airborne);
            if (worldMoveDir.sqrMagnitude < 0.0001f && _currentSpeed < 0.01f)
            {
                if (!airborne)
                {
                    _curMoveSpeedAnim = 0f;
                    ApplyAnimSpeed(0f, false);
                }
                return;
            }

            UpdateAnimSpeedParam();
            float scale = _time != null ? _time.PlayerScale : 1f;

            float targetSpeed = _targetSpeed;
            if (airborne)
                targetSpeed *= Mathf.Max(0f, _tuning.AirMoveSpeedScale);

            float accelTime = airborne
                ? _tuning.Acceleration / Mathf.Max(airControl, 0.05f)
                : _tuning.Acceleration;
            float decelTime = airborne
                ? _tuning.Deceleration / Mathf.Max(airControl, 0.05f)
                : _tuning.Deceleration;

            _currentSpeed = Mathf.SmoothDamp(
                _currentSpeed,
                targetSpeed,
                ref _velocityXSmoothing,
                _currentSpeed < targetSpeed ? accelTime : decelTime);

            if (worldMoveDir.sqrMagnitude > 0.0001f)
            {
                Vector3 horizontal = worldMoveDir * _currentSpeed * Time.fixedDeltaTime * scale;
                if (_motion != null)
                    _motion.TryApply(MotionSource.Locomotion, horizontal, flattenY: true);
                else
                    _controller.Move(horizontal);
            }

            bool animRun = _isRun || (airborne && _currentSpeed > 0.1f);
            ApplyAnimSpeed(_curMoveSpeedAnim, animRun);
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
            if (_canWriteAnimParams != null && !_canWriteAnimParams())
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
