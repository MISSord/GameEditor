using Cinemachine;
using System.Collections.Generic;
using UnityEngine;
using EGamePlay;

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

        [Tooltip("镜头距离平滑时间（秒）；0 = 滚轮即时生效。")]
        public float DistanceSmoothTime = 0.1f;

        [Tooltip("镜头距离上限（滚轮 / 意图请求统一 clamp）。")]
        public float MaxDistance = 12f;

        // ── 相机意图层：滚轮写距离目标；状态提议朝向；技能/调试提交 CameraIntent 接管角度或距离 ──
        [Tooltip("意图接管/归还的默认平滑时间（秒）。")]
        public float IntentBlendTime = 0.25f;

        float _distanceTarget;
        float _distanceVelocity;

        // 仲裁后的实际朝向（意图接管时平滑逼近；无意图时每帧直贴状态提议，正常手感零延迟）
        float _actualPitch, _actualYaw;
        float _pitchVelocity, _yawVelocity;
        float _releaseRemain;
        float _releaseBlendTime;

        readonly List<CameraIntentEntry> _intents = new List<CameraIntentEntry>(4);

        public bool IsOpenPhysic = false;
        [HideInInspector]
        public ICameraTarget CurrentTarget;

        [SerializeField]
        LayerMask obstructionMask = -1;

        // 当前活跃的状态
        public CameraState CurrentCameraState;
        private Dictionary<CameraEnumState, CameraState> _cameraStatesDic = new Dictionary<CameraEnumState, CameraState>();

        /// <summary>未叠震屏的干净朝向；状态机读回基准用它，避免震屏偏移被烤进轨道基准。</summary>
        public Quaternion CleanLookRotation { get; private set; }

        private void Awake()
        {
            Instance = this;

            _distanceTarget = Distance;

            _cameraStatesDic.Add(CameraEnumState.FreeLook, new FreeLookState(this));
            _cameraStatesDic.Add(CameraEnumState.LockLook, new LockOnState(this));
        }

        private void Start()
        {
            regularCamera = MainCamera.GetComponent<Camera>();
            _restFov = regularCamera.fieldOfView;
            _halfExtendsDirty = true;
            SwitchState(_cameraStatesDic[CameraEnumState.FreeLook]);
            CleanLookRotation = CurrentCameraState != null ? CurrentCameraState.lookRotation : Quaternion.identity;

            Vector3 initEuler = CurrentCameraState != null ? CurrentCameraState.lookRotation.eulerAngles : Vector3.zero;
            _actualPitch = initEuler.x > 180f ? initEuler.x - 360f : initEuler.x;
            _actualYaw = initEuler.y;

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

        /// <summary>
        /// 距离意图结算：栈内最高优先级距离意图覆盖滚轮目标；实际距离 SmoothDamp 逼近（"改动多少"在这一层）。
        /// </summary>
        void UpdateDistance(float dt)
        {
            float target = _distanceTarget;
            float smooth = DistanceSmoothTime;
            if (TryGetTopDistanceIntent(out float distTarget, out float distBlend))
            {
                target = Mathf.Clamp(distTarget, MinDistance, MaxDistance);
                smooth = distBlend;
            }

            Distance = Mathf.SmoothDamp(Distance, target, ref _distanceVelocity, Mathf.Max(0.001f, smooth), float.MaxValue, dt);
        }

        /// <summary>
        /// 提交相机意图：同来源单槽（低优先级不覆盖，语义同 TimeScaleEffectManager）；
        /// 角度意图按优先级接管朝向，距离意图覆盖滚轮目标，接管/归还均按 BlendTime 平滑。
        /// 带 Duration 的意图过期自动归还，无需手动 Cancel（打断不会忘了还镜头）。
        /// </summary>
        public bool SubmitIntent(in CameraIntent intent)
        {
            if (intent.Source == CameraIntentSource.None)
                return false;

            for (int i = 0; i < _intents.Count; i++)
            {
                CameraIntentEntry entry = _intents[i];
                if (entry.Source == intent.Source)
                {
                    if (intent.Priority < entry.Priority)
                        return false;
                    entry.Priority = intent.Priority;
                    entry.BlendTime = intent.BlendTime;
                    entry.Duration = intent.Duration;
                    entry.Elapsed = 0f;
                    entry.LookPoint = intent.LookPoint;
                    entry.Distance = intent.Distance;
                    return true;
                }
            }

            _intents.Add(new CameraIntentEntry
            {
                Source = intent.Source,
                Priority = intent.Priority,
                BlendTime = intent.BlendTime,
                Duration = intent.Duration,
                LookPoint = intent.LookPoint,
                Distance = intent.Distance,
            });
            return true;
        }

        /// <summary>取消来源的意图；priority &gt; 0 时仅取消同优先级条目（低优先级释放不影响高者）。</summary>
        public void CancelIntent(CameraIntentSource source, int priority = 0)
        {
            for (int i = _intents.Count - 1; i >= 0; i--)
            {
                CameraIntentEntry entry = _intents[i];
                if (entry.Source != source)
                    continue;
                if (priority <= 0 || entry.Priority == priority)
                    _intents.RemoveAt(i);
            }
        }

        /// <summary>意图计时推进 + 过期剔除（unscaled，暂停冻结，与镜头层一致）。</summary>
        void TickIntents(float dt)
        {
            for (int i = _intents.Count - 1; i >= 0; i--)
            {
                CameraIntentEntry entry = _intents[i];
                if (entry.Duration <= 0f)
                    continue;
                entry.Elapsed += dt;
                if (entry.Expired)
                    _intents.RemoveAt(i);
            }
        }

        bool TryGetTopLookIntent(out Vector3 lookPoint, out float blendTime)
        {
            lookPoint = default;
            blendTime = IntentBlendTime;
            CameraIntentEntry best = null;
            for (int i = 0; i < _intents.Count; i++)
            {
                CameraIntentEntry entry = _intents[i];
                if (entry.LookPoint == null)
                    continue;
                if (best == null || entry.Priority > best.Priority)
                    best = entry;
            }

            if (best == null)
                return false;
            lookPoint = best.LookPoint.Value;
            if (best.BlendTime > 0f)
                blendTime = best.BlendTime;
            return true;
        }

        bool TryGetTopDistanceIntent(out float distance, out float blendTime)
        {
            distance = 0f;
            blendTime = DistanceSmoothTime;
            CameraIntentEntry best = null;
            for (int i = 0; i < _intents.Count; i++)
            {
                CameraIntentEntry entry = _intents[i];
                if (entry.Distance <= 0f)
                    continue;
                if (best == null || entry.Priority > best.Priority)
                    best = entry;
            }

            if (best == null)
                return false;
            distance = best.Distance;
            if (best.BlendTime > 0f)
                blendTime = best.BlendTime;
            return true;
        }

        /// <summary>
        /// 请求镜头距离（Stage 0 API，现走意图栈）：同源低优先级不覆盖；
        /// smoothTime &gt; 0 用该值，否则用 DistanceSmoothTime。
        /// </summary>
        public void RequestDistance(float target, int priority = 1, float smoothTime = -1f)
        {
            SubmitIntent(new CameraIntent(CameraIntentSource.SkillCamera, null, target, priority,
                smoothTime > 0f ? smoothTime : -1f));
        }

        /// <summary>释放距离请求：仅当来源条目优先级与请求者一致时归还给滚轮目标。</summary>
        public void ReleaseDistance(int priority = 1)
        {
            CancelIntent(CameraIntentSource.SkillCamera, priority);
        }

        /// <summary>
        /// 角度仲裁：最高优先级角度意图接管朝向；无意图且不在归还期时直贴状态提议（正常手感零延迟）。
        /// 位置锚点统一为 focusPoint（= 相机目标点）：FreeLook 本就如此，LockOn 视觉高度会轻微上移对齐。
        /// </summary>
        void UpdateAngleArbitration(float dt)
        {
            if (TryGetTopLookIntent(out Vector3 lookPoint, out float blendTime))
            {
                Vector3 dir = lookPoint - focusPoint;
                if (dir.sqrMagnitude > 0.0001f)
                {
                    dir.Normalize();
                    Vector3 euler = Quaternion.LookRotation(dir, Vector3.up).eulerAngles;
                    _releaseRemain = blendTime;
                    _releaseBlendTime = blendTime;
                    SmoothAngleTo(euler.x > 180f ? euler.x - 360f : euler.x, euler.y, blendTime, dt);
                    return;
                }
            }

            Vector3 baseEuler = CurrentCameraState.lookRotation.eulerAngles;
            float basePitch = baseEuler.x > 180f ? baseEuler.x - 360f : baseEuler.x;

            if (_releaseRemain > 0f)
            {
                _releaseRemain -= dt;
                SmoothAngleTo(basePitch, baseEuler.y, _releaseBlendTime, dt);
                return;
            }

            _actualPitch = basePitch;
            _actualYaw = baseEuler.y;
            _pitchVelocity = 0f;
            _yawVelocity = 0f;
        }

        void SmoothAngleTo(float pitchTarget, float yawTarget, float blendTime, float dt)
        {
            float smooth = Mathf.Max(0.001f, blendTime);
            _actualPitch = Mathf.SmoothDampAngle(_actualPitch, pitchTarget, ref _pitchVelocity, smooth, float.MaxValue, dt);
            _actualYaw = Mathf.SmoothDampAngle(_actualYaw, yawTarget, ref _yawVelocity, smooth, float.MaxValue, dt);
        }

        sealed class CameraIntentEntry
        {
            public CameraIntentSource Source;
            public int Priority;
            public float BlendTime;
            public float Duration;
            public float Elapsed;
            public Vector3? LookPoint;
            public float Distance;

            public bool Expired => Duration > 0f && Elapsed >= Duration;
        }

        private void Update()
        {
            scroll = Input.GetAxis("Mouse ScrollWheel");

            if (scroll != 0)
            {
                // 滚轮是"用户意图"：只写目标值，实际距离在 UpdateDistance 平滑逼近
                _distanceTarget = Mathf.Clamp(_distanceTarget - scroll * ScrollMultiplier, MinDistance, MaxDistance);
            }

            // 意图栈推进（过期剔除；暂停冻结，与镜头层一致）
            TickIntents(TimeScaleEffectManager.IsGameplayPaused ? 0f : Time.unscaledDeltaTime);

            // 距离意图每帧结算（镜头层时钟；本方法在 GameTimeManager.Tick 之前，取上帧 CameraDelta 对平滑量无感）
            UpdateDistance(GameTimeManager.CameraDelta);

            CheckEsc();

            // Tab / 鼠标中键：崩坏3风格锁定切换
            if ((Input.GetKeyDown(KeyCode.Tab) || Input.GetMouseButtonDown(2)) && LockSystem.Instance != null)
            {
                LockSystem.Instance.ToggleLock();
            }

            // 调试热键（F7 技能镜头 / F8 震屏 / F9 时空断裂等）已统一移到 SkillEditorScene，此处只保留游戏性输入

            if(this.focus == null || CurrentCameraState == null) return;
            CurrentCameraState.OnUpdate();
            
        }

        private Vector3 _cachedHalfExtends;
        private bool _halfExtendsDirty = true;
        private float _restFov;

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

            // 状态提议 = 基准（状态机下一帧读回，永不包含意图/震屏叠加）
            CleanLookRotation = CurrentCameraState.lookRotation;

            // 角度仲裁：意图接管朝向（接管/归还按 BlendTime 平滑；无意图时直贴基准）
            UpdateAngleArbitration(GameTimeManager.CameraDelta);

            Quaternion finalRotation = Quaternion.Euler(_actualPitch, _actualYaw, 0f);
            Vector3 finalPosition = focusPoint - finalRotation * Vector3.forward * Distance;

            //按照近平面的位置进行物理碰撞检测
            Vector3 lookDirection = finalRotation * Vector3.forward;
            Vector3 rectOffset = lookDirection * regularCamera.nearClipPlane;
            Vector3 rectPosition = finalPosition + rectOffset;
            Vector3 castFrom = focus.position;
            Vector3 castLine = rectPosition - castFrom;
            float castDistance = castLine.magnitude;
            Vector3 castDirection = castLine / castDistance;

            //物理碰撞检测
            if (IsOpenPhysic == true && Physics.BoxCast(
                castFrom, CameraHalfExtends, castDirection, out RaycastHit hit,
                finalRotation, castDistance, obstructionMask
            ))
            {
                rectPosition = castFrom + castDirection * hit.distance;
                finalPosition = rectPosition - rectOffset;
            }

            // 震屏合成：偏移只在最终输出上叠加，不写回状态机基准（CleanLookRotation 已被记录）
            if (CameraShakeController.Instance != null)
            {
                CameraShakeController.Instance.GetOffset(out Vector3 shakePos, out Vector3 shakeRot, out float fovOffset, out Vector3 kickWorld);

                // 视距联动：镜头越远震幅按比例放大；锁定镜头减旋幅防晕（鸣潮/ZZZ 锁定下只减旋转不减位移）
                float distScale = Mathf.Clamp(Distance / 5f, 0.5f, 2f);
                bool locked = LockSystem.Instance != null && LockSystem.Instance.IsLocked;
                Vector3 rotOffset = shakeRot * (locked ? 0.6f : 1f);

                Quaternion localShake = Quaternion.Euler(rotOffset);
                finalRotation = finalRotation * localShake;
                finalPosition += finalRotation * (shakePos * distScale) + kickWorld;
                regularCamera.fieldOfView = _restFov + fovOffset;
            }

            MainCamera.SetPositionAndRotation(finalPosition, finalRotation);
        }

#if UNITY_EDITOR
        /// <summary>调试：技能镜头测试（由 SkillEditorScene 的 F7 调用）。看向锁定目标或前方，拉近 3.2m，1.5s 后自动归还。</summary>
        public void DebugPlaySkillCamIntent()
        {
            Vector3 lookPoint;
            if (LockSystem.Instance != null && LockSystem.Instance.LockedTarget != null)
                lookPoint = LockSystem.Instance.LockedTarget.GetCameraTargetPos();
            else if (focus != null)
                lookPoint = focus.position + CleanLookRotation * Vector3.forward * 8f;
            else
                lookPoint = MainCamera.position + MainCamera.forward * 8f;

            SubmitIntent(new CameraIntent(CameraIntentSource.Debug, lookPoint, 3.2f,
                priority: 30, blendTime: 0.25f, duration: 1.5f));
        }
#endif

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
