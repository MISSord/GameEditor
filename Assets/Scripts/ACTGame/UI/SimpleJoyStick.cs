using ACTGameEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public enum JoystickType
{
    Fixed,
    Floating,
    Dynamic
}

public class SimpleJoyStick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    private RectTransform _baseRect = null;
    public static SimpleJoyStick Instance;
    public JoystickType joystickType = JoystickType.Fixed;
    public RectTransform background = null;
    public RectTransform handle = null;
    public Canvas canvas;
    public Camera UICamera;
    public float MoveThreshold;
    private float _deadZone = 0;
    public Vector2 input = Vector2.zero;
    private Vector2 _center = new Vector2(0.5f, 0.5f);
    private Vector2 _fixedPosition = Vector2.zero;
    private Vector2 _radius;
    private InputAction _moveAction;
    public bool IsDrag { get; private set; } = false;

    // 缓存当前使用的相机，避免每帧 GetComponent
    private Camera _cachedCamera;

    private void Awake()
    {
        if(Instance != null)
        {
            Debug.LogError($"这个类只能有一个，检测代码和预制体{this.gameObject.name}");
            return;
        }
        Instance = this;
    }

    protected virtual void Start()
    {
        _baseRect = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();

        // 优先使用 UICamera；否则从 Canvas 或自身获取一次并缓存
        _cachedCamera = UICamera;
        if (_cachedCamera == null)
        {
            if (canvas != null && canvas.worldCamera != null)
            {
                _cachedCamera = canvas.worldCamera;
            }
            else
            {
                _cachedCamera = GetComponent<Camera>();
            }
        }

        background.pivot = _center;
        handle.anchorMin = _center;
        handle.anchorMax = _center;
        handle.pivot = _center;
        handle.anchoredPosition = Vector2.zero;
        _fixedPosition = background.anchoredPosition;
        _radius = background.sizeDelta / 2;
        SetMode();

        IsDrag = false;
        _moveAction = ConfigurableInputManager.Instance.InputActionsAsset.FindAction("Move");
    }

    public void SetMode()
    {
        if (joystickType == JoystickType.Fixed)
        {
            background.anchoredPosition = _fixedPosition;
            background.gameObject.SetActive(true);
        }
        else
            background.gameObject.SetActive(false);
    }

    public void Update()
    {
        if (IsDrag == true) return;
        if (_moveAction == null) return;
        input = _moveAction.ReadValue<Vector2>();
        HandleInput(input.magnitude, input.normalized, _radius);
        handle.anchoredPosition = input * _radius;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (joystickType != JoystickType.Fixed)
        {
            background.anchoredPosition = ScreenPointToAnchoredPosition(eventData.position);
            background.gameObject.SetActive(true);
        }
        IsDrag = true;
        OnDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_cachedCamera == null) return;

        Vector2 position = _cachedCamera.WorldToScreenPoint(background.position);
        input = (eventData.position - position) / (_radius * canvas.scaleFactor);
        HandleInput(input.magnitude, input.normalized, _radius);
        handle.anchoredPosition = input * _radius;                              
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (joystickType != JoystickType.Fixed)
            background.gameObject.SetActive(false);
        input = Vector2.zero;
        handle.anchoredPosition = Vector2.zero;
        IsDrag = false;
    }

    public void HandleInput(float magnitude, Vector2 normalised, Vector2 radius)
    {
        if (joystickType == JoystickType.Dynamic && magnitude > MoveThreshold)
        {
            Vector2 difference = normalised * (magnitude - MoveThreshold) * radius;
            background.anchoredPosition += difference;
        }
        if (magnitude > _deadZone)
        {
            if (magnitude > 1)
                input = normalised;
        }
        else
        {
            input = Vector2.zero;
        }
    }

    private Vector2 ScreenPointToAnchoredPosition(Vector2 screenPosition)
    {
        Vector2 localPoint = Vector2.zero;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_baseRect, screenPosition, _cachedCamera, out localPoint);
        return localPoint;
    }
}
