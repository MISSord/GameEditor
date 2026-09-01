using ACTGameEditor.Combat;
using EGamePlay.Unity;
using UnityEngine;

namespace ACTGameEditor.Locomotion
{
    /// <summary>战斗实体可移动门控。</summary>
    public sealed class CombatMoveGate : IMoveGate
    {
        readonly CombatEntity _entity;

        public CombatMoveGate(CombatEntity entity) => _entity = entity;

        /// <inheritdoc />
        public float MoveWeight => _entity != null ? _entity.MoveWeight : 0f;
    }

    /// <summary>战斗实体可跳跃门控。</summary>
    public sealed class CombatJumpGate : IJumpGate
    {
        readonly CombatEntity _entity;

        public CombatJumpGate(CombatEntity entity) => _entity = entity;

        public bool CanJump => _entity != null && _entity.IsCanJump;
    }

    /// <summary>把移动意图报给 CombatStateDirector，不直接写 CurState。</summary>
    public sealed class CombatLocomotionStateSink : ILocomotionStateSink
    {
        readonly CombatEntity _entity;

        public CombatLocomotionStateSink(CombatEntity entity) => _entity = entity;

        public void SetLocomotionState(bool isMoving, bool isRun, bool isWalk)
        {
            if (_entity == null)
                return;
            _entity.StateDirector?.NotifyLocomotion(isMoving, isRun, isWalk);
        }

        public void NotifyJumpStarted()
        {
            if (_entity == null)
                return;
            _entity.StateDirector?.NotifyJumpStarted();
            _entity.GetComponent<AnimComponent>()?.Director?.TryPlayLocomotionJump();
        }

        public void SyncAirborneState(bool isGrounded, bool isFalling)
        {
            _entity?.StateDirector?.SyncAirborne(isGrounded, isFalling);
        }
    }

    /// <summary>锁定时朝向目标，位移仍相机相对（绕圈）。</summary>
    public sealed class CombatLockFacingProvider : IMoveFacingProvider
    {
        /// <inheritdoc />
        public bool TryGetFacingPoint(out Vector3 worldPoint)
        {
            worldPoint = default;
            LockSystem lockSys = LockSystem.Instance;
            if (lockSys == null || !lockSys.IsLocked)
                return false;

            CombatEntity target = lockSys.LockedCombatEntity;
            if (target == null || target.IsDisposed || target.IsDead)
                return false;

            worldPoint = target.Position;
            return true;
        }
    }

    /// <summary>从本帧 <see cref="PlayerInputSnapshot"/> 读移动轴（空安全）。</summary>
    public sealed class ConfigurableInputMoveProvider : IMoveInputProvider
    {
        public Vector2 MoveAxis
        {
            get
            {
                var mgr = ConfigurableInputManager.Instance;
                return mgr != null ? mgr.Snapshot.MoveAxis : Vector2.zero;
            }
        }

        /// <inheritdoc />
        public bool WalkTogglePressed
        {
            get
            {
                var mgr = ConfigurableInputManager.Instance;
                return mgr != null && mgr.WalkTogglePressed;
            }
        }

        /// <inheritdoc />
        public bool SprintPressed
        {
            get
            {
                var mgr = ConfigurableInputManager.Instance;
                return mgr != null && mgr.SprintPressed;
            }
        }
    }
}
