using System;
using UnityEngine;

namespace ACTGameEditor
{
    /// <summary>
    /// 角色渲染表现驱动：受击闪白 / 噪声溶解 / 强制外轮廓 / 遮挡轮廓宽度门闩。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CharacterRenderFX : MonoBehaviour
    {
        static readonly int HitFlashId = Shader.PropertyToID("_HitFlash");
        static readonly int DissolveId = Shader.PropertyToID("_Dissolve");
        static readonly int ForceOutlineId = Shader.PropertyToID("_ForceOutline");
        static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");
        static readonly int IncludeInDepthVisionId = Shader.PropertyToID("_IncludeInDepthVision");
        static readonly int RevealId = Shader.PropertyToID("_Reveal");
        static readonly int ProximityDitherId = Shader.PropertyToID("_ProximityDither");

        [SerializeField]
        Transform modelRoot;

        [SerializeField]
        Renderer[] renderers;

        [SerializeField]
        float defaultOutlineWidth = 0.025f;

        MaterialPropertyBlock _mpb;
        ObjectFxController _objectFx;
        DepthVisionParticipant _depthParticipant;
        ScanRevealVisual _scanReveal;
        AfterimageController _afterimage;
        float _hitFlash;
        float _dissolve;
        float _forceOutline;
        float _outlineWidth;
        float _proximityDither;
        float _flashTimer;
        float _flashDuration;
        float _dissolveTimer;
        float _dissolveDuration;
        bool _dissolving;
        bool _flashing;
        Action _onDissolveComplete;

        /// <summary>当前闪白强度（0~1）。</summary>
        public float HitFlash => _hitFlash;

        /// <summary>当前溶解阈值（0~1）。</summary>
        public float Dissolve => _dissolve;

        /// <summary>强制外轮廓（扫描揭示旧路径）。</summary>
        public bool ForceOutlineActive => _forceOutline > 0.5f;

        /// <summary>近距镂空强度 0~1。</summary>
        public float ProximityDither => _proximityDither;

        void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            _objectFx = GetComponent<ObjectFxController>();
            _depthParticipant = GetComponent<DepthVisionParticipant>();
            _scanReveal = GetComponent<ScanRevealVisual>();
            _afterimage = GetComponent<AfterimageController>();
            _outlineWidth = defaultOutlineWidth;
            EnsureRenderers();
            RefreshCapabilityGates();
        }

        void OnEnable()
        {
            // 无 ObjectFxController 时也需响应全局开关（有 ObjectFx 时由其转发，此处双订无害）
            var svc = FindObjectOfType<GraphicsFxService>();
            if (svc != null)
                svc.OnAnyChanged += RefreshCapabilityGates;
        }

        void OnDisable()
        {
            var svc = FindObjectOfType<GraphicsFxService>();
            if (svc != null)
                svc.OnAnyChanged -= RefreshCapabilityGates;
        }

        /// <summary>
        /// 绑定模型根节点并刷新 Renderer 缓存。
        /// </summary>
        public void BindModel(Transform root)
        {
            modelRoot = root;
            renderers = null;
            _afterimage ??= GetComponent<AfterimageController>();
            _afterimage?.RefreshSources(root);
            EnsureRenderers();
            RefreshCapabilityGates();
        }

        /// <summary>
        /// 播放闪避 / 冲刺残影（3 快照 PerRenderer BakeMesh）。
        /// </summary>
        public void PlayAfterimage(AfterimageProfile profile = null)
        {
            if (!IsAllowed(ObjectFxFlags.Afterimage))
                return;

            _afterimage ??= GetComponent<AfterimageController>();
            if (_afterimage == null)
                return;

            if (profile != null)
                _afterimage.PlayAfterimage(profile);
            else
                _afterimage.PlayAfterimage();
        }

        /// <summary>
        /// 停止残影。
        /// </summary>
        public void StopAfterimage()
        {
            _afterimage ??= GetComponent<AfterimageController>();
            _afterimage?.StopAfterimage();
        }

        /// <summary>
        /// 根据全局/对象开关刷新 MPB 门闩状态。
        /// </summary>
        public void RefreshCapabilityGates()
        {
            if (!IsAllowed(ObjectFxFlags.HitFlash) && _hitFlash > 0f)
            {
                _flashing = false;
                _hitFlash = 0f;
            }

            if (!IsAllowed(ObjectFxFlags.Dissolve) && _dissolve > 0f)
            {
                _dissolving = false;
                _dissolve = 0f;
                _onDissolveComplete = null;
            }

            if (!IsAllowed(ObjectFxFlags.ForceOutline))
                _forceOutline = 0f;

            if (!IsAllowed(ObjectFxFlags.ProximityDither))
                _proximityDither = 0f;

            _outlineWidth = IsAllowed(ObjectFxFlags.OcclusionOutline) ? defaultOutlineWidth : 0f;

            // 同步 Keyword / Pass（与 ObjectFx 一致；无 ObjectFx 时按全局）
            EnsureRenderers();
            ObjectFxFlags effective = MaterialFxSync.ResolveEffectiveFlags(_objectFx);
            MaterialFxSync.ApplyToRenderers(renderers, effective);

            ApplyBlock();
        }

