using System.Collections.Generic;
using UnityEngine;

namespace ACTGameEditor
{
    /// <summary>
    /// 从角色中心球体外扩扫描：命中带 <see cref="ScanTarget"/> 的对象时强制外轮廓，结束后清除。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ScanPulseController : MonoBehaviour
    {
        const int OverlapCapacity = 64;

        [Header("Source")]
        [SerializeField]
        Transform scanOrigin;

        [Header("Pulse")]
        [SerializeField]
        float maxRadius = 8f;

        [SerializeField]
        float expandDuration = 0.6f;

        [SerializeField]
        float holdDuration = 1.2f;

        [SerializeField]
        LayerMask detectMask = ~0;

        [Header("Visual")]
        [SerializeField]
        Material scanSphereMaterial;

        [SerializeField]
        bool createSphereIfMissing = true;

        readonly Collider[] _overlapBuffer = new Collider[OverlapCapacity];
        readonly HashSet<ScanTarget> _revealed = new();
        readonly List<ScanTarget> _revealedList = new(32);

        Transform _sphere;
        Renderer _sphereRenderer;
        MaterialPropertyBlock _sphereMpb;
        bool _scanning;
        float _elapsed;
        float _currentRadius;

        /// <summary>是否正在扫描。</summary>
        public bool IsScanning => _scanning;

        void Awake()
        {
            if (scanOrigin == null)
                scanOrigin = transform;
            _sphereMpb = new MaterialPropertyBlock();
        }

        /// <summary>
        /// 触发一次扫描（进行中则忽略）。
        /// </summary>
        public void TriggerScan()
        {
            if (_scanning)
                return;

            if (!GraphicsFxService.Query(GraphicsFxId.ScanPulse))
                return;

            EnsureSphere();
            ClearRevealed();
            _scanning = true;
            _elapsed = 0f;
            _currentRadius = 0f;
            SetSphereVisible(true);
            UpdateSphereScale(0f);
        }

        /// <summary>
        /// 立即停止扫描并清除揭示效果。
        /// </summary>
        public void CancelScan()
        {
            if (!_scanning && _revealed.Count == 0)
            {
                SetSphereVisible(false);
                return;
            }

            _scanning = false;
            ClearRevealed();
            SetSphereVisible(false);
        }

        void Update()
        {
            if (!_scanning)
                return;

            _elapsed += Time.deltaTime;
            float total = expandDuration + holdDuration;

            if (_elapsed <= expandDuration)
            {
                float t = expandDuration > 0f ? _elapsed / expandDuration : 1f;
                _currentRadius = Mathf.Lerp(0f, maxRadius, t);
                UpdateSphereScale(_currentRadius);
                DetectInRadius(_currentRadius);
            }
            else
            {
                _currentRadius = maxRadius;
                UpdateSphereScale(_currentRadius);
                // hold 阶段保持揭示，球体可淡出
                float holdT = holdDuration > 0f ? (_elapsed - expandDuration) / holdDuration : 1f;
                SetSphereAlpha(Mathf.Lerp(1f, 0f, holdT));
            }

            if (_elapsed >= total)
            {
                _scanning = false;
                ClearRevealed();
                SetSphereVisible(false);
                SetSphereAlpha(1f);
            }
        }

        void DetectInRadius(float radius)
        {
            Vector3 center = scanOrigin != null ? scanOrigin.position : transform.position;
            int count = Physics.OverlapSphereNonAlloc(center, radius, _overlapBuffer, detectMask, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                Collider col = _overlapBuffer[i];
                if (col == null)
                    continue;

                ScanTarget target = col.GetComponentInParent<ScanTarget>();
                if (target == null || !_revealed.Add(target))
                    continue;

                _revealedList.Add(target);
                target.SetRevealed(true);
            }
        }

        void ClearRevealed()
        {
            for (int i = 0; i < _revealedList.Count; i++)
            {
                ScanTarget t = _revealedList[i];
                if (t != null)
                    t.SetRevealed(false);
            }

            _revealedList.Clear();
            _revealed.Clear();
        }

        void EnsureSphere()
        {
            if (_sphere != null)
                return;

            if (!createSphereIfMissing)
                return;

            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "ScanPulseSphere";
            go.transform.SetParent(scanOrigin != null ? scanOrigin : transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.zero;

            Object.Destroy(go.GetComponent<Collider>());
            _sphere = go.transform;
            _sphereRenderer = go.GetComponent<Renderer>();

            if (scanSphereMaterial != null)
                _sphereRenderer.sharedMaterial = scanSphereMaterial;

            SetSphereVisible(false);
        }

        void UpdateSphereScale(float radius)
        {
            if (_sphere == null)
                return;

            // Unity 默认 Sphere 直径为 1，scale = diameter = radius * 2
            float d = radius * 2f;
            _sphere.localScale = new Vector3(d, d, d);
        }

        void SetSphereVisible(bool visible)
        {
            if (_sphere != null)
                _sphere.gameObject.SetActive(visible);
        }

        void SetSphereAlpha(float alphaMul)
        {
            if (_sphereRenderer == null || scanSphereMaterial == null)
                return;

            if (_sphereMpb == null)
                _sphereMpb = new MaterialPropertyBlock();

            Color c = scanSphereMaterial.HasProperty("_Color")
                ? scanSphereMaterial.GetColor("_Color")
                : Color.cyan;
            c.a *= Mathf.Clamp01(alphaMul);
            _sphereMpb.Clear();
            _sphereMpb.SetColor("_Color", c);
            _sphereRenderer.SetPropertyBlock(_sphereMpb);
        }

        void OnDisable()
        {
            CancelScan();
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Transform origin = scanOrigin != null ? scanOrigin : transform;
            Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.25f);
            Gizmos.DrawWireSphere(origin.position, maxRadius);
            if (_scanning)
            {
                Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.6f);
                Gizmos.DrawWireSphere(origin.position, _currentRadius);
            }
        }
#endif
    }
}
