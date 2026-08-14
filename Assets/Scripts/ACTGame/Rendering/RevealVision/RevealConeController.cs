using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ACTGameEditor
{
    /// <summary>
    /// 手电筒式圆锥显现：锥体内物体由 ACT/RevealMasked Shader 显示，锥外不可见。
    /// 默认以角色为锥顶、沿角色朝向照射；运行时绘制锥边缘线框便于辨认范围。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RevealConeController : MonoBehaviour
    {
        const int WireSegments = 32;

        [Header("Input")]
        [SerializeField]
        KeyCode toggleKey = KeyCode.Alpha7;

        [Tooltip("按住开启；关闭则按键切换")]
        [SerializeField]
        bool holdToReveal;

        [Header("Cone")]
        [Tooltip("锥顶；为空则用本角色 Transform")]
        [SerializeField]
        Transform coneOrigin;

        [SerializeField]
        float range = 14f;

        [SerializeField]
        [Range(5f, 120f)]
        float angleDegrees = 40f;

        [SerializeField]
        [Range(0f, 1f)]
        float softEdge = 0.2f;

        [SerializeField]
        RevealChannel channelMask = RevealChannel.All;

        [Tooltip("锥顶相对 coneOrigin 的高度偏移（脚底→胸口）")]
        [SerializeField]
        float originHeightOffset = 1.0f;

        [SerializeField]
        bool respectGraphicsFxGate = true;

        [Header("边缘显示")]
        [SerializeField]
        bool showConeGuide = true;

        [Tooltip("在距锥顶该距离处画近环（角色作锥顶时用于辨认朝向）")]
        [SerializeField]
        float edgeRingDistance = 2.5f;

        [SerializeField]
        Color edgeColor = new Color(1f, 0.92f, 0.35f, 0.95f);

        [SerializeField]
        bool edgeAlwaysOnTop = true;

        [Tooltip("屏幕圆环：仅当锥顶贴近主相机时才有意义，角色原点时建议关闭")]
        [SerializeField]
        bool showScreenEdgeRing = false;

        bool _active;
        Mesh _wireMesh;
        GameObject _wireGo;
        MeshFilter _wireFilter;
        MeshRenderer _wireRenderer;
        Material _edgeMat;

        Vector3[] _unitRim;
        Vector3[] _wireVerts;
        Color[] _wireColors;
        int[] _wireIndices;
        float _cachedHalfRad = -1f;
        float _cachedRingDist = -1f;
        float _cachedRange = -1f;

        readonly HashSet<RevealVisionSubject> _revealed = new();
        readonly List<RevealVisionSubject> _revealedList = new(32);
        readonly List<RevealVisionSubject> _queryBuffer = new(32);

        /// <summary>圆锥显现是否开启。</summary>
        public bool IsActive => _active;

        void Awake()
        {
            ResolveOrigin();
            BuildUnitCache();
        }

        void OnDisable()
        {
            SetActive(false);
        }

        void OnDestroy()
        {
            if (_wireMesh != null)
                Destroy(_wireMesh);
            if (_edgeMat != null)
                Destroy(_edgeMat);
            if (_wireGo != null)
                Destroy(_wireGo);
        }

        void Update()
        {
            ResolveOrigin();

            if (holdToReveal)
            {
                bool want = Input.GetKey(toggleKey);
                if (want != _active)
                    SetActive(want);
            }
            else if (Input.GetKeyDown(toggleKey))
            {
                Toggle();
            }

            if (_active)
                PushMask();
        }

        void LateUpdate()
        {
            if (_active)
                PushMask();
        }

        /// <summary>切换圆锥显现。</summary>
        public void Toggle() => SetActive(!_active);

        /// <summary>开启 / 关闭。</summary>
        public void SetActive(bool active)
        {
            if (active && respectGraphicsFxGate && !GraphicsFxService.Query(GraphicsFxId.RevealVision))
                return;

            _active = active;
            if (_active)
            {
                EnsureWireGuide();
                PushMask();
                SetGuideVisible(showConeGuide);
            }
            else
            {
                RevealMaskState.SetCone(false, Vector3.zero, Vector3.forward, 0f, angleDegrees, softEdge);
                ClearRevealed();
                SetGuideVisible(false);
            }
        }

        void ResolveOrigin()
        {
            // 已指向相机的旧配置，迁移为角色原点
            if (coneOrigin != null && coneOrigin.GetComponent<Camera>() != null)
                coneOrigin = transform;

            if (coneOrigin == null)
                coneOrigin = transform;
        }

        Vector3 GetConeOriginPosition(Transform t)
        {
            return t.position + Vector3.up * originHeightOffset;
        }

        void PushMask()
        {
            ResolveOrigin();
            Transform t = coneOrigin != null ? coneOrigin : transform;
            Vector3 origin = GetConeOriginPosition(t);
            Vector3 dir = t.forward;
            RevealMaskState.SetCone(true, origin, dir, range, angleDegrees, softEdge);
            DetectInCone(origin, dir);
            UpdateWireGuide(t, origin);
        }

        void DetectInCone(Vector3 origin, Vector3 dir)
        {
            RevealVisionService.CollectInCone(origin, dir, range, angleDegrees, channelMask, _queryBuffer);

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

                if (IsInsideCone(s.WorldPosition, origin, dir))
                    continue;

                s.SetRevealed(false);
                _revealed.Remove(s);
                _revealedList.RemoveAt(i);
            }
        }

        bool IsInsideCone(Vector3 point, Vector3 origin, Vector3 dir)
        {
            Vector3 to = point - origin;
            float distSq = to.sqrMagnitude;
            if (distSq > range * range)
                return false;
            if (distSq <= 1e-8f)
                return true;

            float cosOuter = Mathf.Cos(Mathf.Max(0.1f, angleDegrees * 0.5f) * Mathf.Deg2Rad);
            return Vector3.Dot(to.normalized, dir.normalized) >= cosOuter;
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

        void BuildUnitCache()
        {
            _unitRim = new Vector3[WireSegments];
            for (int i = 0; i < WireSegments; i++)
            {
                float a = (i / (float)WireSegments) * Mathf.PI * 2f;
                _unitRim[i] = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 1f);
            }

            const int generators = 8;
            int vertCount = WireSegments * 2 + generators * 2 + 2;
            int indexCount = WireSegments * 4 + generators * 2 + 2;
            _wireVerts = new Vector3[vertCount];
            _wireColors = new Color[vertCount];
            _wireIndices = new int[indexCount];
        }

        void EnsureWireGuide()
        {
            if (!showConeGuide || _wireGo != null)
                return;

            if (_unitRim == null)
                BuildUnitCache();

            _wireMesh = new Mesh { name = "RevealConeEdgeWire", indexFormat = IndexFormat.UInt16 };
            _wireMesh.MarkDynamic();

            _wireGo = new GameObject("RevealConeEdgeGuide");
            _wireGo.transform.SetParent(null, false);
            _wireFilter = _wireGo.AddComponent<MeshFilter>();
            _wireFilter.sharedMesh = _wireMesh;
            _wireRenderer = _wireGo.AddComponent<MeshRenderer>();

            _edgeMat = CreateEdgeMaterial();
            _wireRenderer.sharedMaterial = _edgeMat;
            _wireRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _wireRenderer.receiveShadows = false;
            _wireRenderer.lightProbeUsage = LightProbeUsage.Off;
            _wireRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            _wireGo.SetActive(false);
        }

        Material CreateEdgeMaterial()
        {
            var shader = Shader.Find("Hidden/Internal-Colored");
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                shader = Shader.Find("Unlit/Color");

            var mat = new Material(shader) { name = "RevealConeEdgeMat", hideFlags = HideFlags.HideAndDontSave };
            mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_Cull", (int)CullMode.Off);
            mat.SetInt("_ZWrite", 0);
            mat.SetInt("_ZTest", edgeAlwaysOnTop ? (int)CompareFunction.Always : (int)CompareFunction.LessEqual);
            ApplyEdgeColor(mat);
            return mat;
        }

        void ApplyEdgeColor(Material mat)
        {
            if (mat == null)
                return;

            if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", edgeColor);
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", edgeColor);
        }

        void SetGuideVisible(bool visible)
        {
            if (_wireGo != null)
                _wireGo.SetActive(visible && showConeGuide);
        }

        void UpdateWireGuide(Transform facing, Vector3 worldOrigin)
        {
            if (_wireGo == null || !_wireGo.activeSelf || _wireMesh == null || _wireVerts == null)
                return;

            float halfRad = angleDegrees * 0.5f * Mathf.Deg2Rad;
            float ringDist = Mathf.Clamp(edgeRingDistance, 0.3f, Mathf.Max(0.5f, range));
            float farDist = Mathf.Max(ringDist + 0.1f, range);

            bool topologyDirty =
                !Mathf.Approximately(halfRad, _cachedHalfRad) ||
                !Mathf.Approximately(ringDist, _cachedRingDist) ||
                !Mathf.Approximately(farDist, _cachedRange);

            if (topologyDirty)
            {
                RebuildWireMesh(halfRad, ringDist, farDist);
                _cachedHalfRad = halfRad;
                _cachedRingDist = ringDist;
                _cachedRange = farDist;
            }

            _wireGo.transform.SetPositionAndRotation(worldOrigin, Quaternion.LookRotation(facing.forward, facing.up));
            _wireGo.transform.localScale = Vector3.one;

            if (_edgeMat != null)
            {
                _edgeMat.SetInt("_ZTest", edgeAlwaysOnTop ? (int)CompareFunction.Always : (int)CompareFunction.LessEqual);
                ApplyEdgeColor(_edgeMat);
            }
        }

        void RebuildWireMesh(float halfRad, float ringDist, float farDist)
        {
            float tanHalf = Mathf.Tan(halfRad);
            float nearR = tanHalf * ringDist;
            float farR = tanHalf * farDist;

            Color cNear = edgeColor;
            Color cFar = new Color(edgeColor.r, edgeColor.g, edgeColor.b, edgeColor.a * 0.55f);
            Color cGen = new Color(edgeColor.r, edgeColor.g, edgeColor.b, edgeColor.a * 0.75f);

            const int generators = 8;
            int vi = 0;

            int nearStart = vi;
            for (int i = 0; i < WireSegments; i++)
            {
                Vector3 u = _unitRim[i];
                _wireVerts[vi] = new Vector3(u.x * nearR, u.y * nearR, ringDist);
                _wireColors[vi] = cNear;
                vi++;
            }

            int farStart = vi;
            for (int i = 0; i < WireSegments; i++)
            {
                Vector3 u = _unitRim[i];
                _wireVerts[vi] = new Vector3(u.x * farR, u.y * farR, farDist);
                _wireColors[vi] = cFar;
                vi++;
            }

            int genStart = vi;
            for (int i = 0; i < generators; i++)
            {
                int rim = (i * WireSegments) / generators;
                Vector3 u = _unitRim[rim];
                _wireVerts[vi] = Vector3.zero;
                _wireColors[vi] = cGen;
                vi++;
                _wireVerts[vi] = new Vector3(u.x * farR, u.y * farR, farDist);
                _wireColors[vi] = cFar;
                vi++;
            }

            int axisStart = vi;
            _wireVerts[vi] = Vector3.zero;
            _wireColors[vi] = cGen;
            vi++;
            _wireVerts[vi] = new Vector3(0f, 0f, farDist);
            _wireColors[vi] = cFar;

            int ii = 0;
            for (int i = 0; i < WireSegments; i++)
            {
                _wireIndices[ii++] = nearStart + i;
                _wireIndices[ii++] = nearStart + ((i + 1) % WireSegments);
            }

            for (int i = 0; i < WireSegments; i++)
            {
                _wireIndices[ii++] = farStart + i;
                _wireIndices[ii++] = farStart + ((i + 1) % WireSegments);
            }

            for (int i = 0; i < generators; i++)
            {
                _wireIndices[ii++] = genStart + i * 2;
                _wireIndices[ii++] = genStart + i * 2 + 1;
            }

            _wireIndices[ii++] = axisStart;
            _wireIndices[ii++] = axisStart + 1;

            _wireMesh.Clear();
            _wireMesh.vertices = _wireVerts;
            _wireMesh.colors = _wireColors;
            _wireMesh.SetIndices(_wireIndices, MeshTopology.Lines, 0, false);
            _wireMesh.RecalculateBounds();
        }

        void OnGUI()
        {
            if (!_active || !showScreenEdgeRing || !showConeGuide)
                return;

            Camera cam = Camera.main;
            if (cam == null)
                return;

            // 仅当锥顶大致就是主相机时画屏幕环（否则用世界线框即可）
            Transform origin = coneOrigin != null ? coneOrigin : transform;
            if ((origin.position - cam.transform.position).sqrMagnitude > 0.25f)
                return;

            float halfCone = angleDegrees * 0.5f * Mathf.Deg2Rad;
            float halfFovV = cam.fieldOfView * 0.5f * Mathf.Deg2Rad;
            float aspect = cam.aspect;
            float halfFovH = Mathf.Atan(Mathf.Tan(halfFovV) * aspect);

            // 视口半径（相对半屏）：tan(cone)/tan(fov)
            float ry = Mathf.Tan(halfCone) / Mathf.Max(0.001f, Mathf.Tan(halfFovV));
            float rx = Mathf.Tan(halfCone) / Mathf.Max(0.001f, Mathf.Tan(halfFovH));
            ry = Mathf.Clamp(ry, 0.02f, 1.2f);
            rx = Mathf.Clamp(rx, 0.02f, 1.2f);

            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.5f;
            float px = rx * Screen.width * 0.5f;
            float py = ry * Screen.height * 0.5f;

            Color prev = GUI.color;
            GUI.color = edgeColor;
            const int segs = 48;
            const float thickness = 2f;
            Vector2 prevPt = new Vector2(cx + px, cy);
            for (int i = 1; i <= segs; i++)
            {
                float a = (i / (float)segs) * Mathf.PI * 2f;
                var pt = new Vector2(cx + Mathf.Cos(a) * px, cy + Mathf.Sin(a) * py);
                DrawScreenLine(prevPt, pt, thickness);
                prevPt = pt;
            }

            GUI.color = prev;
        }

        static void DrawScreenLine(Vector2 a, Vector2 b, float thickness)
        {
            Vector2 d = b - a;
            float len = d.magnitude;
            if (len < 0.01f)
                return;

            float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            Matrix4x4 matrixBackup = GUI.matrix;
            GUIUtility.RotateAroundPivot(angle, a);
            GUI.DrawTexture(new Rect(a.x, a.y - thickness * 0.5f, len, thickness), Texture2D.whiteTexture);
            GUI.matrix = matrixBackup;
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Transform t = coneOrigin != null ? coneOrigin : transform;
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.85f);
            float halfRad = angleDegrees * 0.5f * Mathf.Deg2Rad;
            float r = Mathf.Tan(halfRad) * range;
            Vector3 tip = t.position;
            Vector3 end = tip + t.forward * range;
            Vector3 right = t.right * r;
            Vector3 up = t.up * r;
            Gizmos.DrawLine(tip, end + right);
            Gizmos.DrawLine(tip, end - right);
            Gizmos.DrawLine(tip, end + up);
            Gizmos.DrawLine(tip, end - up);
            Gizmos.DrawWireSphere(end, r * 0.15f);

            float ring = Mathf.Clamp(edgeRingDistance, 0.3f, range);
            float rr = Mathf.Tan(halfRad) * ring;
            Vector3 c = tip + t.forward * ring;
            const int segs = 24;
            Vector3 prev = c + t.right * rr;
            for (int i = 1; i <= segs; i++)
            {
                float a = (i / (float)segs) * Mathf.PI * 2f;
                Vector3 p = c + (t.right * Mathf.Cos(a) + t.up * Mathf.Sin(a)) * rr;
                Gizmos.DrawLine(prev, p);
                prev = p;
            }
        }
#endif
    }
}
