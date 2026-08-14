using Cinemachine;
using System.Collections.Generic;
using UnityEngine;

namespace ACTGameEditor
{
    public class CameraManager : MonoBehaviour
    {
        private const float MinDistance = 0.1f;
        private const float ScrollMultiplier = 1.5f;
        public static CameraManager Instance;

        #region CinemaChineCamera
        //private const float _threshold = 0.01f;
        //private float _cinemachineTargetYaw;
        //private float _cinemachineTargetPitch;
        //private Cinemachine3rdPersonFollow VirtualCamera_3rd;
        //private CinemachineBrain CinemachineBrain;
        //private Vector2 look;
        //private Transform CinemachineCameraTarget;

        //public float TopClamp = 70.0f;
        //public float BottomClamp = -30.0f;
        //[Tooltip("额外的降级功能以覆盖摄像头设置。在摄像头被锁定时，可用于微调其位置")]
        //public float CameraAngleOverride = 0.0f;
        //[Tooltip("是否锁定摄像机全部轴")]
        //public bool LockCameraPosition = false;
        #endregion

        [SerializeField]
        public Transform MainCamera;
        [HideInInspector]
        public Camera regularCamera;
        [HideInInspector]
        public Transform focus;
        [HideInInspector]
        public Vector3 focusPoint;
        [HideInInspector]
        public Vector3 previousFocusPoint;
        [HideInInspector]
        public float scroll;
        public float Distance = 5f;

        public bool IsOpenPhysic = false;
        [HideInInspector]
        public ICameraTarget CurrentTarget;

        [SerializeField]
        LayerMask obstructionMask = -1;

        // 当前活跃的状态
        public CameraState CurrentCameraState;
        private Dictionary<CameraEnumState, CameraState> _cameraStatesDic = new Dictionary<CameraEnumState, CameraState>();

        private void Awake()
        {
            Instance = this;

            _cameraStatesDic.Add(CameraEnumState.FreeLook, new FreeLookState(this));
            _cameraStatesDic.Add(CameraEnumState.LockLook, new LockOnState(this));
        }

        private void Start()
        {
            regularCamera = MainCamera.GetComponent<Camera>();
            _halfExtendsDirty = true;
            SwitchState(_cameraStatesDic[CameraEnumState.FreeLook]);

            if (LockSystem.Instance != null)
                LockSystem.Instance.OnLockChanged += OnLockSystemChanged;
        }

        private void OnDisable()
        {
            if (LockSystem.Instance != null)
                LockSystem.Instance.OnLockChanged -= OnLockSystemChanged;
        }

        void OnLockSystemChanged()
        {
            var ls = LockSystem.Instance;
            if (ls == null) return;

            if (ls.IsLocked)
                SwitchState(_cameraStatesDic[CameraEnumState.LockLook]);
            else
                SwitchState(_cameraStatesDic[CameraEnumState.FreeLook]);
        }

        public void ChangeCurFollowTarget(ICameraTarget target)
        {
            if (target == null) return;
            this.CurrentTarget = target;
            this.focus = target.GetCameraTarget();
            this.focusPoint = target.GetCameraTargetPos();

            //CinemachineCameraTarget = target;
            //LockCursor();
        }

        //public void ChangeCurMainCamera(Transform camera)
        //{
        //    if (camera.TryGetComponent<Camera>(out Camera cameraCom))
        //    {
        //        MainCamera = camera;
        //        regularCamera = cameraCom;
        //    }
        //}

        #region CinemaChineCamera

        //private void LocalTrue_OnVCamActive(ICinemachineCamera arg0, ICinemachineCamera arg1)
        //{
        //    CheckCamLookAt(arg0);
        //}

        //private void CheckCamLookAt(ICinemachineCamera camera)
        //{
        //    if (camera != null && camera.VirtualCameraGameObject.CompareTag("PlayerCam"))
        //    {
        //        camera.Follow = CinemachineCameraTarget;
        //        camera.LookAt = CinemachineCameraTarget;
        //    }
        //}

        //private void CheckLocalCam()
        //{
        //    if (VirtualCamera_3rd == null)
        //    {
        //        CinemachineBrain = CinemachineCore.Instance.GetActiveBrain(0);
        //        if (CinemachineBrain.ActiveVirtualCamera != null)
        //        {
        //            CinemachineVirtualCamera virtualCamera = CinemachineBrain.ActiveVirtualCamera as CinemachineVirtualCamera;
        //            virtualCamera.Follow = CinemachineCameraTarget;
        //            VirtualCamera_3rd = virtualCamera.GetCinemachineComponent<Cinemachine3rdPersonFollow>();
        //        }
        //    }
        //    else if(CinemachineBrain != null && CinemachineBrain.ActiveVirtualCamera != null)
        //    {
        //        Debug.LogError("CinemachineCameraTarget");
        //        CinemachineVirtualCamera virtualCamera = CinemachineBrain.ActiveVirtualCamera as CinemachineVirtualCamera;
        //        virtualCamera.Follow = CinemachineCameraTarget;
        //    }
        //}

