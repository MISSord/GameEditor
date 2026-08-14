using UnityEngine;

namespace ACTGameEditor
{
    /// <summary>
    /// 摄像机过近时，用屏幕抖动镂空（clip）逐渐“抠掉”物体，透出后方画面；不走透明混合。
    /// 需要物体使用 ACT/Character（或支持 _ProximityDither 的同类 Shader）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ProximityDitherFade : MonoBehaviour
    {
        [Header("距离（米）")]
        [Tooltip("大于此距离：完全可见")]
        [SerializeField]
        float fullyVisibleDistance = 2.5f;

        [Tooltip("小于此距离：完全镂空消失")]
        [SerializeField]
        float fullyHiddenDistance = 0.6f;

        [Tooltip("距离采样点；为空则用自身 Bounds 最近表面")]
        [SerializeField]
        Transform distanceProbe;

        [SerializeField]
        bool useRendererBounds = true;

        [Header("过渡")]
        [SerializeField]
        [Range(0.1f, 4f)]
        float fadePower = 1.25f;

        [SerializeField]
        bool respectGraphicsFxGate = true;

        [SerializeField]
        Renderer[] renderers;

        CharacterRenderFX _renderFx;
        ObjectFxController _objectFx;
        MaterialPropertyBlock _mpb;
        float _currentDither;
        float _lastApplied = -1f;
        static readonly int ProximityDitherId = Shader.PropertyToID("_ProximityDither");

        /// <summary>当前镂空强度 0~1。</summary>
        public float CurrentDither => _currentDither;

        void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            _renderFx = GetComponent<CharacterRenderFX>();
            _objectFx = GetComponent<ObjectFxController>();
            EnsureRenderers();
        }

        void OnEnable()
        {
            var svc = FindObjectOfType<GraphicsFxService>();
            if (svc != null)
                svc.OnAnyChanged += OnFxChanged;

            // 无 ObjectFx 时也刷一次 Keyword（例如纯 Prop + 本组件）
            EnsureRenderers();
            if (_objectFx != null)
                _objectFx.RefreshDependents();
            else
                MaterialFxSync.ApplyToRenderers(renderers, MaterialFxSync.ResolveEffectiveFlags(null));
        }

        void OnDisable()
        {
            var svc = FindObjectOfType<GraphicsFxService>();
            if (svc != null)
                svc.OnAnyChanged -= OnFxChanged;

            _currentDither = 0f;
            ApplyDither(0f, force: true);
        }

        void OnFxChanged()
        {
            if (!IsAllowed())
                ApplyDither(0f, force: true);
        }

        void LateUpdate()
        {
            if (!IsAllowed())
            {
                if (_currentDither > 0f)
                {
                    _currentDither = 0f;
                    ApplyDither(0f, force: true);
                }
                return;
            }

            Camera cam = ResolveCamera();
            if (cam == null)
                return;

            float dist = SampleDistance(cam.transform.position);
            float near = Mathf.Min(fullyHiddenDistance, fullyVisibleDistance);
            float far = Mathf.Max(fullyHiddenDistance, fullyVisibleDistance);
            float t = 1f - Mathf.InverseLerp(near, far, dist);
            t = Mathf.Pow(Mathf.Clamp01(t), fadePower);
            _currentDither = t;

            if (Mathf.Abs(_currentDither - _lastApplied) > 0.002f)
                ApplyDither(_currentDither, force: false);
        }

        static Camera ResolveCamera()
        {
            Camera cam = Camera.main;
            if (cam != null)
                return cam;

            cam = Camera.current;
            if (cam != null && cam.cameraType == CameraType.Game)
                return cam;

            var cams = Camera.allCameras;
            for (int i = 0; i < cams.Length; i++)
            {
                if (cams[i] != null && cams[i].enabled && cams[i].gameObject.activeInHierarchy)
                    return cams[i];
            }

            return null;
        }

        bool IsAllowed()
        {
            if (respectGraphicsFxGate && !GraphicsFxService.Query(GraphicsFxId.ProximityDither))
                return false;

            if (_objectFx != null && !_objectFx.IsAllowed(ObjectFxFlags.ProximityDither))
                return false;

            return true;
        }

        float SampleDistance(Vector3 cameraPos)
        {
            if (distanceProbe != null)
                return Vector3.Distance(cameraPos, distanceProbe.position);

            if (useRendererBounds)
            {
                EnsureRenderers();
                if (renderers != null && renderers.Length > 0)
                {
                    float best = float.MaxValue;
                    bool any = false;
                    for (int i = 0; i < renderers.Length; i++)
                    {
                        Renderer r = renderers[i];
                        if (r == null || !r.enabled)
                            continue;

                        // Bounds 最近点，比中心更符合“贴脸”
                        Vector3 closest = r.bounds.ClosestPoint(cameraPos);
                        float d = Vector3.Distance(cameraPos, closest);
                        if (d < best)
                            best = d;
                        any = true;
                    }

                    if (any)
                        return best;
                }
            }

            return Vector3.Distance(cameraPos, transform.position);
        }

        void ApplyDither(float value, bool force)
        {
            value = Mathf.Clamp01(value);
            if (!force && Mathf.Abs(value - _lastApplied) <= 0.002f)
                return;

            _lastApplied = value;

            if (_renderFx == null)
                _renderFx = GetComponent<CharacterRenderFX>();

            if (_renderFx != null)
            {
                _renderFx.SetProximityDither(value);
                return;
            }

            EnsureRenderers();
            if (renderers == null)
                return;

            if (_mpb == null)
                _mpb = new MaterialPropertyBlock();

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null)
                    continue;

                r.GetPropertyBlock(_mpb);
                _mpb.SetFloat(ProximityDitherId, value);
                r.SetPropertyBlock(_mpb);
            }
        }

        void EnsureRenderers()
        {
            if (renderers != null && renderers.Length > 0)
                return;
            renderers = GetComponentsInChildren<Renderer>(true);
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.5f, 0.2f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, fullyVisibleDistance);
            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, fullyHiddenDistance);
        }
#endif
    }
}
