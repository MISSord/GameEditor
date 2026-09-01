using ACTGameEditor;
using ACTGameEditor.Locomotion;
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
    /// <summary>手指移动超过该像素才从「点按方向」切到拖动采样，避免浮动摇杆瞬移后把轴清成 0。</summary>
    const float DragSlopSqr = 64f;

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

    Camera _cachedCamera;
    bool _holdTapAxis;
    Vector2 _pointerDownScreen;

    private void Awake()
    {
        if (Instance != null)
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

        _cachedCamera = UICamera;
        if (_cachedCamera == null)
        {
            if (canvas != null && canvas.worldCamera != null)
                _cachedCamera = canvas.worldCamera;
            else
                _cachedCamera = GetComponent<Camera>();
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

    /// <summary>点按：相对整块触摸区中心取样，再按需把浮动底盘挪到手指。</summary>
    public void OnPointerDown(PointerEventData eventData)
    {
        RectTransform tapRect = _baseRect != null ? _baseRect : (RectTransform)transform;
        if (TryPointerToAxis(eventData, tapRect, out Vector2 tapAxis))
            ApplyAxis(tapAxis);

        if (joystickType != JoystickType.Fixed)
        {
            background.anchoredPosition = ScreenPointToAnchoredPosition(eventData.position, eventData);
            background.gameObject.SetActive(true);
            _holdTapAxis = true;
            _pointerDownScreen = eventData.position;
        }
        else
        {
            _holdTapAxis = false;
        }

        IsDrag = true;
    }

    /// <summary>拖动：超过像素阈值后改从底盘中心做模拟量。</summary>
    public void OnDrag(PointerEventData eventData)
    {
        if (_holdTapAxis)
        {
            Vector2 delta = eventData.position - _pointerDownScreen;
            if (delta.sqrMagnitude < DragSlopSqr)
                return;
            _holdTapAxis = false;
        }

        RectTransform analog = background != null ? background : _baseRect;
        if (!TryPointerToAxis(eventData, analog, out Vector2 axis))
            return;
        ApplyAxis(axis);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (joystickType != JoystickType.Fixed)
            background.gameObject.SetActive(false);
        input = Vector2.zero;
        handle.anchoredPosition = Vector2.zero;
        IsDrag = false;
        _holdTapAxis = false;
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

    void ApplyAxis(Vector2 axis)
    {
        input = axis;
        HandleInput(input.magnitude, input.sqrMagnitude > 0.0001f ? input.normalized : Vector2.zero, _radius);
        handle.anchoredPosition = input * _radius;
        CameraRelativeMove.LatchFromAxis(input);
    }

    bool TryPointerToAxis(PointerEventData eventData, RectTransform relativeTo, out Vector2 axis)
    {
        axis = default;
        if (eventData == null || relativeTo == null)
            return false;

        Camera cam = ResolveEventCamera(eventData);
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                relativeTo, eventData.position, cam, out Vector2 local))
            return false;

        Vector2 half = relativeTo.rect.size * 0.5f;
        if (Mathf.Abs(half.x) < 0.01f || Mathf.Abs(half.y) < 0.01f)
            return false;

        axis = new Vector2(local.x / half.x, local.y / half.y);
        return true;
    }

    Camera ResolveEventCamera(PointerEventData eventData)
    {
        if (eventData != null && eventData.pressEventCamera != null)
            return eventData.pressEventCamera;

        if (canvas == null)
            canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            return _cachedCamera;

        if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            return null;

        if (canvas.worldCamera != null)
            return canvas.worldCamera;
        return _cachedCamera;
    }

    Vector2 ScreenPointToAnchoredPosition(Vector2 screenPosition, PointerEventData eventData)
    {
        Vector2 localPoint = Vector2.zero;
        Camera cam = ResolveEventCamera(eventData);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_baseRect, screenPosition, cam, out localPoint);
        return localPoint;
    }
}
