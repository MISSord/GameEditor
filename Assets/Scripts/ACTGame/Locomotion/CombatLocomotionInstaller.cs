using ACTGameEditor.Combat;
using EGamePlay;
using EGamePlay.Unity;
using EGamePlay.Unity.Locomotion;
using UnityEngine;

namespace ACTGameEditor.Locomotion
{
    /// <summary>
    /// 战斗实体 InputMove 默认装配：输入、相机、门控、时间源与 AnimDirector 联动。
    /// </summary>
    public sealed class CombatLocomotionInstaller : IInputMoveBinder
    {
        /// <summary>战斗场景默认装配器。</summary>
        public static readonly CombatLocomotionInstaller Default = new();

        /// <inheritdoc />
        public void Bind(
            LocomotionMotor motor,
            AnimComponent anim,
            CombatEntity entity,
            CharacterController controller,
            Transform root,
            Animator animator,
            PlayerMoveSettingSo playerSetting)
        {
            LocomotionTuning tuning = LocomotionTuningBuilder.FromPlayerMoveSetting(playerSetting);

            motor.SetTuning(tuning);
            motor.Bind(
                controller,
                root,
                animator,
                new ConfigurableInputMoveProvider(),
                new TransformCameraProvider(null),
                new CombatMoveGate(entity),
                new GameTimeLocomotionTimeSource(),
                new CombatLocomotionStateSink(entity));

            motor.SetJumpGate(new CombatJumpGate(entity));
            motor.SetFacingProvider(new CombatLockFacingProvider());

            CombatAnimDirector director = anim?.Director;
            if (anim?.Motion != null)
                motor.BindMotion(anim.Motion);

            if (director != null)
            {
                director.MoveIntentProvider = () =>
                {
                    var mgr = ConfigurableInputManager.Instance;
                    return mgr != null ? mgr.Snapshot.MoveAxis : Vector2.zero;
                };
                director.MoveIntentDeadZone = tuning.InputDeadZone;
                motor.SetAnimParamWriteGate(() => !director.HasSkillOwner);
            }
        }
    }
}
