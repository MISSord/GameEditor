using UnityEngine;
using UnityEngine.InputSystem;

namespace ACTGameEditor
{
    public class ConfigurableInputManager : Singleton<ConfigurableInputManager>
    {
        //输入资源
        public InputActionAsset InputActionsAsset;
        public Vector2 PlayerInput = Vector2.zero;

        private string _actionMapName = "ACTPlayer";
        private SimpleJoyStick _joyStick;
        private IAttackPlayer _curPlayer;
        private InputAction _moveAction;

        //改变当前操作的人物
        public void ChangeCurPlayer()
        {
            ActPlayer actPlayer = PlayerManager.Instance.LocalPlayer;
            if(actPlayer != null && actPlayer is IAttackPlayer)
            {
                _curPlayer = actPlayer as IAttackPlayer;
                _curPlayer.ChangeInputMoveState(true);
            }
        }

        //绑定输入监听
        public void InitListener()
        {
            if (InputActionsAsset == null) return;

            InputActionMap map = InputActionsAsset.FindActionMap(_actionMapName);
            if (map == null) return;

            map.FindAction(InputListernType.ButtonX.ToString()).performed += OnButtonXPerform;
            map.FindAction(InputListernType.ButtonY.ToString()).performed += OnButtonYPerform;
            map.FindAction(InputListernType.ButtonA.ToString()).performed += OnButtonAPerform;
            map.FindAction(InputListernType.ButtonB.ToString()).performed += OnButtonBPerform;

            InputAction jumpAction = map.FindAction(InputListernType.Jump.ToString());
            if (jumpAction != null)
                jumpAction.performed += OnJumpPerform;

            //map.FindAction(InputListernType.LongButtonX.ToString()).started += OnLongButtonXStart;
            //map.FindAction(InputListernType.LongButtonX.ToString()).performed += OnLongButtonXPerform;
            //map.FindAction(InputListernType.LongButtonX.ToString()).canceled += OnLongButtonXEnd;

            _moveAction = map.FindAction(InputListernType.Move.ToString());

            _joyStick = SimpleJoyStick.Instance;
        }

        public void Start()
        {
            if (InputActionsAsset == null) return;
            InputActionsAsset.Enable();
        }

        public void Update()
        {
            if (_curPlayer == null)
                return;

            if (_moveAction != null)
                PlayerInput = _moveAction.ReadValue<Vector2>();

            // 摇杆可能未挂场景，需空安全
            if (_joyStick == null)
                _joyStick = SimpleJoyStick.Instance;

            if (_joyStick != null && _joyStick.IsDrag)
                PlayerInput = _joyStick.input;
        }

        public void OnButtonXPerform(InputAction.CallbackContext context)
        {
            if (_curPlayer == null) return;
            _curPlayer.AddInputRecord(InputListernType.ButtonX, PressType.Click, InputCallBackType.Performed);
        }

        public void OnButtonYPerform(InputAction.CallbackContext context)
        {
            if (_curPlayer == null) return;
            _curPlayer.AddInputRecord(InputListernType.ButtonY, PressType.Click, InputCallBackType.Performed);
        }

        public void OnButtonAPerform(InputAction.CallbackContext context)
        {
            if (_curPlayer == null) return;
            _curPlayer.AddInputRecord(InputListernType.ButtonA, PressType.Click, InputCallBackType.Performed);
        }

        public void OnButtonBPerform(InputAction.CallbackContext context)
        {
            if (_curPlayer == null) return;
            _curPlayer.AddInputRecord(InputListernType.ButtonB, PressType.Click, InputCallBackType.Performed);
        }

        public void OnJumpPerform(InputAction.CallbackContext context)
        {
            if (_curPlayer == null)
                return;

            if (_curPlayer is ActPlayer actPlayer)
                actPlayer.TryJump();
        }

        //长按部分
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
