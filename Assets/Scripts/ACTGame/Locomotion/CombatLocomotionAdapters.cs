using EGamePlay.Combat;
using EGamePlay.Unity.Locomotion;
using UnityEngine;

namespace ACTGameEditor.Locomotion
{
    /// <summary>战斗实体可移动门控。</summary>
    public sealed class CombatMoveGate : IMoveGate
    {
        readonly CombatEntity _entity;

        public CombatMoveGate(CombatEntity entity) => _entity = entity;

        public bool CanMove => _entity != null && _entity.IsCanMove;
    }

    /// <summary>把移动意图报给 CombatStateDirector，不直接写 CurState。</summary>
    public sealed class CombatLocomotionStateSink : ILocomotionStateSink
    {
        readonly CombatEntity _entity;

        public CombatLocomotionStateSink(CombatEntity entity) => _entity = entity;

        public void SetLocomotionState(bool isMoving, bool isRun)
        {
            if (_entity == null)
                return;
            _entity.StateDirector?.NotifyLocomotion(isMoving, isRun);
        }
    }

    /// <summary>从 ConfigurableInputManager 读移动轴（空安全）。</summary>
    public sealed class ConfigurableInputMoveProvider : IMoveInputProvider
    {
        public Vector2 MoveAxis
        {
            get
            {
                var mgr = ConfigurableInputManager.Instance;
                return mgr != null ? mgr.PlayerInput : Vector2.zero;
            }
        }
    }
}
