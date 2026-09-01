using UnityEngine;

namespace EGamePlay.Unity
{
    /// <summary>
    /// 无 Unity 生命周期的移动核：输入→相机相对方向→转向/加速→CharacterController + 重力 + Animator。
    /// </summary>
    public sealed class LocomotionMotor
    {
        static readonly int MoveSpeedId = Animator.StringToHash("MoveSpeed");
        static readonly int IsRunId = Animator.StringToHash("IsRun");
        static readonly int IsWalkId = Animator.StringToHash("IsWalk");
        static readonly int IsGroundId = Animator.StringToHash("IsGround");
        static readonly int MoveXId = Animator.StringToHash("MoveX");
        static readonly int MoveYId = Animator.StringToHash("MoveY");
        static readonly int IdleStateId = Animator.StringToHash("Idle");
        static readonly int RunningStateId = Animator.StringToHash("Running");
        static readonly int WalkStateId = Animator.StringToHash("Walk_Eqip_Front");
        const float GaitCrossFade = 0.08f;
        /// <summary>鸣潮式停步滞回：松开方向后这段时间内仍视为有移动意图，避免换键空帧掉快跑。</summary>
        const float MoveReleaseGrace = 0.15f;
        /// <summary>水平速度低于该值才算真正停步，才允许清快跑。</summary>
        const float StopSpeed = 0.35f;

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
        IMoveFacingProvider _facing;
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
        bool _isWalk;
        bool _walkMode;
        bool _sprintArmed;
        float _noInputTime = 999f;
        float _coyoteRemain;
        float _jumpBufferRemain;
        float _landSlowRemain;
        bool _wasGroundedLast = true;
        int _lastGaitAnimHash;
        bool _animStatesCached;
        bool _hasIdleState;
        bool _hasRunningState;
        bool _hasWalkState;
        Vector3 _airborneMoveDir;

        /// <summary>是否处于空中 Locomotion（跳跃/滑落，不含技能浮空）。</summary>
        bool IsAirborneLocomotion => !IsGrounded || _velocity.y > 0.05f;

        /// <summary>是否允许 AutoRotate 跟随移动方向。</summary>
        public bool FaceEnabled { get; set; } = true;

        /// <summary>落地检测结果。</summary>
        public bool IsGrounded { get; private set; } = true;

        /// <summary>当前水平移动方向（世界空间）。</summary>
        public Vector3 MoveDir => _moveDir;

        /// <summary>是否处于跑步态。</summary>
        public bool IsRun => _isRun;

        /// <summary>解绑并清速度，供组件池复用。</summary>
        public void ResetRuntimeState()
        {
            _controller = null;
            _root = null;
            _animator = null;
            _input = null;
            _camera = null;
            _gate = null;
            _jumpGate = null;
            _time = null;
            _stateSink = null;
            _facing = null;
            _canWriteAnimParams = null;
            _motion = null;
            _currentSpeed = 0f;
            _targetSpeed = 0f;
            _velocityXSmoothing = 0f;
            _curMoveSpeedAnim = 0f;
            _disableGravityTimer = 0f;
            _stepStartTime = 0f;
            _moveDir = Vector3.zero;
            _inputDir = Vector2.zero;
            _velocity = Vector3.zero;
            _isFalling = false;
            _isPerformingStep = false;
            _enableGravity = true;
            _isRun = false;
            _isWalk = false;
            _walkMode = false;
            _sprintArmed = false;
            _noInputTime = 999f;
            _coyoteRemain = 0f;
            _jumpBufferRemain = 0f;
            _landSlowRemain = 0f;
            _wasGroundedLast = true;
            _lastGaitAnimHash = 0;
            _animStatesCached = false;
            _airborneMoveDir = Vector3.zero;
            IsGrounded = true;
            FaceEnabled = true;
        }

        /// <summary>
        /// 鸣潮：非走路时闪避锁存快跑。技能占轴期间不采步态，结束时若仍有移动意图则接疾跑，直到真正停步。
        /// </summary>
        public void ArmSprintFromDodge()
        {
            if (_walkMode)
                return;
            _sprintArmed = true;
        }

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
            _animStatesCached = false;
            _lastGaitAnimHash = 0;
        }

        /// <summary>应用调参。</summary>
        public void SetTuning(in LocomotionTuning tuning) => _tuning = tuning;

        /// <summary>动画参数写门控。返回 false 时不写 MoveSpeed/IsRun（技能占轴期间）。</summary>
        public void SetAnimParamWriteGate(System.Func<bool> canWrite) => _canWriteAnimParams = canWrite;

