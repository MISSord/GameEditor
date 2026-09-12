using ACTGameEditor.Combat;
using EGamePlay;
using EGamePlay.Unity;
using UnityEngine;
using ACTGameEditor;

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
            bool localControl = entity != null && entity.isTruePlayer;
            IMoveInputProvider input = localControl
                ? ConfigurableInputMoveProvider.Instance
                : IdleMoveInputProvider.Instance;

            motor.SetTuning(tuning);
            motor.Bind(
                controller,
                root,
                animator,
                input,
                new TransformCameraProvider(null),
                new CombatMoveGate(entity),
                new CombatUnitLocomotionTimeSource(entity),
                new CombatLocomotionStateSink(entity));

            motor.SetJumpGate(new CombatJumpGate(entity));
            // 人机稍后由 EnemyBrain 换成目标朝向；先不要跟玩家 LockSystem。
            motor.SetFacingProvider(localControl ? new CombatLockFacingProvider() : null);

            CombatAnimDirector director = anim?.Director;
            if (anim?.Motion != null)
                motor.BindMotion(anim.Motion);

            if (director != null)
            {
                BindMoveIntent(director, localControl, tuning.InputDeadZone);
                motor.SetAnimParamWriteGate(() => !director.HasSkillOwner);
            }
        }

        /// <summary>换人后切换本地输入 / 停步；电机 Enable 仍由 Presence 管。</summary>
        public static void BindLocalControl(CombatEntity entity, bool localControl)
        {
            if (entity == null || entity.IsDisposed)
                return;

            InputMoveComponent move = entity.GetComponent<InputMoveComponent>();
            AnimComponent anim = entity.GetComponent<AnimComponent>();
            if (move == null)
                return;

            IMoveInputProvider input = localControl
                ? ConfigurableInputMoveProvider.Instance
                : IdleMoveInputProvider.Instance;
            move.InstallAiDrivers(
                input,
                new TransformCameraProvider(null),
                localControl ? new CombatLockFacingProvider() : null);

            CombatAnimDirector director = anim?.Director;
            if (director != null)
                BindMoveIntent(director, localControl, director.MoveIntentDeadZone);
        }

        static void BindMoveIntent(CombatAnimDirector director, bool localControl, float deadZone)
        {
            if (localControl)
            {
                director.MoveIntentProvider = () =>
                {
                    var mgr = ConfigurableInputManager.Instance;
                    return mgr != null ? mgr.Snapshot.MoveAxis : Vector2.zero;
                };
            }
            else
            {
                director.MoveIntentProvider = static () => Vector2.zero;
            }

            director.MoveIntentDeadZone = deadZone;
        }
    }
}
