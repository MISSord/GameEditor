using UnityEngine;
using UnityEngine.Rendering;

namespace ACTGameEditor
{
    /// <summary>
    /// 玩家立方体迷雾：雾盒在开启时按玩家坐标原地生成；可视清晰半径跟随玩家。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerFogController : MonoBehaviour
    {
        [Header("输入")]
        [SerializeField]
        KeyCode toggleKey = KeyCode.Alpha8;

        [Header("锚点")]
        [Tooltip("迷雾中心；为空则用本对象（角色）")]
        [SerializeField]
        Transform fogOrigin;

        [Tooltip("关闭=开启时按玩家坐标原地生成并固定；开启=每帧跟随玩家")]
        [SerializeField]
        bool followOrigin = false;

        [Header("雾盒尺寸（长宽高一起改，米）")]
        [Tooltip("立方体边长；长宽高同步")]
        [SerializeField]
        float boxSize = 24f;

        [Tooltip("相对脚底的中心高度偏移")]
        [SerializeField]
        float centerHeightOffset = 1.0f;

        [Header("可视与雾色（在这里调）")]
        [Tooltip("此半径内清晰可见")]
        [SerializeField]
        float clearRadius = 6f;

        [Tooltip("清晰区外到全雾的过渡宽度")]
        [SerializeField]
        float fogFade = 4f;

        [Tooltip("雾颜色（天空也会被罩成此色）")]
        [SerializeField]
        Color fogColor = new Color(0.92f, 0.94f, 0.97f, 1f);

        [SerializeField]
        [Range(0f, 1f)]
        float intensity = 0.95f;

        [Tooltip("天空盒是否罩雾（建议开启）")]
        [SerializeField]
        bool fogSky = true;

        [Tooltip("0=按水平距离(XZ)，1=按三维距离")]
        [SerializeField]
        [Range(0f, 1f)]
        float heightFalloff = 0f;

        [SerializeField]
        bool respectGraphicsFxGate = true;

        [Header("线框辅助")]
        [SerializeField]
        bool showBoxGuide = true;

        [SerializeField]
        Color guideColor = new Color(0.6f, 0.75f, 0.95f, 0.85f);

        bool _active;
        Vector3 _lockedCenter;
        Mesh _guideMesh;
        GameObject _guideGo;
        MeshFilter _guideFilter;
        MeshRenderer _guideRenderer;
        Material _guideMat;

        /// <summary>迷雾是否开启。</summary>
        public bool IsActive => _active;

        /// <summary>雾盒边长（长宽高相同）。</summary>
        public float BoxSize
        {
            get => boxSize;
            set => boxSize = Mathf.Max(0.01f, value);
        }

        /// <summary>雾盒尺寸向量（长宽高相同，供内部/兼容使用）。</summary>
        public Vector3 BoxSize3 => Vector3.one * Mathf.Max(0.01f, boxSize);

        /// <summary>清晰半径。</summary>
        public float ClearRadius
        {
            get => clearRadius;
            set => clearRadius = Mathf.Max(0.01f, value);
        }

        /// <summary>雾过渡宽度。</summary>
        public float FogFade
        {
            get => fogFade;
            set => fogFade = Mathf.Max(0.01f, value);
        }

        /// <summary>雾颜色。</summary>
        public Color FogColor
        {
            get => fogColor;
            set => fogColor = value;
        }

        /// <summary>强度。</summary>
        public float Intensity
        {
            get => intensity;
            set => intensity = Mathf.Clamp01(value);
        }

        /// <summary>天空是否罩雾。</summary>
        public bool FogSky
        {
            get => fogSky;
            set => fogSky = value;
        }

        void Awake()
        {
            if (fogOrigin == null)
                fogOrigin = transform;

            // 迷雾按开启瞬间的玩家坐标原地生成，不跟随
            followOrigin = false;
        }

        void OnDisable()
        {
            SetActive(false);
        }

        void OnDestroy()
        {
            if (_guideMesh != null)
                Destroy(_guideMesh);
            if (_guideMat != null)
                Destroy(_guideMat);
            if (_guideGo != null)
                Destroy(_guideGo);
        }

        void Update()
        {
            if (Input.GetKeyDown(toggleKey))
                Toggle();

            if (_active)
                PushState();
        }

        /// <summary>切换迷雾。</summary>
        public void Toggle() => SetActive(!_active);

        /// <summary>开启 / 关闭。开启时雾盒钉在玩家当前坐标；可视半径仍跟随玩家。</summary>
        public void SetActive(bool active)
        {
            if (active && respectGraphicsFxGate && !GraphicsFxService.Query(GraphicsFxId.PlayerFog))
                return;

            _active = active;
            if (_active)
            {
                _lockedCenter = GetOriginCenter();
                EnsureGuide();
                SetGuideVisible(showBoxGuide);
                PushState();
            }
            else
            {
                PlayerFogState.Clear();
                SetGuideVisible(false);
            }
        }

        Vector3 GetOriginCenter()
        {
            Transform t = fogOrigin != null ? fogOrigin : transform;
            return t.position + Vector3.up * centerHeightOffset;
        }

        void PushState()
        {
            // 雾盒：原地固定；清晰区：始终跟随玩家
            Vector3 boxCenter = followOrigin ? GetOriginCenter() : _lockedCenter;
            if (followOrigin)
                _lockedCenter = boxCenter;

            Vector3 clearCenter = GetOriginCenter();

            PlayerFogState.Set(
                true,
                boxCenter,
                clearCenter,
                BoxSize3,
                clearRadius,
                fogFade,
                fogColor,
                intensity,
                heightFalloff,
                fogSky);

            UpdateGuide(boxCenter);
        }

        void EnsureGuide()
        {
            if (!showBoxGuide || _guideGo != null)
                return;

            _guideMesh = BuildWireCube();
            _guideGo = new GameObject("PlayerFogBoxGuide");
            _guideFilter = _guideGo.AddComponent<MeshFilter>();
            _guideFilter.sharedMesh = _guideMesh;
            _guideRenderer = _guideGo.AddComponent<MeshRenderer>();

            var shader = Shader.Find("Hidden/Internal-Colored");
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            _guideMat = new Material(shader != null ? shader : Shader.Find("Unlit/Color"))
            {
                name = "PlayerFogGuideMat",
                hideFlags = HideFlags.HideAndDontSave
            };
            _guideMat.SetInt("_ZWrite", 0);
            _guideMat.SetInt("_ZTest", (int)CompareFunction.Always);
            _guideMat.SetInt("_Cull", (int)CullMode.Off);
            if (_guideMat.HasProperty("_Color"))
                _guideMat.SetColor("_Color", guideColor);

            _guideRenderer.sharedMaterial = _guideMat;
            _guideRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _guideRenderer.receiveShadows = false;
            _guideGo.SetActive(false);
        }

        void SetGuideVisible(bool visible)
        {
            if (_guideGo != null)
                _guideGo.SetActive(visible && showBoxGuide);
        }

        void UpdateGuide(Vector3 center)
        {
            if (_guideGo == null || !_guideGo.activeSelf)
                return;

            _guideGo.transform.SetPositionAndRotation(center, Quaternion.identity);
            float s = Mathf.Max(0.01f, boxSize);
            _guideGo.transform.localScale = new Vector3(s, s, s);

            if (_guideMat != null && _guideMat.HasProperty("_Color"))
                _guideMat.SetColor("_Color", guideColor);
        }

        static Mesh BuildWireCube()
        {
            // 单位立方体线框，中心原点
            var mesh = new Mesh { name = "PlayerFogWireCube" };
            var v = new[]
            {
                new Vector3(-0.5f, -0.5f, -0.5f),
                new Vector3(0.5f, -0.5f, -0.5f),
                new Vector3(0.5f, -0.5f, 0.5f),
                new Vector3(-0.5f, -0.5f, 0.5f),
                new Vector3(-0.5f, 0.5f, -0.5f),
                new Vector3(0.5f, 0.5f, -0.5f),
                new Vector3(0.5f, 0.5f, 0.5f),
                new Vector3(-0.5f, 0.5f, 0.5f),
            };

            var indices = new[]
            {
                0, 1, 1, 2, 2, 3, 3, 0,
                4, 5, 5, 6, 6, 7, 7, 4,
                0, 4, 1, 5, 2, 6, 3, 7,
            };

            var colors = new Color[v.Length];
            for (int i = 0; i < colors.Length; i++)
                colors[i] = Color.white;

            mesh.vertices = v;
            mesh.colors = colors;
            mesh.SetIndices(indices, MeshTopology.Lines, 0, true);
            mesh.RecalculateBounds();
            return mesh;
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Vector3 boxC = Application.isPlaying && _active
                ? _lockedCenter
                : GetOriginCenter();
            Gizmos.color = new Color(0.5f, 0.7f, 1f, 0.35f);
            Gizmos.DrawWireCube(boxC, BoxSize3);

            // 清晰半径始终画在玩家当前位置
            Gizmos.color = new Color(0.3f, 0.9f, 1f, 0.5f);
            Gizmos.DrawWireSphere(GetOriginCenter(), clearRadius);
        }
#endif
    }
}
