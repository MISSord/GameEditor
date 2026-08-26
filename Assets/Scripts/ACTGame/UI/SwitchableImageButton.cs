using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using ACTGameEditor;

[RequireComponent(typeof(Image))]
[RequireComponent(typeof(Button))]
public class SwitchableImageButton : MonoBehaviour
{
    [Header("Image Settings")]

    [SerializeField]
    private Sprite _initialSprite;

    [SerializeField]
    private Sprite _changedSprite;

    [SerializeField]
    private string _actionMapName = "Player";

    [SerializeField]
    private InputListernType _actionName = InputListernType.Move;

    private Image _targetImage;
    private Button _button;
    private InputAction _triggerAction;

    // 状态追踪
    private bool _isButtonPressed = false;
    private bool _isActionTriggered = false;
    private bool _isChangingSprite = false;

    private void Awake()
    {
        _targetImage = GetComponent<Image>();
        _button = GetComponent<Button>();

        // 设置初始图片
        if (_initialSprite != null && _targetImage != null)
        {
            _targetImage.sprite = _initialSprite;
        }
    }

    private void InitializeInputAction()
    {
        // 查找指定的 Action
        var triggerAction = ConfigurableInputManager.Instance.InputActionsAsset.FindAction(_actionName.ToString());
        if (triggerAction != null)
        {
            // 绑定按下开始与抬起取消事件
            triggerAction.started += OnActionStarted;
            triggerAction.canceled += OnActionCanceled;
        }
        else
        {
            Debug.LogWarning($"Action '{_actionName}' not found in ActionMap '{_actionMapName}'");
        }
    }

    private void Start()
    {
        // 绑定 UI 按钮事件
        SetupButtonEvents();
        // 初始化 Input Action
        InitializeInputAction();
    }

    private void SetupButtonEvents()
    {
        if (_button != null)
        {
            // 使用 EventTrigger 监听按下和抬起事件
            var eventTrigger = _button.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();

            // 按下事件
            var pointerDownEntry = new UnityEngine.EventSystems.EventTrigger.Entry();
            pointerDownEntry.eventID = UnityEngine.EventSystems.EventTriggerType.PointerDown;
            pointerDownEntry.callback.AddListener((data) => OnButtonPointerDown());
            eventTrigger.triggers.Add(pointerDownEntry);

            // 抬起事件
            var pointerUpEntry = new UnityEngine.EventSystems.EventTrigger.Entry();
            pointerUpEntry.eventID = UnityEngine.EventSystems.EventTriggerType.PointerUp;
            pointerUpEntry.callback.AddListener((data) => OnButtonPointerUp());
            eventTrigger.triggers.Add(pointerUpEntry);

            // 取消事件（指针移出等）
            var pointerExitEntry = new UnityEngine.EventSystems.EventTrigger.Entry();
            pointerExitEntry.eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit;
            pointerExitEntry.callback.AddListener((data) => OnButtonPointerExit());
            eventTrigger.triggers.Add(pointerExitEntry);

            //// 进入事件
            //var pointerEnterEntry = new UnityEngine.EventSystems.EventTrigger.Entry();
            //pointerEnterEntry.eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter;
            //pointerEnterEntry.callback.AddListener((data) => OnButtonPointerEnter());
            //eventTrigger.triggers.Add(pointerEnterEntry);
        }
    }

    private void OnButtonPointerDown()
    {
        _isButtonPressed = true;
        SwitchToChangedSprite();
    }

    private void OnButtonPointerUp()
    {
        _isButtonPressed = false;
        TryRevertToInitialSprite();
    }

    private void OnButtonPointerExit()
    {
        // 指针移出时若按钮仍在按下状态，则视为取消按下
        if (_isButtonPressed)
        {
            _isButtonPressed = false;
        }
    }

    //private void OnButtonPointerEnter()
    //{
    //    // 指针进入时若按钮仍在按下状态（被外部打断），则恢复按下状态
    //    // 需要与 PointerExit 配合使用
    //}

    private void OnActionStarted(InputAction.CallbackContext context)
    {
        _isActionTriggered = true;
        SwitchToChangedSprite();
    }

    private void OnActionCanceled(InputAction.CallbackContext context)
    {
        _isActionTriggered = false;
        TryRevertToInitialSprite();
    }

    private void SwitchToChangedSprite()
    {
        if (_changedSprite != null && _targetImage != null)
        {
            _targetImage.sprite = _changedSprite;
            _isChangingSprite = true;
        }
    }

    private void TryRevertToInitialSprite()
    {
        // 只有当按钮没有被按下且动作没有被触发时，才会恢复初始图片
        if (!_isButtonPressed && !_isActionTriggered && _isChangingSprite)
        {
            if (_initialSprite != null && _targetImage != null)
            {
                _targetImage.sprite = _initialSprite;
                _isChangingSprite = false;
            }
        }
    }

    private void OnDisable()
    {
        // 确保恢复初始状态
        _isButtonPressed = false;
        _isActionTriggered = false;
        if (_targetImage != null && _initialSprite != null)
        {
            _targetImage.sprite = _initialSprite;
            _isChangingSprite = false;
        }
    }

    private void OnDestroy()
    {
        // 清理事件绑定
        if (_triggerAction != null)
        {
            _triggerAction.started -= OnActionStarted;
            _triggerAction.canceled -= OnActionCanceled;
        }
    }

    //// 运行时动态手动设置图片
    //public void SetSprites(Sprite newInitialSprite, Sprite newChangedSprite)
    //{
    //    _initialSprite = newInitialSprite;
    //    _changedSprite = newChangedSprite;

    //    // 如果当前没有切换，则设为初始图片
    //    if (!_isChangingSprite && _targetImage != null)
    //    {
    //        _targetImage.sprite = _initialSprite;
    //    }
    //}

    //// 手动触发切换（供外部调用）
    //public void ManualTrigger(bool switchToChanged)
    //{
    //    if (switchToChanged)
    //    {
    //        SwitchToChangedSprite();
    //    }
    //    else
    //    {
    //        _isButtonPressed = false;
    //        _isActionTriggered = false;
    //        TryRevertToInitialSprite();
    //    }
    //}
}
