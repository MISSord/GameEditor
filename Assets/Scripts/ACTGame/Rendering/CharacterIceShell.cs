using UnityEngine;

namespace ACTGameEditor
{
    /// <summary>
    /// 把 <c>ACT/Ice</c> 叠到角色 Renderer 上（二游冰壳）。由 <see cref="CharacterRenderFX.SetFreeze"/> 开关。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CharacterIceShell : MonoBehaviour
    {
        [SerializeField]
        Transform modelRoot;

        [SerializeField]
        Renderer[] renderers;

        [SerializeField]
        Material iceMaterial;

        Material[][] _baseMaterials;
        Material[][] _frozenMaterials;
        bool _prepared;
        bool _visible;
        bool _ownsMaterial;

        /// <summary>当前是否叠了冰壳材质。</summary>
        public bool IsVisible => _visible;

        void Awake()
        {
            EnsureMaterial();
            EnsureRenderers();
        }

        void OnDestroy()
        {
            if (_visible)
                SetVisible(false);

            if (_ownsMaterial && iceMaterial != null)
            {
                if (Application.isPlaying)
                    Destroy(iceMaterial);
                else
                    DestroyImmediate(iceMaterial);
                iceMaterial = null;
            }
        }

        /// <summary>绑定模型根并丢掉材质缓存。</summary>
        public void BindModel(Transform root)
        {
            if (_visible)
                SetVisible(false);

            modelRoot = root;
            renderers = null;
            _prepared = false;
            EnsureRenderers();
        }

        /// <summary>叠上 / 撤掉冰壳材质。amount 由 MPB _FreezeAmount 控制外观。</summary>
        public void SetVisible(bool visible)
        {
            EnsureMaterial();
            EnsureRenderers();
            if (iceMaterial == null || renderers == null || renderers.Length == 0)
                return;

            PrepareMaterials();
            _visible = visible;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null)
                    continue;
                r.sharedMaterials = visible ? _frozenMaterials[i] : _baseMaterials[i];
            }
        }

        void EnsureMaterial()
        {
            if (iceMaterial != null)
                return;

            Shader shader = Shader.Find("ACT/Ice");
            if (shader == null)
            {
                Debug.LogWarning($"[CharacterIceShell] {name} 找不到 Shader ACT/Ice。", this);
                return;
            }

            iceMaterial = new Material(shader)
            {
                name = "ACTIce_Runtime",
                hideFlags = HideFlags.HideAndDontSave,
            };
            _ownsMaterial = true;
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
            _frozenMaterials = new Material[renderers.Length][];

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null)
                {
                    _baseMaterials[i] = System.Array.Empty<Material>();
                    _frozenMaterials[i] = System.Array.Empty<Material>();
                    continue;
                }

                Material[] baseMats = r.sharedMaterials;
                _baseMaterials[i] = baseMats;

                bool hasIce = false;
                for (int m = 0; m < baseMats.Length; m++)
                {
                    if (baseMats[m] == iceMaterial)
                    {
                        hasIce = true;
                        break;
                    }
                }

                if (hasIce)
                {
                    _frozenMaterials[i] = baseMats;
                    continue;
                }

                var combined = new Material[baseMats.Length + 1];
                for (int m = 0; m < baseMats.Length; m++)
                    combined[m] = baseMats[m];
                combined[baseMats.Length] = iceMaterial;
                _frozenMaterials[i] = combined;
            }

            _prepared = true;
        }
    }
}