        /// <summary>锁定朝向。有则转向目标（绕圈），无则朝移动方向。</summary>
        public void SetFacingProvider(IMoveFacingProvider facing) => _facing = facing;

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
        /// 尝试一段跳：写入缓冲并立刻尝试消耗。离地后仍可走土狼时间；落地前按跳会在缓冲窗口内补跳。
        /// </summary>
        public bool TryJump()
        {
            float buffer = _tuning.JumpBufferTime > 0.001f ? _tuning.JumpBufferTime : 0.12f;
            _jumpBufferRemain = buffer;
            return TryConsumeJump();
        }

        bool TryConsumeJump()
        {
            if (_jumpBufferRemain <= 0f)
                return false;
            if (_jumpGate != null && !_jumpGate.CanJump)
                return false;
            if (_controller == null || !_controller.enabled)
                return false;
            if (_velocity.y > 0.05f)
                return false;

            bool groundedFeet = IsGrounded;
            bool coyote = _coyoteRemain > 0f;
            if (!groundedFeet && !coyote)
                return false;

            float height = _tuning.JumpHeight;
            if (height <= 0f)
                return false;

            float gravity = Mathf.Abs(_tuning.Gravity);
            if (gravity < 0.01f)
                return false;

            _velocity.y = Mathf.Sqrt(2f * gravity * height);
            _isFalling = false;
            _jumpBufferRemain = 0f;
            _coyoteRemain = 0f;
            _landSlowRemain = 0f;

            if (_moveDir.sqrMagnitude > 0.0001f)
                _airborneMoveDir = _moveDir;
            else if (_root != null)
                _airborneMoveDir = Vector3.ProjectOnPlane(_root.forward, Vector3.up).normalized;

            _stateSink?.NotifyJumpStarted();

            float stepDelta = _time != null ? _time.FixedPlayerDelta : Time.fixedDeltaTime;
            Vector3 lift = new Vector3(0f, _velocity.y * stepDelta, 0f);
            if (_motion != null)
                _motion.TryApplyGravity(lift);
            else
                _controller.Move(lift);

            return true;
        }

        void TickJumpAssist(float dt)
        {
            bool groundedStable = IsGrounded && _velocity.y <= 0.05f;
            if (groundedStable)
                _coyoteRemain = _tuning.CoyoteTime > 0.001f ? _tuning.CoyoteTime : 0.1f;
            else
                _coyoteRemain = Mathf.Max(0f, _coyoteRemain - dt);

            if (!_wasGroundedLast && groundedStable)
                _landSlowRemain = _tuning.LandSlowTime > 0.001f ? _tuning.LandSlowTime : 0.1f;
            _wasGroundedLast = IsGrounded;

            if (_jumpBufferRemain > 0f)
                _jumpBufferRemain = Mathf.Max(0f, _jumpBufferRemain - dt);

            TryConsumeJump();
        }

        /// <summary>Update 阶段：落地检测 + 输入与状态。</summary>
        public void TickUpdate()
        {
            GroundedCheck();
            float dt = _time != null ? _time.PlayerDelta : Time.deltaTime;
            TickJumpAssist(dt);

            bool isFalling = !IsGrounded;
            _stateSink?.SyncAirborneState(IsGrounded, isFalling);

            SampleMoveIntent(dt);

            if (ResolveMoveWeight() <= 0f)
                return;

            if (_controller == null || !_controller.enabled)
                return;

            if (IsGrounded)
                _isFalling = false;
            else
                _isFalling = true;

            float dead = _tuning.InputDeadZone;
            bool moving = Mathf.Clamp01(_inputDir.magnitude) > dead;
            bool hasMoveIntent = moving || _noInputTime < MoveReleaseGrace;

            ApplyGaitInput(hasMoveIntent);

            if (hasMoveIntent)
            {
                if (!_isPerformingStep)
                {
                    _isPerformingStep = true;
                    _stepStartTime = _time != null ? _time.PlayerTime : Time.time;
                }

                ApplyGaitTargetSpeed();
                _stateSink?.SetLocomotionState(true, _isRun, _isWalk);
            }
            else
            {
                if (_currentSpeed < StopSpeed && !IsAirborneLocomotion)
                {
                    _moveDir = Vector3.zero;
                    _isRun = false;
                    _isWalk = false;
                    _stateSink?.SetLocomotionState(false, false, false);
                }

                float now = _time != null ? _time.PlayerTime : Time.time;
                if (now - _stepStartTime >= _tuning.MinimumStepTime && !IsAirborneLocomotion)
                {
                    _isPerformingStep = false;
                    _targetSpeed = 0f;
                }
            }
        }

