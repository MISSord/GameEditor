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

    // ״̬׷��
    private bool _isButtonPressed = false;
    private bool _isActionTriggered = false;
    private bool _isChangingSprite = false;

    private void Awake()
    {
        _targetImage = GetComponent<Image>();
        _button = GetComponent<Button>();

        // ���ó�ʼͼƬ
        if (_initialSprite != null && _targetImage != null)
        {
            _targetImage.sprite = _initialSprite;
        }
    }

    private void InitializeInputAction()
    {
        // ����ָ����Action
        var triggerAction = ConfigurableInputManager.Instance.InputActionsAsset.FindAction(_actionName.ToString());
        if (triggerAction != null)
        {
            // ���������Ŀ�ʼ��ȡ���¼�
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
        // ����UI��ť�¼�
        SetupButtonEvents();
        // ��ʼ��Input Action
        InitializeInputAction();
    }

    private void SetupButtonEvents()
    {
        if (_button != null)
        {
            // ʹ��EventTrigger���������º�̧���¼�
            var eventTrigger = _button.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();

            // �����¼�
            var pointerDownEntry = new UnityEngine.EventSystems.EventTrigger.Entry();
            pointerDownEntry.eventID = UnityEngine.EventSystems.EventTriggerType.PointerDown;
            pointerDownEntry.callback.AddListener((data) => OnButtonPointerDown());
            eventTrigger.triggers.Add(pointerDownEntry);

            // ̧���¼�
            var pointerUpEntry = new UnityEngine.EventSystems.EventTrigger.Entry();
            pointerUpEntry.eventID = UnityEngine.EventSystems.EventTriggerType.PointerUp;
            pointerUpEntry.callback.AddListener((data) => OnButtonPointerUp());
            eventTrigger.triggers.Add(pointerUpEntry);

            // ȡ���¼�������Ƴ��ȣ�
            var pointerExitEntry = new UnityEngine.EventSystems.EventTrigger.Entry();
            pointerExitEntry.eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit;
            pointerExitEntry.callback.AddListener((data) => OnButtonPointerExit());
            eventTrigger.triggers.Add(pointerExitEntry);

            //// �����¼�
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
        // �������Ƴ�ʱ��ť���ڰ���״̬�����Ϊ���ٰ���
        if (_isButtonPressed)
        {
            _isButtonPressed = false;
        }
    }

    //private void OnButtonPointerEnter()
    //{
    //    // ����������ʱ��ť���ڰ���״̬�����ⲿ���룩���ָ�����״̬
    //    // ����Ҫ���PointerExitʹ��
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
        // ֻ�е���ťû�б������Ҷ���û�б�����ʱ���Żָ���ʼͼƬ
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
        // ȷ���ָ���ʼ״̬
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
        // �����¼�����
        if (_triggerAction != null)
        {
            _triggerAction.started -= OnActionStarted;
            _triggerAction.canceled -= OnActionCanceled;
        }
    }

    //// �������������ֶ�����ͼƬ
    //public void SetSprites(Sprite newInitialSprite, Sprite newChangedSprite)
    //{
    //    _initialSprite = newInitialSprite;
    //    _changedSprite = newChangedSprite;

    //    // �����ǰû���л�������Ϊ��ʼͼƬ
    //    if (!_isChangingSprite && _targetImage != null)
    //    {
    //        _targetImage.sprite = _initialSprite;
    //    }
    //}

    //// �ֶ������л������ⲿ���ã�
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