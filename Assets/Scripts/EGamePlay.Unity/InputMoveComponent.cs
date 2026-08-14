using ACTGameEditor;
using ACTGameEditor.Locomotion;
using UnityEngine;
using XiaoCao;

namespace EGamePlay.Combat
{
    /// <summary>
    /// 输入移动组件：薄桥到 <see cref="LocomotionMotor"/>，保留战斗门控与时间缩放。
    /// </summary>
    public class InputMoveComponent : EGamePlay.Component
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

            Animator animator = _combatEntity.GetComponent<AnimComponent>().animator;
            LocomotionTuning tuning = LocomotionTuning.FromPlayerMoveSetting(data.playerSetting);

            // RootTransform 在 ActPlayer.Init 里于 AddCombatEntity 之后才赋值，此处用 CC.transform
            Transform root = _controller != null ? _controller.transform : null;

            _motor.SetTuning(tuning);
            _motor.Bind(
                _controller,
                root,
                animator,
                new ConfigurableInputMoveProvider(),
                new TransformCameraProvider(null),
                new CombatMoveGate(_combatEntity),
                new GameTimeLocomotionTimeSource(),
                new CombatLocomotionStateSink(_combatEntity));
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
    }
}
