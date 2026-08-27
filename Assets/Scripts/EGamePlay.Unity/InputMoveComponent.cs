using EGamePlay;
using EGamePlay.Combat;
using EGamePlay.Unity.Locomotion;
using UnityEngine;
using ACTGameEditor.Combat;

namespace EGamePlay.Unity
{
    /// <summary>
    /// 输入移动组件：薄桥到 <see cref="LocomotionMotor"/>；战斗依赖由 <see cref="IInputMoveBinder"/> 注入。
    /// </summary>
    public class InputMoveComponent : Component
    {
        public override bool IsNeedFixedUpdate { get; protected set; } = true;
        public override bool IsNeedUpdate { get; protected set; } = true;
        public override bool DefaultEnable { get; set; } = false;

        readonly LocomotionMotor _motor = new();
        CombatEntity _combatEntity;
        CharacterController _controller;

        /// <summary>是否落地。</summary>
        public bool IsGrounded => _motor.IsGrounded;

        public override void Awake(object initData)
        {
            var data = (GameObjectData)initData;
            _controller = data.controller;
            _combatEntity = GetEntity<CombatEntity>();

            AnimComponent anim = _combatEntity.GetComponent<AnimComponent>();
            Transform root = _controller != null ? _controller.transform : null;
            Animator animator = anim?.animator;

            data.inputMoveBinder?.Bind(
                _motor,
                anim,
                _combatEntity,
                _controller,
                root,
                animator,
                data.playerSetting);

            _motor.LocomotionEnabled = Enable;
        }

        public override void Update(float deltaTime)
        {
            _motor.LocomotionEnabled = Enable;
            _motor.TickUpdate();
        }

        public override void FixedUpdate(float fixDeltaTime)
        {
            _motor.LocomotionEnabled = Enable;
            _motor.TickFixed();
        }

        /// <summary>临时关闭重力（技能等调用）。</summary>
        public void SetNoGravityT(float time) => _motor.SetNoGravityT(time);

        /// <summary>设置是否允许旋转。</summary>
        public void SetRotationEnabled(bool enabled) => _motor.RotationEnabled = enabled;

        /// <summary>尝试一段跳。</summary>
        public bool TryJump() => _motor.TryJump();

        public override void OnDestroy()
        {
            _motor.LocomotionEnabled = false;
            _combatEntity = null;
            _controller = null;
        }

        public override void OnReset() => OnDestroy();
    }
}
