using EGamePlay;
using EGamePlay.Combat;
using EGamePlay.Unity.Locomotion;
using UnityEngine;

namespace EGamePlay.Unity
{
    /// <summary>
    /// 战斗场景 InputMove 装配契约；由 ACTGameEditor 实现并通过 <see cref="GameObjectData"/> 注入。
    /// </summary>
    public interface IInputMoveBinder
    {
        /// <summary>
        /// 配置 LocomotionMotor 与 AnimDirector 的战斗侧依赖（输入、门控、时间源等）。
        /// </summary>
        void Bind(
            LocomotionMotor motor,
            AnimComponent anim,
            CombatEntity entity,
            CharacterController controller,
            Transform root,
            Animator animator,
            PlayerMoveSettingSo playerSetting);
    }
}
