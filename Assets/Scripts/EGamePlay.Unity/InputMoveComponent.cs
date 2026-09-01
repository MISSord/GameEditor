using UnityEngine;
using ACTGameEditor.Combat;

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
        }

        public override void Update(float deltaTime)
        {
            if (!Enable)
                return;
            _motor.TickUpdate();
        }

        public override void FixedUpdate(float fixDeltaTime)
        {
            if (!Enable)
                return;
            _motor.TickFixed();
        }

        /// <summary>临时关闭重力（技能等调用）。</summary>
        public void SetNoGravityT(float time) => _motor.SetNoGravityT(time);

        /// <summary>设置电机是否允许 AutoRotate。</summary>
        public void SetRotationEnabled(bool enabled) => _motor.FaceEnabled = enabled;

        /// <summary>尝试一段跳。电机未启用时直接失败。</summary>
        public bool TryJump() => Enable && _motor.TryJump();

        /// <summary>鸣潮：闪避锁存快跑（走路模式除外）。</summary>
        public void ArmSprintFromDodge() => _motor.ArmSprintFromDodge();

        /// <summary>当前水平移动方向（世界空间）；无迈步意图时返回 false。</summary>
        public bool TryGetPlanarMoveDir(out Vector3 worldDir)
        {
            worldDir = _motor.MoveDir;
            worldDir.y = 0f;
            if (worldDir.sqrMagnitude < 0.0001f)
                return false;
            worldDir.Normalize();
            return true;
        }

        public override void OnDestroy()
        {
            _motor.ResetRuntimeState();
            _combatEntity = null;
            _controller = null;
        }

        public override void OnReset() => OnDestroy();
    }
}