        //private void CameraRotation()
        //{
        //    if (VirtualCamera_3rd == null || CinemachineBrain.ActiveVirtualCamera.Follow == null)
        //    {
        //        CheckLocalCam();
        //        return;
        //    }

        //    // if there is an input and camera position is not fixed
        //    if (look.sqrMagnitude >= _threshold && !LockCameraPosition)
        //    {
        //        _cinemachineTargetYaw += look.x;
        //        _cinemachineTargetPitch += look.y * -1f;
        //    }
        //    //限制角度在360内
        //    _cinemachineTargetYaw = ClampAngle(_cinemachineTargetYaw, float.MinValue, float.MaxValue);
        //    _cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, BottomClamp, TopClamp);
        //    CinemachineCameraTarget.rotation = Quaternion.Euler(_cinemachineTargetPitch + CameraAngleOverride, _cinemachineTargetYaw, 0.0f);
        //}

        //private static float ClampAngle(float lfAngle, float lfMin, float lfMax)
        //{
        //    if (lfAngle < -360f) lfAngle += 360f;
        //    if (lfAngle > 360f) lfAngle -= 360f;
        //    return Mathf.Clamp(lfAngle, lfMin, lfMax);
        //}

        //private void Update()
        //{
        //    look.x = Input.GetAxis("Mouse X");
        //    look.y = Input.GetAxis("Mouse Y");
        //    scroll = Input.GetAxis("Mouse ScrollWheel");

        //    if (scroll != 0)
        //    {
        //        if (VirtualCamera_3rd)
        //        {
        //            VirtualCamera_3rd.CameraDistance = Mathf.Clamp(VirtualCamera_3rd.CameraDistance - scroll * 1.5f, 4, 10);
        //        }
        //        else
        //        {
        //            Debug.LogError($"yns VirtualCamera_3rd null");
        //        }
        //    }
        //}

        #endregion

        private void Update()
        {
            scroll = Input.GetAxis("Mouse ScrollWheel");

            if (scroll != 0)
            {
                Distance = Mathf.Max(MinDistance, Distance - scroll * ScrollMultiplier);
            }

            CheckEsc();

            // Tab / 鼠标中键：崩坏3风格锁定切换
            if ((Input.GetKeyDown(KeyCode.Tab) || Input.GetMouseButtonDown(2)) && LockSystem.Instance != null)
            {
                LockSystem.Instance.ToggleLock();
            }

            if(this.focus == null || CurrentCameraState == null) return;
            CurrentCameraState.OnUpdate();
            
        }

        private Vector3 _cachedHalfExtends;
        private bool _halfExtendsDirty = true;

        /// <summary>物理碰撞半径，IsOpenPhysic 时按需更新缓存</summary>
        Vector3 CameraHalfExtends
        {
            get
            {
                if (_halfExtendsDirty && regularCamera != null)
                {
                    _halfExtendsDirty = false;
                    float y = regularCamera.nearClipPlane * Mathf.Tan(0.5f * Mathf.Deg2Rad * regularCamera.fieldOfView);
                    _cachedHalfExtends = new Vector3(y * regularCamera.aspect, y, 0f);
                }
                return _cachedHalfExtends;
            }
        }

        private void LateUpdate()
        {
            if (focus == null || CurrentCameraState == null) return;

            CurrentCameraState.OnLateUpdate();

            //按照近平面的位置进行物理碰撞检测
            Vector3 lookDirection = CurrentCameraState.lookRotation * Vector3.forward;
            Vector3 rectOffset = lookDirection * regularCamera.nearClipPlane;
            Vector3 rectPosition = CurrentCameraState.lookPosition + rectOffset;
            Vector3 castFrom = focus.position;
            Vector3 castLine = rectPosition - castFrom;
            float castDistance = castLine.magnitude;
            Vector3 castDirection = castLine / castDistance;

            //物理碰撞检测
            if (IsOpenPhysic == true && Physics.BoxCast(
                castFrom, CameraHalfExtends, castDirection, out RaycastHit hit,
                CurrentCameraState.lookRotation, castDistance, obstructionMask
            ))
            {
                rectPosition = castFrom + castDirection * hit.distance;
                CurrentCameraState.lookPosition = rectPosition - rectOffset;
            }

            MainCamera.SetPositionAndRotation(CurrentCameraState.lookPosition, CurrentCameraState.lookRotation);
        }

        private void CheckEsc()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (Cursor.lockState == CursorLockMode.None)
                {
                    LockCursor();
                }
                else
                {
                    UnlockCursor();
                }
            }
        }

        private void LockCursor()
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }

        private void UnlockCursor()
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        // 切换状态的核心方法
        public void SwitchState(CameraState newState)
        {
            CameraState oldState = null;
            if (CurrentCameraState != null)
            {
                oldState = CurrentCameraState;
                oldState.OnExit();
            }

            CurrentCameraState = newState;
            CurrentCameraState.OnEnter(oldState);
        }

    }
}