        /// <summary>
        /// 技能占轴时仍采样摇杆：有输入则更新方向，松开则把粘性意图耗掉。
        /// 正常停步不在这里清速度，仍走原来的减速。
        /// </summary>
        void SampleMoveIntent(float dt)
        {
            _inputDir = _input != null ? _input.MoveAxis : Vector2.zero;
            float dead = _tuning.InputDeadZone;
            bool moving = Mathf.Clamp01(_inputDir.magnitude) > dead;
            if (moving)
                _noInputTime = 0f;
            else
                _noInputTime += dt;

            if (moving)
            {
                Vector3 forward = _camera != null ? _camera.PlanarForward : Vector3.forward;
                Vector3 right = _camera != null ? _camera.PlanarRight : Vector3.right;
                Vector3 relative = _inputDir.x * right + _inputDir.y * forward;
                relative.Normalize();
                _moveDir = relative;
                return;
            }

            bool gated = ResolveMoveWeight() <= 0f;
            if (!gated || _noInputTime < MoveReleaseGrace || IsAirborneLocomotion)
                return;

            _moveDir = Vector3.zero;
            _targetSpeed = 0f;
            _currentSpeed = 0f;
            _velocityXSmoothing = 0f;
            _isRun = false;
            _isWalk = false;
            _isPerformingStep = false;
        }

        /// <summary>FixedUpdate 阶段：重力 + 转向 + 水平位移 + 动画。</summary>
        public void TickFixed()
        {
            UpdateGravity();

            if (!IsAirborneLocomotion && _landSlowRemain > 0f)
            {
                float landDt = _time != null ? _time.FixedPlayerDelta : Time.fixedDeltaTime;
                _landSlowRemain = Mathf.Max(0f, _landSlowRemain - landDt);
            }

            if (_controller == null)
                return;

            float moveWeight = ResolveMoveWeight();

            if (FaceEnabled)
            {
                Vector3 faceDir = ResolveFaceDir();
                if (faceDir.sqrMagnitude > 0.0001f)
                    AutoRotate(faceDir);
            }

            ApplyHorizontalMove(moveWeight, IsAirborneLocomotion);
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

            Vector3 planar = Vector3.ProjectOnPlane(worldMove, Vector3.up);
            if (planar.sqrMagnitude < 0.0001f)
                return;

            Quaternion targetRot = Quaternion.LookRotation(planar.normalized, Vector3.up);
            float delta = _time != null ? _time.FixedPlayerDelta : Time.fixedDeltaTime;
            float maxDeg = _tuning.MovingTurnSpeed * delta;
            _root.rotation = Quaternion.RotateTowards(_root.rotation, targetRot, maxDeg);
        }

        /// <summary>有锁朝目标；否则朝当前水平移动方向。</summary>
        Vector3 ResolveFaceDir()
        {
            const float minLockFacingSqr = 0.0225f;
            if (_facing != null && _root != null && _facing.TryGetFacingPoint(out Vector3 point))
            {
                Vector3 to = Vector3.ProjectOnPlane(point - _root.position, Vector3.up);
                if (to.sqrMagnitude >= minLockFacingSqr)
                    return to;
            }

            return ResolveHorizontalMoveDir(IsAirborneLocomotion);
        }

        /// <summary>
        /// Ctrl 切走/慢跑；慢跑中点 Shift 锁存快跑。
        /// 鸣潮：疾跑跟锁存走，松开方向后等真正停步才退，不因单帧空输入掉回慢跑。
        /// </summary>
        void ApplyGaitInput(bool hasMoveIntent)
        {
            if (_input != null && _input.WalkTogglePressed)
            {
                _walkMode = !_walkMode;
                if (_walkMode)
                    _sprintArmed = false;
            }

            if (hasMoveIntent && !_walkMode && _input != null && _input.SprintPressed)
                _sprintArmed = !_sprintArmed;

            if (_sprintArmed && !_walkMode && !hasMoveIntent && !IsAirborneLocomotion && _currentSpeed <= StopSpeed)
                _sprintArmed = false;
        }

