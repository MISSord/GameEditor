using UnityEngine;

namespace ACTGameEditor
{
    /// <summary>
    /// 单对象效果开关：与全局 <see cref="GraphicsFxService"/> 做 AND，并同步材质 Keyword/Pass。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ObjectFxController : MonoBehaviour
    {
        public enum FxPreset
        {
            /// <summary>角色：战斗表现 + 描边 + 近距镂空。</summary>
            Character = 0,
            /// <summary>场景物：仅近距镂空。</summary>
            PropDitherOnly = 1,
            /// <summary>场景物：仅扫描/遮挡高亮类。</summary>
            PropOutlineOnly = 2,
            /// <summary>显式全开（调试用）。</summary>
            All = 3,
            /// <summary>自定义（用下方 Flags）。</summary>
            Custom = 4,
        }

        [Tooltip("预设会覆盖 Flags；选 Custom 后可手改 Flags")]
        [SerializeField]
        FxPreset preset = FxPreset.Character;

        [Tooltip("本对象允许的效果（全局关闭时仍会被挡住）")]
        [SerializeField]
        ObjectFxFlags enabledFx = ObjectFxFlags.HitFlash | ObjectFxFlags.Dissolve
                                  | ObjectFxFlags.OcclusionOutline | ObjectFxFlags.ForceOutline
                                  | ObjectFxFlags.ScanEdgeHighlight
                                  | ObjectFxFlags.ProximityDither | ObjectFxFlags.Afterimage;

        [SerializeField]
        Renderer[] renderers;

        /// <summary>本对象允许的效果位。</summary>
        public ObjectFxFlags EnabledFx
        {
            get => enabledFx;
            set
            {
                if (enabledFx == value)
                    return;
                enabledFx = value;
                preset = FxPreset.Custom;
                RefreshDependents();
            }
        }

        /// <summary>当前预设。</summary>
        public FxPreset Preset => preset;

        /// <summary>
        /// 查询某对象效果是否真正可用（对象允许 && 全局开启）。
        /// </summary>
        public bool IsAllowed(ObjectFxFlags flag)
        {
            if ((enabledFx & flag) == 0)
                return false;

            if (!GraphicsFxMapping.TryToFxId(flag, out GraphicsFxId id))
                return true;

            return GraphicsFxService.Query(id);
        }

        /// <summary>
        /// 设置单个对象效果位。
        /// </summary>
        public void SetFlag(ObjectFxFlags flag, bool enabled)
        {
            EnabledFx = enabled ? (enabledFx | flag) : (enabledFx & ~flag);
        }

        /// <summary>应用预设并刷新。</summary>
        public void ApplyPreset(FxPreset newPreset)
        {
            preset = newPreset;
            if (preset != FxPreset.Custom)
                enabledFx = FlagsFromPreset(preset);
            RefreshDependents();
        }

        /// <summary>预设 → Flags。</summary>
        public static ObjectFxFlags FlagsFromPreset(FxPreset p) => p switch
        {
            FxPreset.Character => ObjectFxFlags.HitFlash | ObjectFxFlags.Dissolve
                                  | ObjectFxFlags.OcclusionOutline | ObjectFxFlags.ForceOutline
                                  | ObjectFxFlags.ScanEdgeHighlight
                                  | ObjectFxFlags.ProximityDither | ObjectFxFlags.Afterimage,
            FxPreset.PropDitherOnly => ObjectFxFlags.ProximityDither,
            FxPreset.PropOutlineOnly => ObjectFxFlags.OcclusionOutline | ObjectFxFlags.ForceOutline
                                        | ObjectFxFlags.ScanEdgeHighlight,
            FxPreset.All => ObjectFxFlags.All,
            _ => ObjectFxFlags.None,
        };

        void Awake()
        {
            if (preset != FxPreset.Custom)
                enabledFx = FlagsFromPreset(preset);
            EnsureRenderers();
        }

        void OnEnable()
        {
            var svc = FindObjectOfType<GraphicsFxService>();
            if (svc != null)
                svc.OnAnyChanged += RefreshDependents;
            RefreshDependents();
        }

        void OnDisable()
        {
            var svc = FindObjectOfType<GraphicsFxService>();
            if (svc != null)
                svc.OnAnyChanged -= RefreshDependents;
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (preset != FxPreset.Custom)
                enabledFx = FlagsFromPreset(preset);
            if (!Application.isPlaying)
                return;
            RefreshDependents();
        }
#endif

        /// <summary>刷新依赖组件与材质同步。</summary>
        public void RefreshDependents()
        {
            EnsureRenderers();

            ObjectFxFlags effective = MaterialFxSync.ResolveEffectiveFlags(this);
            MaterialFxSync.ApplyToRenderers(renderers, effective);

            var renderFx = GetComponent<CharacterRenderFX>();
            renderFx?.RefreshCapabilityGates();

            var reveal = GetComponent<ScanRevealVisual>();
            if (reveal != null && reveal.IsRevealed && !IsAllowed(ObjectFxFlags.ScanEdgeHighlight))
                reveal.SetRevealed(false);

            var proximity = GetComponent<ProximityDitherFade>();
            if (proximity != null && !IsAllowed(ObjectFxFlags.ProximityDither))
                renderFx?.SetProximityDither(0f);
        }

        void EnsureRenderers()
        {
            if (renderers != null && renderers.Length > 0)
                return;
            renderers = GetComponentsInChildren<Renderer>(true);
        }
    }
}
