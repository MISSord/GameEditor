using UnityEngine;
using UnityEngine.InputSystem;
using ACTGameEditor.Combat;
using ACTGameEditor.Locomotion;

namespace ACTGameEditor
{
    /// <summary>本地玩家输入总管：每帧 Sample 一次，其余只读快照。</summary>
    public class ConfigurableInputManager : Singleton<ConfigurableInputManager>
    {
        public InputActionAsset InputActionsAsset;
        public Vector2 PlayerInput = Vector2.zero;

        private string _actionMapName = "ACTPlayer";
        private SimpleJoyStick _joyStick;
        private IAttackPlayer _curPlayer;
        private InputAction _moveAction;
        private InputAction _walkToggleAction;
        private InputAction _sprintAction;
        private InputAction _switch1Action;
        private InputAction _switch2Action;
        int _sampledFrame = -1;
        PlayerInputSnapshot _snapshot;

        /// <summary>本帧设备快照。Sample 之前是上一帧（或默认值）。</summary>
        public PlayerInputSnapshot Snapshot => _snapshot;

        public void ChangeCurPlayer()
        {
            ActPlayer actPlayer = PlayerManager.Instance.LocalPlayer;
            if (actPlayer != null && actPlayer is IAttackPlayer)
            {
                _curPlayer = actPlayer as IAttackPlayer;
                _curPlayer.ChangeInputMoveState(true);
            }
        }

        public void InitListener()
        {
            if (InputActionsAsset == null) return;

            InputActionMap map = InputActionsAsset.FindActionMap(_actionMapName);
            if (map == null) return;

            BindCombatButton(map, InputListernType.ButtonX, OnButtonXStarted, OnButtonXCanceled);
            BindCombatButton(map, InputListernType.ButtonY, OnButtonYStarted, OnButtonYCanceled);
            BindCombatButton(map, InputListernType.ButtonA, OnButtonAStarted, OnButtonACanceled);
            BindCombatButton(map, InputListernType.ButtonB, OnButtonBStarted, OnButtonBCanceled);

            InputAction jumpAction = map.FindAction(InputListernType.Jump.ToString());
            if (jumpAction != null)
                jumpAction.performed += OnJumpPerform;

            _moveAction = map.FindAction(InputListernType.Move.ToString());
            _walkToggleAction = map.FindAction(InputListernType.WalkToggle.ToString());
            _sprintAction = map.FindAction(InputListernType.Sprint.ToString());
            _walkToggleAction?.Enable();
            _sprintAction?.Enable();

            _switch1Action = map.FindAction(InputListernType.Switch1.ToString())
                ?? map.FindAction(InputListernType.ButtonZ.ToString());
            _switch2Action = map.FindAction(InputListernType.Switch2.ToString())
                ?? map.FindAction(InputListernType.ButtonZ2.ToString());
            if (_switch1Action != null)
            {
                _switch1Action.performed += OnSwitch1Perform;
                _switch1Action.Enable();
            }
            if (_switch2Action != null)
            {
                _switch2Action.performed += OnSwitch2Perform;
                _switch2Action.Enable();
            }

            _joyStick = SimpleJoyStick.Instance;
        }

        static void BindCombatButton(
            InputActionMap map,
            InputListernType type,
            System.Action<InputAction.CallbackContext> started,
            System.Action<InputAction.CallbackContext> canceled)
        {
            InputAction action = map.FindAction(type.ToString());
            if (action == null)
                return;
            action.started += started;
            action.canceled += canceled;
        }

        /// <summary>Ctrl：本帧是否按下（走路/慢跑切换）。读快照，不现场查 InputAction。</summary>
        public bool WalkTogglePressed => _snapshot.WalkTogglePressed;

        /// <summary>Shift：本帧是否按下（慢跑中切入快跑）。读快照，不现场查 InputAction。</summary>
        public bool SprintPressed => _snapshot.SprintPressed;

        /// <summary>当帧移动轴。只返回快照，不会在中途重新采设备。</summary>
        public Vector2 ReadMoveAxis() => _snapshot.MoveAxis;

