using EGamePlay.Unity.Locomotion;
using UnityEngine;
using UnityEngine.InputSystem;
namespace ACTGameEditor.Locomotion
{
    /// <summary>
    /// 独立场景用：直接读 InputActionAsset 的 Move，可选摇杆覆盖；不依赖战斗单例刷人。
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class LocomotionInputReader : MonoBehaviour, IMoveInputProvider
    {
        [SerializeField]
        InputActionAsset actionsAsset;

        [SerializeField]
        string actionMapName = "ACTPlayer";

        [SerializeField]
        string moveActionName = "Move";

        [SerializeField]
        bool preferJoystickWhenDragging = true;

        InputActionMap _map;
        InputAction _moveAction;
        SimpleJoyStick _joyStick;

        /// <inheritdoc />
        public Vector2 MoveAxis
        {
            get
            {
                Vector2 axis = Vector2.zero;
                if (_moveAction != null && _moveAction.enabled)
                    axis = _moveAction.ReadValue<Vector2>();

                if (preferJoystickWhenDragging)
                {
                    if (_joyStick == null)
                        _joyStick = SimpleJoyStick.Instance;

                    if (_joyStick != null && _joyStick.IsDrag)
                        axis = _joyStick.input;
                }

                return axis;
            }
        }

        /// <summary>绑定输入资源（可在 Bootstrap 注入）。</summary>
        public void SetActionsAsset(InputActionAsset asset)
        {
            DisableActions();
            actionsAsset = asset;
            BindAndEnable();
        }

        void OnEnable() => BindAndEnable();

        void OnDisable() => DisableActions();

        void BindAndEnable()
        {
            _map = null;
            _moveAction = null;
            if (actionsAsset == null)
                return;

            _map = actionsAsset.FindActionMap(actionMapName, throwIfNotFound: false);
            if (_map == null)
            {
                Debug.LogWarning($"[LocomotionInputReader] 找不到 ActionMap: {actionMapName}", this);
                return;
            }

            _moveAction = _map.FindAction(moveActionName, throwIfNotFound: false);
            if (_moveAction == null)
            {
                Debug.LogWarning($"[LocomotionInputReader] 找不到 Action: {moveActionName}", this);
                return;
            }

            // 整份 Asset Enable 可能被其它系统 Disable；显式开 Map/Action 更稳
            _map.Enable();
            _moveAction.Enable();
        }

        void DisableActions()
        {
            if (_moveAction != null && _moveAction.enabled)
                _moveAction.Disable();
            if (_map != null && _map.enabled)
                _map.Disable();
        }
    }
}