        /// <summary>
        /// 直接设置闪白强度。
        /// </summary>
        public void SetHitFlash(float value)
        {
            if (!IsAllowed(ObjectFxFlags.HitFlash))
                value = 0f;

            _hitFlash = Mathf.Clamp01(value);
            _flashing = false;
            ApplyBlock();
        }

        /// <summary>
        /// 直接设置溶解阈值。
        /// </summary>
        public void SetDissolve(float value)
        {
            if (!IsAllowed(ObjectFxFlags.Dissolve))
                value = 0f;

            _dissolve = Mathf.Clamp01(value);
            _dissolving = false;
            ApplyBlock();
        }

        /// <summary>
        /// 设置强制外轮廓。
        /// </summary>
        public void SetForceOutline(bool enabled)
        {
            if (!IsAllowed(ObjectFxFlags.ForceOutline))
                enabled = false;

            _forceOutline = enabled ? 1f : 0f;
            ApplyBlock();
        }

        /// <summary>
        /// 设置近距镂空强度（0 可见，1 全镂空）。由 <see cref="ProximityDitherFade"/> 驱动。
        /// </summary>
        public void SetProximityDither(float value)
        {
            if (!IsAllowed(ObjectFxFlags.ProximityDither))
                value = 0f;

            _proximityDither = Mathf.Clamp01(value);
            ApplyBlock();
        }

        /// <summary>
        /// 播放受击闪白。
        /// </summary>
        public void Flash(float duration = 0.12f)
        {
            if (!IsAllowed(ObjectFxFlags.HitFlash))
                return;

            _flashDuration = Mathf.Max(0.01f, duration);
            _flashTimer = _flashDuration;
            _hitFlash = 1f;
            _flashing = true;
            ApplyBlock();
        }

        /// <summary>
        /// 播放溶解（0→1）。
        /// </summary>
        public void PlayDissolve(float duration = 1.2f, Action onComplete = null)
        {
            if (!IsAllowed(ObjectFxFlags.Dissolve))
            {
                onComplete?.Invoke();
                return;
            }

            _dissolveDuration = Mathf.Max(0.01f, duration);
            _dissolveTimer = 0f;
            _dissolve = 0f;
            _dissolving = true;
            _onDissolveComplete = onComplete;
            ApplyBlock();
        }

        /// <summary>
        /// 重置闪白、溶解与强制外轮廓。
        /// </summary>
        public void ResetFX()
        {
            _flashing = false;
            _dissolving = false;
            _hitFlash = 0f;
            _dissolve = 0f;
            _forceOutline = 0f;
            _onDissolveComplete = null;
            RefreshCapabilityGates();
        }

        bool IsAllowed(ObjectFxFlags flag)
        {
            if (_objectFx != null)
                return _objectFx.IsAllowed(flag);

            if (!GraphicsFxMapping.TryToFxId(flag, out GraphicsFxId id))
                return true;

            return GraphicsFxService.Query(id);
        }

        void Update()
        {
            bool dirty = false;

            if (_flashing)
            {
                _flashTimer -= Time.deltaTime;
                _hitFlash = Mathf.Clamp01(_flashTimer / _flashDuration);
                dirty = true;
                if (_flashTimer <= 0f)
                {
                    _flashing = false;
                    _hitFlash = 0f;
                }
            }

            if (_dissolving)
            {
                _dissolveTimer += Time.deltaTime;
                _dissolve = Mathf.Clamp01(_dissolveTimer / _dissolveDuration);
                dirty = true;
                if (_dissolve >= 1f)
                {
                    _dissolving = false;
                    var cb = _onDissolveComplete;
                    _onDissolveComplete = null;
                    cb?.Invoke();
                }
            }

            if (dirty)
                ApplyPropertyBlock();
        }

        /// <summary>
        /// 将当前 FX / 深度参与门闩写入 MaterialPropertyBlock。
        /// </summary>
        public void ApplyPropertyBlock()
        {
            EnsureRenderers();
            if (renderers == null)
                return;

            if (_mpb == null)
                _mpb = new MaterialPropertyBlock();

            if (_depthParticipant == null)
                _depthParticipant = GetComponent<DepthVisionParticipant>();

            if (_scanReveal == null)
                _scanReveal = GetComponent<ScanRevealVisual>();

            _mpb.Clear();
            _mpb.SetFloat(HitFlashId, _hitFlash);
            _mpb.SetFloat(DissolveId, _dissolve);
            _mpb.SetFloat(ForceOutlineId, _forceOutline);
            _mpb.SetFloat(OutlineWidthId, _outlineWidth);
            _mpb.SetFloat(IncludeInDepthVisionId,
                _depthParticipant != null ? _depthParticipant.GetIncludeValue() : 1f);
            // 与 ScanRevealVisual 共用 MPB，避免扫描揭示 Clear 后丢失深度门闩
            _mpb.SetFloat(RevealId, _scanReveal != null ? _scanReveal.RevealValue : 0f);
            _mpb.SetFloat(ProximityDitherId, _proximityDither);

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r != null)
                    r.SetPropertyBlock(_mpb);
            }
        }

        void EnsureRenderers()
        {
            if (renderers != null && renderers.Length > 0)
                return;

            Transform root = modelRoot != null ? modelRoot : transform;
            renderers = root.GetComponentsInChildren<Renderer>(true);
        }

        void ApplyBlock() => ApplyPropertyBlock();
    }
}
