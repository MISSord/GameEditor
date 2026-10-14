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

        /// <summary>技能轴构图默认优先级。低于谐度破坏 28，高于锁定 1。</summary>
        public const int SkillShotPriority = 22;

        const float ShotProbeDistance = 8f;

        [Tooltip("镜头距离平滑时间（秒）；0 = 滚轮即时生效。")]
        public float DistanceSmoothTime = 0.1f;

        [Tooltip("镜头距离上限（滚轮 / 意图请求统一 clamp）。")]
        public float MaxDistance = 12f;

        // ── 相机意图层：FreeLook 永驻基准；LockOn / AutoFraming / Skill 走权重混合；距离仍最高优先独占 ──
        [Tooltip("意图接管/归还的默认平滑时间（秒）。")]
        public float IntentBlendTime = 0.25f;

        [Tooltip("自由视角在角度混合中的优先级。LockOn 高 1 档时约 67% 锁 / 33% 自由。")]
        public int FreeLookPriority = 0;

        [Tooltip("锁定源优先级。比 FreeLook 高 1 档即可留下鼠标残差。")]
        public int LockOnPriority = 1;

        [Tooltip("锁定权重 0↔1 包络时间（秒）。")]
        public float LockWeightBlendTime = 0.15f;

        [Tooltip("锁定朝向跟踪目标的平滑时间（秒）；Q/E 切目标也走这条。")]
        public float LockLookSmoothTime = 0.1f;

        [Tooltip("关闭则构图源权重恒为 0（跑图调试用）。")]
        public bool AutoFramingEnabled = true;

        [Tooltip("构图源优先级。与 FreeLook 同档时，只有目标靠近画幅边缘才会拉 yaw。")]
        public int AutoFramingPriority = 0;

        [Tooltip("构图权重包络时间（秒）。")]
        public float AutoFramingBlendTime = 0.2f;

        [Tooltip("水平偏心距低于此值（0=中心，1=画幅边缘）不拉镜头。")]
        [Range(0f, 1f)]
        public float AutoFramingDeadZone = 0.32f;

        [Tooltip("水平偏心距达到此值时构图权重=1。")]
        [Range(0.1f, 1.5f)]
        public float AutoFramingFullZone = 0.78f;

        [Tooltip("无锁时只收相机前方这个锥角内的敌人（度）。身后不入选。")]
        public float AutoFramingMaxAngle = 95f;

        float _distanceTarget;
        float _distanceVelocity;

        // 仲裁后的实际朝向（技能意图接管时 SmoothDamp；无叠层时每帧直贴混合结果，正常手感零延迟）
        float _actualPitch, _actualYaw;
        float _pitchVelocity, _yawVelocity;
        float _releaseRemain;
        float _releaseBlendTime;

        float _lockWeight;
        float _lockWeightVelocity;
        float _lockLookPitch, _lockLookYaw;
        float _lockPitchVelocity, _lockYawVelocity;

        float _frameWeight;
        float _frameWeightVelocity;
        float _frameYaw;
        float _frameYawVelocity;
        ICameraTarget _framingTarget;

        const int MaxAngleSources = 8;
        const float WeightEpsilon = 0.001f;
        const float Ln2 = 0.6931472f;

        struct AngleBlendSource
        {
            public float Pitch;
            public float Yaw;
            public float Weight;
            public int Priority;
            public float BlendTime;
        }

        readonly AngleBlendSource[] _angleSources = new AngleBlendSource[MaxAngleSources];
        int _angleSourceCount;

        readonly List<CameraIntentEntry> _intents = new List<CameraIntentEntry>(4);

        FreeLookState _freeLookState;
        LockOnState _lockOnState;

        /// <summary>当前跟随的是否为该角色根节点（构图只打主控镜头）。</summary>
        public bool IsFollowing(Transform playerRoot)
        {
            if (playerRoot == null || CurrentTarget == null)
                return false;
            return CurrentTarget.GetPlayerTransform() == playerRoot;
        }

        /// <summary>锁定源当前包络权重（0..1）。混合层只读，不写回基准。</summary>
        public float LockWeight => _lockWeight;

        /// <summary>AutoFraming 当前包络权重（0..1）。只偏 yaw。</summary>
        public float AutoFramingWeight => _frameWeight;

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

            _freeLookState = new FreeLookState(this);
            _lockOnState = new LockOnState(this);
            _cameraStatesDic.Add(CameraEnumState.FreeLook, _freeLookState);
            _cameraStatesDic.Add(CameraEnumState.LockLook, _lockOnState);
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
            if (ls == null)
                return;

            if (ls.IsLocked)
            {
                // 从当前干净输出起算，包络才是转向目标的唯一运动
                _lockLookPitch = _actualPitch;
                _lockLookYaw = _actualYaw;
                _lockPitchVelocity = 0f;
                _lockYawVelocity = 0f;
                return;
            }

            // 解锁：把混合结果写回 FreeLook 基准，随后锁源贴着基准淡出，镜头不会甩回锁定前的旧角度
            if (_freeLookState != null)
            {
                _freeLookState.RebaseOrbit(Quaternion.Euler(_actualPitch, _actualYaw, 0f));
                CleanLookRotation = _freeLookState.lookRotation;
            }
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
        /// 角度按 Weight × 优先级加权混合，距离仍覆盖滚轮目标，技能接管/归还按 BlendTime 平滑。
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
                    entry.Weight = intent.Weight;
                    entry.HasRecipe = intent.HasRecipe;
                    entry.Recipe = intent.Recipe;
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
                Weight = intent.Weight,
                HasRecipe = intent.HasRecipe,
                Recipe = intent.Recipe,
            });
            return true;
        }

        /// <summary>
        /// 提交构图配方。Duration&lt;=0 时直到 CancelSkillShot / 片段结束才还。
        /// </summary>
        public bool SubmitShot(in CameraShotRecipe recipe,
            CameraIntentSource source = CameraIntentSource.SkillCamera,
            int priority = SkillShotPriority,
            float blendTime = -1f,
            float duration = 0f)
        {
            if (!recipe.IsValid)
                return false;
            return SubmitIntent(new CameraIntent(source, in recipe, priority, blendTime, duration));
        }

        /// <summary>取消技能构图槽（默认 priority 22，不影响谐度破坏 28 或距离请求 1）。</summary>
        public void CancelSkillShot(int priority = SkillShotPriority)
        {
            CancelIntent(CameraIntentSource.SkillCamera, priority);
        }

        /// <summary>
        /// 片段结束时取消：仅当当前槽仍是同一预设才拆，避免后一段构图被前一段 OnFinish 误关。
        /// 名称空或无法解析则按 22 整槽取消。
        /// </summary>
        public void CancelSkillShotIfPreset(string presetName)
        {
            if (!CameraShotRecipe.TryFromName(presetName, out CameraShotRecipe recipe))
            {
                CancelSkillShot();
                return;
            }

            for (int i = _intents.Count - 1; i >= 0; i--)
            {
                CameraIntentEntry entry = _intents[i];
                if (entry.Source != CameraIntentSource.SkillCamera || entry.Priority != SkillShotPriority)
                    continue;
                if (!entry.HasRecipe || entry.Recipe.Preset != recipe.Preset)
                    continue;
                _intents.RemoveAt(i);
            }
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
        /// 角度仲裁：FreeLook / LockOn / AutoFraming / 意图按 Weight × 2^(priority-max) 归一化球面混合。
        /// 技能意图仍 SmoothDamp 接管与归还；无技能意图时直贴混合结果（含 AutoFraming），鼠标零延迟。
        /// 位置锚点仍是 focusPoint；混合结果不写回 CleanLookRotation。
        /// </summary>
        void UpdateAngleArbitration(float dt)
        {
            EulerToPitchYaw(CurrentCameraState.lookRotation, out float basePitch, out float baseYaw);

            _angleSourceCount = 0;
            AddAngleSource(basePitch, baseYaw, 1f, FreeLookPriority, 0f);

            UpdateLockWeight(dt);
            UpdateLockLook(dt, basePitch, baseYaw);
            if (_lockWeight > WeightEpsilon)
                AddAngleSource(_lockLookPitch, _lockLookYaw, _lockWeight, LockOnPriority, LockWeightBlendTime);

            UpdateAutoFraming(dt);
            if (_frameWeight > WeightEpsilon)
                AddAngleSource(basePitch, _frameYaw, _frameWeight, AutoFramingPriority, AutoFramingBlendTime);

            bool hasLookIntent = CollectLookIntents();

            if (!TryBlendAngles(out float mixPitch, out float mixYaw, out float mixBlend))
            {
                mixPitch = basePitch;
                mixYaw = baseYaw;
                mixBlend = IntentBlendTime;
            }

            if (hasLookIntent)
            {
                _releaseRemain = mixBlend;
                _releaseBlendTime = mixBlend;
                SmoothAngleTo(mixPitch, mixYaw, mixBlend, dt);
                return;
            }

            if (_releaseRemain > 0f)
            {
                _releaseRemain -= dt;
                SmoothAngleTo(mixPitch, mixYaw, _releaseBlendTime, dt);
                return;
            }

            _actualPitch = mixPitch;
            _actualYaw = mixYaw;
            _pitchVelocity = 0f;
            _yawVelocity = 0f;
        }

        void UpdateLockWeight(float dt)
        {
            float target = LockSystem.Instance != null && LockSystem.Instance.IsLocked ? 1f : 0f;
            float smooth = Mathf.Max(0.001f, LockWeightBlendTime);
            _lockWeight = Mathf.Clamp01(Mathf.SmoothDamp(_lockWeight, target, ref _lockWeightVelocity, smooth, float.MaxValue, dt));
            if (_lockWeight < WeightEpsilon)
            {
                _lockWeight = 0f;
                _lockWeightVelocity = 0f;
            }
            else if (target >= 1f && _lockWeight > 1f - WeightEpsilon)
            {
                _lockWeight = 1f;
                _lockWeightVelocity = 0f;
            }
        }

        void UpdateLockLook(float dt, float freePitch, float freeYaw)
        {
            if (_lockOnState != null && _lockOnState.TryProposeLook(out float pitch, out float yaw))
            {
                float smooth = Mathf.Max(0.001f, LockLookSmoothTime);
                _lockLookPitch = Mathf.SmoothDampAngle(_lockLookPitch, pitch, ref _lockPitchVelocity, smooth, float.MaxValue, dt);
                _lockLookYaw = Mathf.SmoothDampAngle(_lockLookYaw, yaw, ref _lockYawVelocity, smooth, float.MaxValue, dt);
                return;
            }

            // 解锁包络期：锁源贴着 FreeLook，淡出不跟鼠标抢、也不甩向旧锁定点
            _lockLookPitch = freePitch;
            _lockLookYaw = freeYaw;
            _lockPitchVelocity = 0f;
            _lockYawVelocity = 0f;
        }

        void UpdateAutoFraming(float dt)
        {
            float desiredWeight = 0f;
            float desiredYaw = _actualYaw;
            bool haveYaw = false;

            if (AutoFramingEnabled)
            {
                ICameraTarget target = ResolveFramingTarget();
                if (target != null && TryGetPlanarYaw(target, out desiredYaw))
                {
                    haveYaw = true;
                    desiredWeight = ComputeFramingWeight(target);
                }
            }

            float prevWeight = _frameWeight;
            float smooth = Mathf.Max(0.001f, AutoFramingBlendTime);
            _frameWeight = Mathf.Clamp01(Mathf.SmoothDamp(
                _frameWeight, desiredWeight, ref _frameWeightVelocity, smooth, float.MaxValue, dt));
            if (_frameWeight < WeightEpsilon)
            {
                _frameWeight = 0f;
                _frameWeightVelocity = 0f;
                _frameYawVelocity = 0f;
                return;
            }

            if (!haveYaw)
                return;

            if (prevWeight <= WeightEpsilon)
            {
                _frameYaw = _actualYaw;
                _frameYawVelocity = 0f;
            }

            _frameYaw = Mathf.SmoothDampAngle(_frameYaw, desiredYaw, ref _frameYawVelocity, smooth, float.MaxValue, dt);
        }

        ICameraTarget ResolveFramingTarget()
        {
            var ls = LockSystem.Instance;
            if (ls == null)
            {
                _framingTarget = null;
                return null;
            }

            ICameraTarget next = ls.FindFramingTarget(AutoFramingMaxAngle);
            if (_framingTarget != null && ls.IsEligibleFramingTarget(_framingTarget, AutoFramingMaxAngle))
            {
                if (next != null && next != _framingTarget && CurrentTarget != null)
                {
                    float oldSq = (_framingTarget.GetPlayerPos() - CurrentTarget.GetPlayerPos()).sqrMagnitude;
                    float newSq = (next.GetPlayerPos() - CurrentTarget.GetPlayerPos()).sqrMagnitude;
                    // 显著更近才换，避免两名敌人之间抖动
                    if (newSq < oldSq * 0.49f)
                        _framingTarget = next;
                }
            }
            else
            {
                _framingTarget = next;
            }

            return _framingTarget;
        }

        bool TryGetPlanarYaw(ICameraTarget target, out float yaw)
        {
            yaw = 0f;
            Vector3 dir = target.GetCameraTargetPos() - focusPoint;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f)
                return false;
            yaw = Quaternion.LookRotation(dir, Vector3.up).eulerAngles.y;
            return true;
        }

        float ComputeFramingWeight(ICameraTarget target)
        {
            if (regularCamera == null)
                return 0f;

            Vector3 viewport = regularCamera.WorldToViewportPoint(target.GetCameraTargetPos());
            bool locked = LockSystem.Instance != null && LockSystem.Instance.IsLocked
                && target == LockSystem.Instance.LockedTarget;
            if (viewport.z <= 0f)
                return locked ? 1f : 0f;

            float eccentricity = Mathf.Abs(viewport.x - 0.5f) * 2f;
            float dead = Mathf.Min(AutoFramingDeadZone, AutoFramingFullZone);
            float full = Mathf.Max(AutoFramingFullZone, dead + 0.01f);
            return Mathf.InverseLerp(dead, full, eccentricity);
        }

        bool TryResolveRecipeLook(in CameraShotRecipe recipe, out float pitch, out float yaw)
        {
            pitch = 0f;
            yaw = 0f;
            if (focusPoint == default && focus == null)
                return false;

            float spaceYaw = ResolveShotSpaceYaw(recipe.Space);
            Quaternion rot = Quaternion.Euler(recipe.Pitch, spaceYaw + recipe.Azimuth, 0f);
            Vector3 lookPoint = focusPoint + Vector3.up * recipe.LookHeight + rot * Vector3.forward * ShotProbeDistance;
            Vector3 dir = lookPoint - focusPoint;
            if (dir.sqrMagnitude <= 0.0001f)
                return false;

            Vector3 euler = Quaternion.LookRotation(dir.normalized, Vector3.up).eulerAngles;
            pitch = euler.x > 180f ? euler.x - 360f : euler.x;
            yaw = euler.y;
            return true;
        }

        float ResolveShotSpaceYaw(CameraShotSpace space)
        {
            float casterYaw = 0f;
            bool haveCaster = false;
            if (CurrentTarget != null)
            {
                Transform player = CurrentTarget.GetPlayerTransform();
                if (player != null)
                {
                    casterYaw = player.eulerAngles.y;
                    haveCaster = true;
                }
            }

            if (space != CameraShotSpace.LockLine)
                return haveCaster ? casterYaw : _actualYaw;

            Vector3 from = CurrentTarget != null ? CurrentTarget.GetPlayerPos() : focusPoint;
            Vector3 to = default;
            bool haveTo = false;
            var ls = LockSystem.Instance;
            if (ls != null && ls.IsLocked && ls.LockedTarget != null)
            {
                to = ls.LockedTarget.GetCameraTargetPos();
                haveTo = true;
            }
            else if (_framingTarget != null)
            {
                to = _framingTarget.GetCameraTargetPos();
                haveTo = true;
            }

            if (!haveTo)
                return haveCaster ? casterYaw : _actualYaw;

            Vector3 dir = to - from;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f)
                return haveCaster ? casterYaw : _actualYaw;
            return Quaternion.LookRotation(dir, Vector3.up).eulerAngles.y;
        }

        bool CollectLookIntents()
        {
            bool hasLookIntent = false;
            for (int i = 0; i < _intents.Count; i++)
            {
                CameraIntentEntry entry = _intents[i];
                if (entry.Weight <= WeightEpsilon)
                    continue;

                float blend = entry.BlendTime > 0f ? entry.BlendTime : IntentBlendTime;
                if (entry.HasRecipe)
                {
                    if (!TryResolveRecipeLook(in entry.Recipe, out float pitch, out float yaw))
                        continue;
                    AddAngleSource(pitch, yaw, entry.Weight, entry.Priority, blend);
                    hasLookIntent = true;
                    continue;
                }

                if (entry.LookPoint == null)
                    continue;

                Vector3 dir = entry.LookPoint.Value - focusPoint;
                if (dir.sqrMagnitude <= 0.0001f)
                    continue;

                Vector3 euler = Quaternion.LookRotation(dir.normalized, Vector3.up).eulerAngles;
                AddAngleSource(
                    euler.x > 180f ? euler.x - 360f : euler.x,
                    euler.y,
                    entry.Weight,
                    entry.Priority,
                    blend);
                hasLookIntent = true;
            }

            return hasLookIntent;
        }

        void AddAngleSource(float pitch, float yaw, float weight, int priority, float blendTime)
        {
            if (weight <= WeightEpsilon || _angleSourceCount >= MaxAngleSources)
                return;

            _angleSources[_angleSourceCount++] = new AngleBlendSource
            {
                Pitch = pitch,
                Yaw = yaw,
                Weight = weight,
                Priority = priority,
                BlendTime = blendTime,
            };
        }

        bool TryBlendAngles(out float pitch, out float yaw, out float blendTime)
        {
            pitch = 0f;
            yaw = 0f;
            blendTime = IntentBlendTime;
            int count = _angleSourceCount;
            if (count <= 0)
                return false;

            if (count == 1)
            {
                ref AngleBlendSource only = ref _angleSources[0];
                pitch = only.Pitch;
                yaw = only.Yaw;
                if (only.BlendTime > 0f)
                    blendTime = only.BlendTime;
                return true;
            }

            int maxPriority = _angleSources[0].Priority;
            for (int i = 1; i < count; i++)
            {
                int p = _angleSources[i].Priority;
                if (p > maxPriority)
                    maxPriority = p;
            }

            float sinPitch = 0f;
            float cosPitch = 0f;
            float sinYaw = 0f;
            float cosYaw = 0f;
            float weightSum = 0f;
            float bestWeight = -1f;
            for (int i = 0; i < count; i++)
            {
                ref AngleBlendSource source = ref _angleSources[i];
                float w = source.Weight * Mathf.Exp((source.Priority - maxPriority) * Ln2);
                if (w <= WeightEpsilon)
                    continue;

                float pitchRad = source.Pitch * Mathf.Deg2Rad;
                float yawRad = source.Yaw * Mathf.Deg2Rad;
                sinPitch += Mathf.Sin(pitchRad) * w;
                cosPitch += Mathf.Cos(pitchRad) * w;
                sinYaw += Mathf.Sin(yawRad) * w;
                cosYaw += Mathf.Cos(yawRad) * w;
                weightSum += w;
                if (w > bestWeight)
                {
                    bestWeight = w;
                    if (source.BlendTime > 0f)
                        blendTime = source.BlendTime;
                }
            }

            if (weightSum <= WeightEpsilon)
                return false;

            pitch = Mathf.Atan2(sinPitch, cosPitch) * Mathf.Rad2Deg;
            yaw = Mathf.Atan2(sinYaw, cosYaw) * Mathf.Rad2Deg;
            return true;
        }

        void SmoothAngleTo(float pitchTarget, float yawTarget, float blendTime, float dt)
        {
            float smooth = Mathf.Max(0.001f, blendTime);
            _actualPitch = Mathf.SmoothDampAngle(_actualPitch, pitchTarget, ref _pitchVelocity, smooth, float.MaxValue, dt);
            _actualYaw = Mathf.SmoothDampAngle(_actualYaw, yawTarget, ref _yawVelocity, smooth, float.MaxValue, dt);
        }

        static void EulerToPitchYaw(Quaternion rotation, out float pitch, out float yaw)
        {
            Vector3 euler = rotation.eulerAngles;
            pitch = euler.x > 180f ? euler.x - 360f : euler.x;
            yaw = euler.y;
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
            public float Weight = 1f;
            public bool HasRecipe;
            public CameraShotRecipe Recipe;

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
            // Q/E 与目标校验挂在 LockOn 提议源上，不再依赖状态机切到 LockLook
            _lockOnState.OnUpdate();
            
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

            // 状态提议 = 基准（始终是 FreeLook 轨道；混合/构图/震屏都在这之后的输出层）
            CleanLookRotation = CurrentCameraState.lookRotation;

            // 角度仲裁：权重混合 → 技能 SmoothDamp；结果不写回基准
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
        int _debugShotIndex;

        /// <summary>调试：F7 循环三套构图预设，1.5s 后自动归还。</summary>
        public void DebugPlaySkillCamIntent()
        {
            CameraShotPreset preset = (CameraShotPreset)(_debugShotIndex % 3 + 1);
            _debugShotIndex++;
            CameraShotRecipe recipe = CameraShotRecipe.FromPreset(preset);
            SubmitShot(recipe, CameraIntentSource.Debug, priority: 30, blendTime: 0.25f, duration: 1.5f);
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

        // 切换状态的核心方法。Stage A 起 CurrentCameraState 应保持 FreeLook（LockOn 只作混合源）。
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
