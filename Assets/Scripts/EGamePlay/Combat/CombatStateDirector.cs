namespace EGamePlay.Combat
{
    /// <summary>
    /// 战斗行为态唯一写入口：按优先级槽位合成 CurState。
    /// Dead &gt; Control &gt; Stagger &gt; Hit &gt; Skill &gt; Locomotion(Idle/Moving)。
    /// </summary>
    public sealed class CombatStateDirector
    {
        const byte PriorityLocomotion = 0;
        const byte PrioritySkill = 10;
        const byte PriorityHit = 20;
        const byte PriorityDead = 30;

        ICombatUnit _owner;

        bool _skillActive;
        long _skillSourceId;

        bool _hitActive;
        long _hitSourceId;
        float _hitEndTime;

        bool _controlActive;
        bool _staggerActive;

        bool _dead;

        bool _wantMoving;
        bool _wantRun;
        bool _wantWalk;

        bool _airborne;
        bool _jumpAirborne;

        /// <summary>当前对外可见行为态。</summary>
        public PlayerStateEnum Current { get; private set; } = PlayerStateEnum.Idle;

        /// <summary>是否已进入死亡槽。</summary>
        public bool IsDead => _dead;

        /// <summary>是否处于硬控槽（眩晕等，跟 MoveForbid 时长）。</summary>
        public bool IsControl => _controlActive;

        /// <summary>是否处于失衡硬直槽。冻结盖住外观时此标志仍为 true。</summary>
        public bool IsStagger => _staggerActive;

        /// <summary>绑定实体并同步初始 Idle。</summary>
        public void Bind(ICombatUnit owner)
        {
            _owner = owner;
            Current = PlayerStateEnum.Idle;
            ApplyToOwner();
        }

        /// <summary>解绑。</summary>
        public void Unbind()
        {
            _owner = null;
            _skillActive = false;
            _hitActive = false;
            _controlActive = false;
            _staggerActive = false;
            _dead = false;
            _skillSourceId = 0;
            _hitSourceId = 0;
            _hitEndTime = 0f;
            Current = PlayerStateEnum.Idle;
            _airborne = false;
            _jumpAirborne = false;
            _wantMoving = false;
            _wantRun = false;
            _wantWalk = false;
        }

        /// <summary>技能开轴。同槽后写覆盖（连招顶替）。硬控/失衡/死亡中忽略。</summary>
        public void EnterSkill(long sourceId)
        {
            if (_dead || _controlActive || _staggerActive)
                return;
            _skillActive = true;
            _skillSourceId = sourceId;
            // 开技能清受击槽，避免 Hit 盖住 Skill
            if (_hitActive)
            {
                _hitActive = false;
                _hitSourceId = 0;
                _hitEndTime = 0f;
            }
            Recompute();
        }

        /// <summary>技能轴交回；source 不匹配则忽略（已被新技能顶替）。</summary>
        public void ExitSkill(long sourceId)
        {
            if (!_skillActive || _skillSourceId != sourceId)
                return;
            _skillActive = false;
            _skillSourceId = 0;
            Recompute();
        }

        /// <summary>受击硬直。duration≤0 需手动 ExitHit。硬控/失衡中忽略，避免短硬直冲掉长硬直。</summary>
        public void EnterHit(long sourceId, float durationSeconds = 0.35f)
        {
            if (_dead || _controlActive || _staggerActive)
                return;
            _hitActive = true;
            _hitSourceId = sourceId;
            _hitEndTime = durationSeconds > 0f
                ? CombatTimeClock.GetLayerTime(_owner) + durationSeconds
                : 0f;
            Recompute();
        }

        /// <summary>结束受击。</summary>
        public void ExitHit(long sourceId)
        {
            if (!_hitActive || (_hitSourceId != 0 && _hitSourceId != sourceId))
                return;
            _hitActive = false;
            _hitSourceId = 0;
            _hitEndTime = 0f;
            Recompute();
        }

        /// <summary>候场时清短硬直，避免隐藏后计时冻结、再上场仍硬直。</summary>
        public void ClearHit()
        {
            if (!_hitActive)
                return;
            _hitActive = false;
            _hitSourceId = 0;
            _hitEndTime = 0f;
            Recompute();
        }

        /// <summary>进入死亡（最高优先）。</summary>
        public void EnterDead()
        {
            _dead = true;
            _skillActive = false;
            _skillSourceId = 0;
            _hitActive = false;
            _hitSourceId = 0;
            _hitEndTime = 0f;
            _controlActive = false;
            _staggerActive = false;
            Recompute();
        }

        /// <summary>进入失衡硬直。清短硬直；冻结等 Control 仍可盖住外观，本标志保持。</summary>
        public void EnterStagger()
        {
            if (_dead)
                return;
            _staggerActive = true;
            if (_hitActive)
            {
                _hitActive = false;
                _hitSourceId = 0;
                _hitEndTime = 0f;
            }
            Recompute();
        }

        /// <summary>退出失衡硬直（条掉光或调试清空）。</summary>
        public void ExitStagger()
        {
            if (!_staggerActive)
                return;
            _staggerActive = false;
            Recompute();
        }

        /// <summary>进入硬控。清短硬直；技能槽可仍为 true，合成时 Control 盖住 Skill，直到 Break 后 ExitSkill。</summary>
        public void EnterControl()
        {
            if (_dead)
                return;
            _controlActive = true;
            _hitActive = false;
            _hitSourceId = 0;
            _hitEndTime = 0f;
            Recompute();
        }

        /// <summary>退出硬控（MoveForbid 计数归零）。</summary>
        public void ExitControl()
        {
            if (!_controlActive)
                return;
            _controlActive = false;
            Recompute();
        }

        /// <summary>复活。</summary>
        public void ExitDead()
        {
            if (!_dead)
                return;
            _dead = false;
            Recompute();
        }

        /// <summary>Locomotion 只报意图，不直接写 CurState；地面时才刷新 CurMoveState。</summary>
        public void NotifyLocomotion(bool isMoving, bool isRun, bool isWalk = false)
        {
            _wantMoving = isMoving;
            _wantWalk = isWalk && isMoving;
            _wantRun = isRun && isMoving && !_wantWalk;
            if (_owner != null && !_airborne)
                ApplyGroundMoveState();

            // 仅当行为层落在 Locomotion 时刷新 Idle/Moving
            if (!_dead && !_controlActive && !_staggerActive && !_hitActive && !_skillActive)
                Recompute();
        }

        /// <summary>一段跳成功：CurMoveState 切 Jump，直至落地。</summary>
        public void NotifyJumpStarted()
        {
            if (_dead || _owner == null)
                return;

            _airborne = true;
            _jumpAirborne = true;
            _owner.CurMoveState = MoveTypeEnum.Jump;
        }

        /// <summary>每帧同步空中/落地；主动跳全程保持 Jump，踩空为 Falling。</summary>
        public void SyncAirborne(bool isGrounded, bool isFalling)
        {
            if (_owner == null)
                return;

            if (isGrounded)
            {
                if (_airborne)
                {
                    _airborne = false;
                    _jumpAirborne = false;
                    ApplyGroundMoveState();
                }
                return;
            }

            if (!isFalling)
                return;

            _airborne = true;
            _owner.CurMoveState = _jumpAirborne ? MoveTypeEnum.Jump : MoveTypeEnum.Falling;
        }

        void ApplyGroundMoveState()
        {
            if (!_wantMoving)
                _owner.CurMoveState = MoveTypeEnum.Idle;
            else if (_wantWalk)
                _owner.CurMoveState = MoveTypeEnum.Walk;
            else
                _owner.CurMoveState = MoveTypeEnum.Run;
        }

        /// <summary>推进受击计时（传入宿主层累计时间）。</summary>
        public void Tick(float layerTime)
        {
            if (!_hitActive || _hitEndTime <= 0f)
                return;
            if (layerTime < _hitEndTime)
                return;
            _hitActive = false;
            _hitSourceId = 0;
            _hitEndTime = 0f;
            Recompute();
        }

        void Recompute()
        {
            PlayerStateEnum next;
            if (_dead)
                next = PlayerStateEnum.Dead;
            else if (_controlActive)
                next = PlayerStateEnum.Control;
            else if (_staggerActive)
                next = PlayerStateEnum.Stagger;
            else if (_hitActive)
                next = PlayerStateEnum.Hit;
            else if (_skillActive)
                next = PlayerStateEnum.PlayerSkill;
            else
                next = _wantMoving ? PlayerStateEnum.Moving : PlayerStateEnum.Idle;

            if (next == Current)
                return;
            Current = next;
            ApplyToOwner();
        }

        void ApplyToOwner()
        {
            _owner?.ApplyStateFromDirector(Current);
        }
    }
}
