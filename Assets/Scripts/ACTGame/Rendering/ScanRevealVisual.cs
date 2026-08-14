using UnityEngine;

namespace ACTGameEditor
{
    /// <summary>
    /// 扫描揭示视觉：在 Renderer 上叠加边缘高亮材质（ACT/ScanEdgeHighlight）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ScanRevealVisual : MonoBehaviour
    {
        static readonly int RevealId = Shader.PropertyToID("_Reveal");
        static readonly int IncludeInDepthVisionId = Shader.PropertyToID("_IncludeInDepthVision");

        [SerializeField]
        Transform modelRoot;

        [SerializeField]
        Renderer[] renderers;

        [SerializeField]
        Material edgeHighlightMaterial;

        MaterialPropertyBlock _mpb;
        Material[][] _baseMaterials;
        Material[][] _revealedMaterials;
        bool _prepared;
        bool _revealed;
        DepthVisionParticipant _depthParticipant;
        CharacterRenderFX _renderFx;

        /// <summary>当前是否揭示中。</summary>
        public bool IsRevealed => _revealed;

        /// <summary>供 CharacterRenderFX 合并写入 MPB。</summary>
        public float RevealValue => _revealed ? 1f : 0f;

        void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            _depthParticipant = GetComponent<DepthVisionParticipant>();
            _renderFx = GetComponent<CharacterRenderFX>();
            EnsureRenderers();
        }

        /// <summary>
        /// 绑定模型根；可选指定高亮材质。
        /// </summary>
        public void Bind(Transform root, Material highlightMat = null)
        {
            modelRoot = root;
            if (highlightMat != null)
                edgeHighlightMaterial = highlightMat;
            renderers = null;
            _prepared = false;
            EnsureRenderers();
        }

        /// <summary>
        /// 仅设置高亮材质（不重置 Renderer 缓存）。
        /// </summary>
        public void EnsureMaterial(Material highlightMat)
        {
            if (highlightMat == null || edgeHighlightMaterial == highlightMat)
                return;

            edgeHighlightMaterial = highlightMat;
            _prepared = false;
        }

        /// <summary>
        /// 开启 / 关闭边缘高亮揭示。
        /// </summary>
        public void SetRevealed(bool revealed)
        {
            EnsureRenderers();
            if (renderers == null || renderers.Length == 0)
                return;

            if (edgeHighlightMaterial == null)
            {
                Debug.LogWarning($"[ScanRevealVisual] {name} 未指定 ScanEdgeHighlight 材质。", this);
                return;
            }

            PrepareMaterials();
            _revealed = revealed;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null)
                    continue;

                r.sharedMaterials = revealed ? _revealedMaterials[i] : _baseMaterials[i];
            }

            // 统一走 CharacterRenderFX，避免 Clear MPB 冲掉 _IncludeInDepthVision
            if (_renderFx == null)
                _renderFx = GetComponent<CharacterRenderFX>();

            if (_renderFx != null)
            {
                _renderFx.ApplyPropertyBlock();
                return;
            }

            ApplyRevealPropertyBlock();
        }

        /// <summary>
        /// 在无 CharacterRenderFX 时，合并写入 _Reveal 与深度视界门闩（禁止 Clear 整块 MPB）。
        /// </summary>
        public void ApplyRevealPropertyBlock()
        {
            EnsureRenderers();
            if (renderers == null)
                return;

            if (_mpb == null)
                _mpb = new MaterialPropertyBlock();

            if (_depthParticipant == null)
                _depthParticipant = GetComponent<DepthVisionParticipant>();

            float include = _depthParticipant != null ? _depthParticipant.GetIncludeValue() : 1f;
            float reveal = _revealed ? 1f : 0f;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null)
                    continue;

                r.GetPropertyBlock(_mpb);
                _mpb.SetFloat(RevealId, reveal);
                _mpb.SetFloat(IncludeInDepthVisionId, include);
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

        void PrepareMaterials()
        {
            if (_prepared || renderers == null)
                return;

            _baseMaterials = new Material[renderers.Length][];
            _revealedMaterials = new Material[renderers.Length][];

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null)
                {
                    _baseMaterials[i] = System.Array.Empty<Material>();
                    _revealedMaterials[i] = System.Array.Empty<Material>();
                    continue;
                }

                Material[] baseMats = r.sharedMaterials;
                _baseMaterials[i] = baseMats;

                bool hasHighlight = false;
                for (int m = 0; m < baseMats.Length; m++)
                {
                    if (baseMats[m] == edgeHighlightMaterial)
                    {
                        hasHighlight = true;
                        break;
                    }
                }

                if (hasHighlight)
                {
                    _revealedMaterials[i] = baseMats;
                }
                else
                {
                    var combined = new Material[baseMats.Length + 1];
                    for (int m = 0; m < baseMats.Length; m++)
                        combined[m] = baseMats[m];
                    combined[baseMats.Length] = edgeHighlightMaterial;
                    _revealedMaterials[i] = combined;
                }
            }

            _prepared = true;
        }

        void OnDisable()
        {
            if (_revealed)
                SetRevealed(false);
        }
    }
}
