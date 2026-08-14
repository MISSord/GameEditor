using UnityEngine;

namespace ACTGameEditor
{
    /// <summary>
    /// 显现可见性驱动方式。
    /// </summary>
    public enum RevealVisibilityDrive
    {
        /// <summary>CPU 开关 Renderer / 粒子（旧球形逻辑可用）。</summary>
        CpuRenderer = 0,
        /// <summary>Renderer 常开，由 ACT/RevealMasked 按球/锥遮罩裁剪（推荐）。</summary>
        ShaderMask = 1,
    }

    /// <summary>
    /// 显现对象的显示方式（CpuRenderer 模式）。
    /// </summary>
    public enum RevealSubjectMode
    {
        GameObjectActive = 0,
        RendererEnable = 1,
        CanvasGroupAlpha = 2,
        Particle = 3,
    }

    /// <summary>
    /// 平时隐藏的显现物。ShaderMask 模式下仅球/锥范围内可见。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RevealVisionSubject : MonoBehaviour
    {
        [SerializeField]
        RevealChannel channel = RevealChannel.Default;

        [SerializeField]
        RevealVisibilityDrive visibilityDrive = RevealVisibilityDrive.ShaderMask;

        [Tooltip("ShaderMask 模式下自动把材质换成 ACT/RevealMasked（否则圆锥/球扫不到）")]
        [SerializeField]
        bool autoApplyRevealMaskedMaterial = true;

        [SerializeField]
        RevealSubjectMode mode = RevealSubjectMode.RendererEnable;

        [SerializeField]
        Transform probePoint;

        [SerializeField]
        bool startHidden = true;

        [SerializeField]
        float visibleAlpha = 1f;

        [SerializeField]
        float hiddenAlpha = 0f;

        [SerializeField]
        Renderer[] renderers;

        [SerializeField]
        CanvasGroup canvasGroup;

        [SerializeField]
        ParticleSystem[] particles;

        bool _visible;

        /// <summary>所属频道。</summary>
        public RevealChannel Channel => channel;

        /// <summary>驱动方式。</summary>
        public RevealVisibilityDrive VisibilityDrive => visibilityDrive;

        /// <summary>CPU 模式下是否显现。</summary>
        public bool IsVisible => _visible;

        /// <summary>距离判定用世界坐标。</summary>
        public Vector3 WorldPosition =>
            probePoint != null ? probePoint.position : transform.position;

        void Awake()
        {
            CacheRefs();
            if (visibilityDrive == RevealVisibilityDrive.ShaderMask)
            {
                if (autoApplyRevealMaskedMaterial)
                    EnsureRevealMaskedMaterials();
                ApplyRendererEnable(true);
            }
            else if (startHidden)
            {
                ApplyVisible(false, force: true);
            }
        }

        void OnEnable()
        {
            RevealVisionService.Register(this);
            if (visibilityDrive == RevealVisibilityDrive.ShaderMask)
                ApplyRendererEnable(true);
        }

        void OnDisable()
        {
            RevealVisionService.Unregister(this);
            if (visibilityDrive == RevealVisibilityDrive.CpuRenderer && _visible)
                ApplyVisible(false, force: true);
        }

        /// <summary>CPU 驱动时由球形控制器调用；ShaderMask 下可忽略。</summary>
        public void SetRevealed(bool revealed)
        {
            if (visibilityDrive == RevealVisibilityDrive.ShaderMask)
                return;

            if (revealed && !GraphicsFxService.Query(GraphicsFxId.RevealVision))
                revealed = false;

            var svc = FindObjectOfType<RevealVisionService>();
            if (revealed && svc != null && !svc.IsChannelAllowed(channel))
                revealed = false;

            ApplyVisible(revealed, force: false);
        }

        void CacheRefs()
        {
            if (renderers == null || renderers.Length == 0)
                renderers = GetComponentsInChildren<Renderer>(true);

            if (mode == RevealSubjectMode.CanvasGroupAlpha && canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>() ?? GetComponentInChildren<CanvasGroup>(true);

            if (mode == RevealSubjectMode.Particle && (particles == null || particles.Length == 0))
                particles = GetComponentsInChildren<ParticleSystem>(true);
        }

        /// <summary>
        /// 把 Renderer 材质换成 ACT/RevealMasked（保留原颜色），否则遮罩裁剪无效。
        /// </summary>
        public void EnsureRevealMaskedMaterials()
        {
            CacheRefs();
            if (renderers == null)
                return;

            Shader shader = Shader.Find("ACT/RevealMasked");
            if (shader == null)
            {
                Debug.LogWarning($"[RevealVision] 找不到 Shader ACT/RevealMasked，无法处理 {name}", this);
                return;
            }

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null)
                    continue;

                Material[] mats = r.materials;
                bool dirty = false;
                for (int m = 0; m < mats.Length; m++)
                {
                    Material src = mats[m];
                    if (src != null && src.shader == shader)
                        continue;

                    Color keep = Color.cyan;
                    if (src != null)
                    {
                        if (src.HasProperty("_BaseColor"))
                            keep = src.GetColor("_BaseColor");
                        else if (src.HasProperty("_Color"))
                            keep = src.color;
                    }

                    var mat = new Material(shader) { name = $"{name}_RevealMasked" };
                    if (mat.HasProperty("_Color"))
                        mat.SetColor("_Color", keep);
                    mats[m] = mat;
                    dirty = true;
                }

                if (dirty)
                    r.materials = mats;
            }
        }

        void ApplyVisible(bool visible, bool force)
        {
            if (!force && _visible == visible)
                return;

            _visible = visible;
            CacheRefs();

            switch (mode)
            {
                case RevealSubjectMode.GameObjectActive:
                    if (transform.childCount > 0)
                    {
                        for (int i = 0; i < transform.childCount; i++)
                            transform.GetChild(i).gameObject.SetActive(visible);
                    }
                    else
                    {
                        ApplyRendererEnable(visible);
                    }
                    break;

                case RevealSubjectMode.RendererEnable:
                    ApplyRendererEnable(visible);
                    break;

                case RevealSubjectMode.CanvasGroupAlpha:
                    if (canvasGroup != null)
                    {
                        canvasGroup.alpha = visible ? visibleAlpha : hiddenAlpha;
                        canvasGroup.blocksRaycasts = visible;
                        canvasGroup.interactable = visible;
                    }
                    break;

                case RevealSubjectMode.Particle:
                    ApplyParticles(visible);
                    break;
            }
        }

        void ApplyRendererEnable(bool visible)
        {
            if (renderers == null || renderers.Length == 0)
                renderers = GetComponentsInChildren<Renderer>(true);

            if (renderers == null)
                return;

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                    renderers[i].enabled = visible;
            }
        }

        void ApplyParticles(bool visible)
        {
            if (particles == null || particles.Length == 0)
                particles = GetComponentsInChildren<ParticleSystem>(true);

            if (particles == null)
                return;

            for (int i = 0; i < particles.Length; i++)
            {
                ParticleSystem ps = particles[i];
                if (ps == null)
                    continue;

                if (visible)
                {
                    if (!ps.isPlaying)
                        ps.Play(true);
                }
                else
                {
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }
        }
    }
}
