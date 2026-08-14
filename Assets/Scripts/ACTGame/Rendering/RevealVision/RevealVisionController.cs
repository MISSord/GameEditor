using System.Collections.Generic;
using UnityEngine;

namespace ACTGameEditor
{
    /// <summary>
    /// 球形显现：自角色外扩球体，范围内的 <see cref="RevealVisionSubject"/> 才显示（交互类似扫描）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RevealVisionController : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField]
        KeyCode triggerKey = KeyCode.Alpha5;

        [Header("Source")]
        [SerializeField]
        Transform revealOrigin;

        [Tooltip("用于“是否在球内”判定的玩家坐标；为空则用 revealOrigin")]
        [SerializeField]
        Transform playerForTintCheck;

        [Header("Pulse")]
        [SerializeField]
        float maxRadius = 8f;

        [SerializeField]
        float expandDuration = 0.7f;

        [SerializeField]
        float holdDuration = 1.5f;

        [SerializeField]
        RevealChannel channelMask = RevealChannel.All;

        [Header("Visual")]
        [Tooltip("建议复用 ScanSphere 材质")]
        [SerializeField]
        Material revealSphereMaterial;

        [SerializeField]
        bool createSphereIfMissing = true;

        [Header("Screen Tint (player inside sphere)")]
        [SerializeField]
        bool enablePlayerInsideTint = true;

        [SerializeField]
        Color insideTintColor = new Color(0.55f, 0.78f, 1f, 1f);

        [SerializeField]
        [Range(0f, 1f)]
        float insideTintIntensity = 0.28f;

        [SerializeField]
        bool respectGraphicsFxGate = true;

        readonly HashSet<RevealVisionSubject> _revealed = new();
        readonly List<RevealVisionSubject> _revealedList = new(32);
        readonly List<RevealVisionSubject> _queryBuffer = new(32);

        Transform _sphere;
        Renderer _sphereRenderer;
        MaterialPropertyBlock _sphereMpb;
        bool _pulsing;
        float _elapsed;
        float _currentRadius;

        /// <summary>是否正在球形显现。</summary>
        public bool IsActive => _pulsing;

        /// <summary>绑定球体材质（编辑器 / 初始化用）。</summary>
        public void SetSphereMaterial(Material mat) => revealSphereMaterial = mat;

        void Awake()
        {
            if (revealOrigin == null)
                revealOrigin = transform;
            _sphereMpb = new MaterialPropertyBlock();
        }

        void Update()
        {
            if (Input.GetKeyDown(triggerKey))
                TriggerReveal();

            if (!_pulsing)
                return;

            _elapsed += Time.deltaTime;
            float total = expandDuration + holdDuration;

            if (_elapsed <= expandDuration)
            {
                float t = expandDuration > 0f ? _elapsed / expandDuration : 1f;
                _currentRadius = Mathf.Lerp(0f, maxRadius, t);
                UpdateSphereScale(_currentRadius);
                PushSphereMask(_currentRadius);
                DetectInRadius(_currentRadius);
                UpdatePlayerInsideTint(_currentRadius, 1f);
            }
            else
            {
                _currentRadius = maxRadius;
                UpdateSphereScale(_currentRadius);
                PushSphereMask(_currentRadius);
                DetectInRadius(_currentRadius);

                float holdT = holdDuration > 0f ? (_elapsed - expandDuration) / holdDuration : 1f;
                float alphaMul = Mathf.Lerp(1f, 0f, holdT);
                SetSphereAlpha(alphaMul);
                UpdatePlayerInsideTint(_currentRadius, alphaMul);
            }

            if (_elapsed >= total)
                CancelReveal();
        }

        /// <summary>触发一次球形显现。</summary>
        public void TriggerReveal()
        {
            if (_pulsing)
                return;

            if (respectGraphicsFxGate && !GraphicsFxService.Query(GraphicsFxId.RevealVision))
                return;

            RevealVisionService.Instance.SetChannels(channelMask);
            EnsureSphere();
            ClearRevealed();
            _pulsing = true;
            _elapsed = 0f;
            _currentRadius = 0f;
            SetSphereVisible(true);
            SetSphereAlpha(1f);
            UpdateSphereScale(0f);
            PushSphereMask(0f);
            UpdatePlayerInsideTint(0f, 1f);
        }

        /// <summary>兼容旧 API：true=触发，false=取消。</summary>
        public void SetActive(bool active)
        {
            if (active)
                TriggerReveal();
            else
                CancelReveal();
        }