        /// <summary>按当前步态写目标速度：走 / 慢跑 / 快跑。</summary>
        void ApplyGaitTargetSpeed()
        {
            float walk = _tuning.WalkMoveSpeed > 0.01f ? _tuning.WalkMoveSpeed : _tuning.RunMoveSpeed * 0.5f;
            float jog = Mathf.Max(_tuning.RunMoveSpeed, walk);
            float sprint = _tuning.SprintMoveSpeed > jog ? _tuning.SprintMoveSpeed : jog * 1.5f;

            if (_walkMode)
            {
                _targetSpeed = walk;
                _isRun = false;
                _isWalk = true;
                return;
            }

            if (_sprintArmed)
            {
                _targetSpeed = sprint;
                _isRun = true;
                _isWalk = false;
                return;
            }

            _targetSpeed = jog;
            _isRun = true;
            _isWalk = false;
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

        float ResolveMoveWeight()
        {
            return _gate == null ? 1f : Mathf.Clamp01(_gate.MoveWeight);
        }

        void ApplyHorizontalMove(float moveWeight, bool airborne)
        {
            if (_controller == null)
                return;

            float airControl = airborne ? Mathf.Clamp01(_tuning.AirControl) : 1f;
            if (airborne && airControl <= 0f)
                return;

            if (moveWeight <= 0f)
            {
                if (!airborne)
                    ApplyLocomotionAnim(idle: true, airborne: false);
                return;
            }

            Vector3 worldMoveDir = ResolveHorizontalMoveDir(airborne);
            if (worldMoveDir.sqrMagnitude < 0.0001f && _currentSpeed < 0.01f)
            {
                if (!airborne)
                    ApplyLocomotionAnim(idle: true, airborne: false);
                return;
            }

            UpdateAnimSpeedParam();
            float scale = _time != null ? _time.PlayerScale : 1f;

            float targetSpeed = _targetSpeed * moveWeight;
            if (airborne)
                targetSpeed *= Mathf.Max(0f, _tuning.AirMoveSpeedScale);
            else if (_landSlowRemain > 0f)
            {
                float landScale = _tuning.LandSlowScale > 0.01f ? _tuning.LandSlowScale : 0.55f;
                targetSpeed *= landScale;
            }

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

            ApplyLocomotionAnim(idle: false, airborne: airborne);
        }

        void UpdateAnimSpeedParam()
        {
            if (_isWalk)
                _curMoveSpeedAnim = 0.35f;
            else if (_sprintArmed && _isRun)
                _curMoveSpeedAnim = 1f;
            else if (_isRun)
                _curMoveSpeedAnim = 0.7f;
            else
                _curMoveSpeedAnim = 0f;
        }

        void ApplyLocomotionAnim(bool idle, bool airborne)
        {
            if (_animator == null)
                return;
            if (_canWriteAnimParams != null && !_canWriteAnimParams())
            {
                _lastGaitAnimHash = 0;
                return;
            }

            bool isWalk = !idle && _isWalk;
            bool isRun = !idle && (_isRun || (airborne && _currentSpeed > 0.1f));
            float moveSpeed = idle ? 0f : _curMoveSpeedAnim;
            if (idle)
            {
                isWalk = false;
                isRun = false;
                moveSpeed = 0f;
            }

            _animator.SetFloat(MoveSpeedId, moveSpeed);
            _animator.SetBool(IsRunId, isRun);
            _animator.SetBool(IsWalkId, isWalk);
            _animator.SetBool(IsGroundId, IsGrounded);

            float axisScale = isWalk ? 0.45f : (isRun ? 1f : 0f);
            float mx = 0f;
            float my = 0f;
            if (axisScale > 0f && _root != null && _moveDir.sqrMagnitude > 0.0001f)
            {
                Vector3 local = _root.InverseTransformDirection(_moveDir);
                mx = local.x * axisScale;
                my = local.z * axisScale;
            }

            _animator.SetFloat(MoveXId, mx);
            _animator.SetFloat(MoveYId, my);

            if (!airborne)
                TryCrossFadeGait(isWalk, isRun);
        }

        void TryCrossFadeGait(bool isWalk, bool isRun)
        {
            EnsureAnimStateCache();

            int hash = IdleStateId;
            if (isWalk && _hasWalkState)
                hash = WalkStateId;
            else if (isRun && _hasRunningState)
                hash = RunningStateId;
            else if (isWalk && !_hasWalkState)
                hash = IdleStateId;

            if (hash == RunningStateId && !_hasRunningState)
                return;
            if (hash == IdleStateId && !_hasIdleState)
                return;
            if (hash == _lastGaitAnimHash)
                return;

            _lastGaitAnimHash = hash;
            _animator.CrossFadeInFixedTime(hash, GaitCrossFade, 0, 0f);
        }

        void EnsureAnimStateCache()
        {
            if (_animStatesCached || _animator == null)
                return;
            _hasIdleState = _animator.HasState(0, IdleStateId);
            _hasRunningState = _animator.HasState(0, RunningStateId);
            _hasWalkState = _animator.HasState(0, WalkStateId);
            _animStatesCached = true;
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
