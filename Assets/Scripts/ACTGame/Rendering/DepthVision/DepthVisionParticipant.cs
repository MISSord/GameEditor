using UnityEngine;

namespace ACTGameEditor
{
    /// <summary>
    /// 单对象是否写入深度视界：与全局 IncludeCharacterDepth 做 AND。
    /// 挂在使用 ACT/Character 的角色 / ScanTarget 上。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DepthVisionParticipant : MonoBehaviour
    {
        static readonly int IncludeInDepthVisionId = Shader.PropertyToID("_IncludeInDepthVision");

        [Tooltip("关闭后，即使全局允许，此对象也不会出现在深度视界中")]
        [SerializeField]
        bool includeInDepthVision = true;

        [SerializeField]
        Renderer[] renderers;

        MaterialPropertyBlock _mpb;
        CharacterRenderFX _renderFx;

        /// <summary>此对象是否参与深度视界。</summary>
        public bool IncludeInDepthVision
        {
            get => includeInDepthVision;
            set
            {
                if (includeInDepthVision == value)
                    return;
                includeInDepthVision = value;
                Apply();
            }
        }

        void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            _renderFx = GetComponent<CharacterRenderFX>();
            EnsureRenderers();
        }

        void OnEnable() => Apply();

#if UNITY_EDITOR
        void OnValidate()
        {
            if (!Application.isPlaying)
                return;
            Apply();
        }
#endif

        /// <summary>
        /// 写入 MPB；若存在 CharacterRenderFX 则走其统一 Apply，避免互相覆盖。
        /// </summary>
        public void Apply()
        {
            if (_renderFx == null)
                _renderFx = GetComponent<CharacterRenderFX>();

            if (_renderFx != null)
            {
                _renderFx.ApplyPropertyBlock();
                return;
            }

            if (_mpb == null)
                _mpb = new MaterialPropertyBlock();

            EnsureRenderers();
            if (renderers == null)
                return;

            float value = includeInDepthVision ? 1f : 0f;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null)
                    continue;

                r.GetPropertyBlock(_mpb);
                _mpb.SetFloat(IncludeInDepthVisionId, value);
                r.SetPropertyBlock(_mpb);
            }
        }

        /// <summary>供 CharacterRenderFX 读取当前门闩值。</summary>
        public float GetIncludeValue() => includeInDepthVision ? 1f : 0f;

        void EnsureRenderers()
        {
            if (renderers != null && renderers.Length > 0)
                return;

            renderers = GetComponentsInChildren<Renderer>(true);
        }
    }
}