        /// <summary>切换：进行中则取消，否则触发。</summary>
        public void Toggle()
        {
            if (_pulsing)
                CancelReveal();
            else
                TriggerReveal();
        }

        /// <summary>立即停止并隐藏已显现对象。</summary>
        public void CancelReveal()
        {
            _pulsing = false;
            ClearRevealed();
            SetSphereVisible(false);
            SetSphereAlpha(1f);
            RevealMaskState.SetSphere(false, Vector3.zero, 0f);
            ScreenTintState.Clear();
        }

        void PushSphereMask(float radius)
        {
            Vector3 center = revealOrigin != null ? revealOrigin.position : transform.position;
            RevealMaskState.SetSphere(true, center, radius);
        }

        void UpdatePlayerInsideTint(float radius, float strengthMul)
        {
            if (!enablePlayerInsideTint)
            {
                ScreenTintState.Clear();
                return;
            }

            Vector3 center = revealOrigin != null ? revealOrigin.position : transform.position;
            Transform player = playerForTintCheck != null ? playerForTintCheck : revealOrigin;
            if (player == null)
                player = transform;

            // 简单坐标判定：玩家是否在当前球半径内
            bool inside = (player.position - center).sqrMagnitude <= radius * radius;
            if (!inside)
            {
                ScreenTintState.Clear();
                return;
            }

            float intensity = insideTintIntensity * Mathf.Clamp01(strengthMul);
            ScreenTintState.Set(true, insideTintColor, intensity);
        }

        void DetectInRadius(float radius)
        {
            Vector3 center = revealOrigin != null ? revealOrigin.position : transform.position;
            RevealVisionService.CollectInRadius(center, radius, channelMask, _queryBuffer);

            for (int i = 0; i < _queryBuffer.Count; i++)
            {
                RevealVisionSubject s = _queryBuffer[i];
                if (s == null || !_revealed.Add(s))
                    continue;

                _revealedList.Add(s);
                s.SetRevealed(true);
            }

            for (int i = _revealedList.Count - 1; i >= 0; i--)
            {
                RevealVisionSubject s = _revealedList[i];
                if (s == null)
                {
                    _revealedList.RemoveAt(i);
                    continue;
                }

                if ((s.WorldPosition - center).sqrMagnitude <= radius * radius)
                    continue;

                s.SetRevealed(false);
                _revealed.Remove(s);
                _revealedList.RemoveAt(i);
            }
        }

        void ClearRevealed()
        {
            for (int i = 0; i < _revealedList.Count; i++)
            {
                RevealVisionSubject s = _revealedList[i];
                if (s != null)
                    s.SetRevealed(false);
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
            go.name = "RevealVisionSphere";
            go.transform.SetParent(revealOrigin != null ? revealOrigin : transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.zero;

            Object.Destroy(go.GetComponent<Collider>());
            _sphere = go.transform;
            _sphereRenderer = go.GetComponent<Renderer>();

            if (revealSphereMaterial != null)
                _sphereRenderer.sharedMaterial = revealSphereMaterial;

            SetSphereVisible(false);
        }

        void UpdateSphereScale(float radius)
        {
            if (_sphere == null)
                return;

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
            if (_sphereRenderer == null)
                return;

            Material mat = revealSphereMaterial != null ? revealSphereMaterial : _sphereRenderer.sharedMaterial;
            if (mat == null)
                return;

            if (_sphereMpb == null)
                _sphereMpb = new MaterialPropertyBlock();

            Color c = mat.HasProperty("_Color") ? mat.GetColor("_Color") : new Color(0.6f, 0.3f, 1f, 0.35f);
            c.a *= Mathf.Clamp01(alphaMul);
            _sphereMpb.Clear();
            _sphereMpb.SetColor("_Color", c);
            _sphereRenderer.SetPropertyBlock(_sphereMpb);
        }

        void OnDisable()
        {
            CancelReveal();
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Transform origin = revealOrigin != null ? revealOrigin : transform;
            Gizmos.color = new Color(0.7f, 0.3f, 1f, 0.25f);
            Gizmos.DrawWireSphere(origin.position, maxRadius);
            if (_pulsing)
            {
                Gizmos.color = new Color(0.7f, 0.3f, 1f, 0.6f);
                Gizmos.DrawWireSphere(origin.position, _currentRadius);
            }
        }
#endif
    }
}