        /// <summary>
        /// 本帧唯一设备采样。合并 WASD 与摇杆，写入快照并更新闪避 LastAim。
        /// 同一 <see cref="Time.frameCount"/> 重复调用直接返回。
        /// </summary>
        public void Sample()
        {
            int frame = Time.frameCount;
            if (_sampledFrame == frame)
                return;

            if (_joyStick == null)
                _joyStick = SimpleJoyStick.Instance;

            Vector2 keyboard = _moveAction != null ? _moveAction.ReadValue<Vector2>() : Vector2.zero;
            Vector2 axis = keyboard;
            if (_joyStick != null && _joyStick.IsDrag && _joyStick.input.sqrMagnitude > 0.01f)
                axis = _joyStick.input;

            bool walkToggle = _walkToggleAction != null && _walkToggleAction.WasPressedThisFrame();
            bool sprint = _sprintAction != null && _sprintAction.WasPressedThisFrame();

            _snapshot = new PlayerInputSnapshot(axis, walkToggle, sprint, frame);
            _sampledFrame = frame;
            PlayerInput = axis;
            CameraRelativeMove.LatchFromAxis(axis);
        }

        public void Start()
        {
            if (InputActionsAsset == null) return;
            InputActionsAsset.Enable();
        }

        /// <summary>战斗 Tick 入口调用；无角色时也要采轴，避免快照停在上一帧。</summary>
        public void Update()
        {
            Sample();
            PollSwitchFallback();
        }

        void OnSwitch1Perform(InputAction.CallbackContext context)
        {
            CombatSquad.Instance?.TrySwitchRelative(1);
        }

        void OnSwitch2Perform(InputAction.CallbackContext context)
        {
            CombatSquad.Instance?.TrySwitchRelative(2);
        }

        /// <summary>InputAction 未配到 Q/E 时的键盘兜底，不进技能槽。</summary>
        void PollSwitchFallback()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;
            if (_switch1Action == null && keyboard.qKey.wasPressedThisFrame)
                CombatSquad.Instance?.TrySwitchRelative(1);
            if (_switch2Action == null && keyboard.eKey.wasPressedThisFrame)
                CombatSquad.Instance?.TrySwitchRelative(2);
        }

        /// <summary>键盘 / 屏上技能钮共用：写入当前玩家预输入。</summary>
        public void NotifySkillInput(
            InputListernType cmd,
            PressType type = PressType.Click,
            InputCallBackType callBackType = InputCallBackType.Performed)
        {
            if (_curPlayer == null)
                return;
            _curPlayer.AddInputRecord(cmd, type, callBackType);
        }

        void NotifyCombatPress(InputListernType cmd, InputCallBackType phase)
        {
            if (_curPlayer == null)
                return;
            _curPlayer.NotifyCombatPress(cmd, phase);
        }

        public void OnButtonXStarted(InputAction.CallbackContext context) =>
            NotifyCombatPress(InputListernType.ButtonX, InputCallBackType.Started);

        public void OnButtonXCanceled(InputAction.CallbackContext context) =>
            NotifyCombatPress(InputListernType.ButtonX, InputCallBackType.Canceled);

        public void OnButtonYStarted(InputAction.CallbackContext context) =>
            NotifyCombatPress(InputListernType.ButtonY, InputCallBackType.Started);

        public void OnButtonYCanceled(InputAction.CallbackContext context) =>
            NotifyCombatPress(InputListernType.ButtonY, InputCallBackType.Canceled);

        public void OnButtonAStarted(InputAction.CallbackContext context) =>
            NotifyCombatPress(InputListernType.ButtonA, InputCallBackType.Started);

        public void OnButtonACanceled(InputAction.CallbackContext context) =>
            NotifyCombatPress(InputListernType.ButtonA, InputCallBackType.Canceled);

        public void OnButtonBStarted(InputAction.CallbackContext context) =>
            NotifyCombatPress(InputListernType.ButtonB, InputCallBackType.Started);

        public void OnButtonBCanceled(InputAction.CallbackContext context) =>
            NotifyCombatPress(InputListernType.ButtonB, InputCallBackType.Canceled);

        public void OnButtonXPerform(InputAction.CallbackContext context)
        {
            NotifySkillInput(InputListernType.ButtonX);
        }

        public void OnButtonYPerform(InputAction.CallbackContext context)
        {
            NotifySkillInput(InputListernType.ButtonY);
        }

        public void OnButtonAPerform(InputAction.CallbackContext context)
        {
            NotifySkillInput(InputListernType.ButtonA);
        }

        public void OnButtonBPerform(InputAction.CallbackContext context)
        {
            NotifySkillInput(InputListernType.ButtonB);
        }

        public void OnJumpPerform(InputAction.CallbackContext context)
        {
            if (_curPlayer == null)
                return;

            if (_curPlayer is ActPlayer actPlayer)
                actPlayer.TryJump();
        }

        public void OnLongButtonXStart(InputAction.CallbackContext context)
        {
        }

        public void OnLongButtonXPerform(InputAction.CallbackContext context)
        {
        }

        public void OnLongButtonXEnd(InputAction.CallbackContext context)
        {
        }
    }
}
