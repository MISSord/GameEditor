using System;
using ACTGameEditor;

namespace EGamePlay.Combat
{
    /// <summary>
    /// 战斗行为态唯一写入口：按优先级槽位合成 CurState。
    /// Dead &gt; Hit &gt; Skill &gt; Locomotion(Idle/Moving)。
    /// </summary>
    public sealed class CombatStateDirector
    {
        const byte PriorityLocomotion = 0;
        const byte PrioritySkill = 10;
        const byte PriorityHit = 20;
        const byte PriorityDead = 30;

        CombatEntity _owner;

        bool _skillActive;
        long _skillSourceId;

        bool _hitActive;
        long _hitSourceId;
        float _hitEndTime;

        bool _dead;

        bool _wantMoving;
        bool _wantRun;

        /// <summary>当前对外可见行为态。</summary>
        public PlayerStateEnum Current { get; private set; } = PlayerStateEnum.Idle;

        /// <summary>是否已进入死亡槽。</summary>
        public bool IsDead => _dead;

        /// <summary>绑定实体并同步初始 Idle。</summary>
        public void Bind(CombatEntity owner)
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
            _dead = false;
            _skillSourceId = 0;
            _hitSourceId = 0;
            _hitEndTime = 0f;
            Current = PlayerStateEnum.Idle;
        }

        /// <summary>技能开轴。同槽后写覆盖（连招顶替）。</summary>
        public void EnterSkill(long sourceId)
        {
            if (_dead)
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

        /// <summary>受击硬直。duration≤0 需手动 ExitHit。</summary>
        public void EnterHit(long sourceId, float durationSeconds = 0.35f)
        {
            if (_dead)
                return;
            _hitActive = true;
            _hitSourceId = sourceId;
            _hitEndTime = durationSeconds > 0f
                ? GameTimeManager.PlayerTime + durationSeconds
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

        /// <summary>进入死亡（最高优先）。</summary>
        public void EnterDead()
        {
            _dead = true;
            _skillActive = false;
            _skillSourceId = 0;
            _hitActive = false;
            _hitSourceId = 0;
            _hitEndTime = 0f;
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

        /// <summary>Locomotion 只报意图，不直接写 CurState。</summary>
        public void NotifyLocomotion(bool isMoving, bool isRun)
        {
            _wantMoving = isMoving;
            _wantRun = isRun && isMoving;
            if (_owner != null)
                _owner.CurMoveState = _wantRun ? MoveTypeEnum.Run : MoveTypeEnum.Idle;

            // 仅当行为层落在 Locomotion 时刷新 Idle/Moving
            if (!_dead && !_hitActive && !_skillActive)
                Recompute();
        }

        /// <summary>推进受击计时。</summary>
        public void Tick(float playerTime)
        {
            if (!_hitActive || _hitEndTime <= 0f)
                return;
            if (playerTime < _hitEndTime)
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
            _owner?.SetCurStateFromDirector(Current);
        }
    }
}
