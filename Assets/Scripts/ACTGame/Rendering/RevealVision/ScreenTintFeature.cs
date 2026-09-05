using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ACTGameEditor
{
    /// <summary>
    /// 全屏染色状态（由球形显现在“玩家在球内”时驱动）。
    /// </summary>
    public static class ScreenTintState
    {
        /// <summary>是否叠加染色。</summary>
        public static bool IsActive;

        /// <summary>叠加颜色（默认浅蓝）。</summary>
        public static Color TintColor = new Color(0.55f, 0.78f, 1f, 1f);

        /// <summary>混合强度 0~1。</summary>
        public static float Intensity = 0.28f;

        /// <summary>去饱和 0~1（Perfect Dodge 灰屏，与染色可叠加）。</summary>
        public static float Desaturate;

        /// <summary>染色或灰屏任一开启则画 Pass。</summary>
        public static bool ShouldRender =>
            (IsActive && Intensity > 0.001f) || Desaturate > 0.001f;

        /// <summary>写入染色。</summary>
        public static void Set(bool active, Color color, float intensity)
        {
            IsActive = active;
            TintColor = color;
            Intensity = Mathf.Clamp01(intensity);
        }

        /// <summary>写入去饱和（不改染色）。</summary>
        public static void SetDesaturate(float amount) =>
            Desaturate = Mathf.Clamp01(amount);

        /// <summary>关闭染色（保留灰屏）。</summary>
        public static void Clear()
        {
            IsActive = false;
            Intensity = 0f;
        }

        /// <summary>关闭灰屏。</summary>
        public static void ClearDesaturate() => Desaturate = 0f;
    }

    /// <summary>
    /// URP 全屏颜色叠加。
    /// </summary>
    public sealed class ScreenTintFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public sealed class Settings
        {
            public RenderPassEvent injectionPoint = RenderPassEvent.BeforeRenderingPostProcessing;
            public Shader shader;
        }

        sealed class ScreenTintPass : ScriptableRenderPass
        {
            static readonly int TintColorId = Shader.PropertyToID("_TintColor");
            static readonly int IntensityId = Shader.PropertyToID("_Intensity");
            static readonly int DesaturateId = Shader.PropertyToID("_Desaturate");
            static readonly int TempId = Shader.PropertyToID("_ACT_ScreenTintTemp");

            readonly Material _material;
            RenderTargetIdentifier _source;

            public ScreenTintPass(Material material)
            {
                _material = material;
                profilingSampler = new ProfilingSampler("ACT Screen Tint");
            }

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                _source = renderingData.cameraData.renderer.cameraColorTarget;
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (_material == null || !ScreenTintState.ShouldRender)
                    return;

                Camera cam = renderingData.cameraData.camera;
                if (cam.cameraType != CameraType.Game)
                    return;

                _material.SetColor(TintColorId, ScreenTintState.TintColor);
                _material.SetFloat(IntensityId, ScreenTintState.IsActive ? ScreenTintState.Intensity : 0f);
                _material.SetFloat(DesaturateId, ScreenTintState.Desaturate);

                CommandBuffer cmd = CommandBufferPool.Get("ACT Screen Tint");
                using (new ProfilingScope(cmd, profilingSampler))
                {
                    RenderTextureDescriptor desc = renderingData.cameraData.cameraTargetDescriptor;
                    desc.depthBufferBits = 0;
                    desc.msaaSamples = 1;
                    cmd.GetTemporaryRT(TempId, desc, FilterMode.Bilinear);
                    Blit(cmd, _source, TempId, _material, 0);
                    Blit(cmd, TempId, _source);
                    cmd.ReleaseTemporaryRT(TempId);
                }

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }
        }

        public Settings settings = new Settings();
        Material _material;
        ScreenTintPass _pass;

        /// <inheritdoc />
        public override void Create()
        {
            if (settings.shader == null)
                settings.shader = Shader.Find("ACT/ScreenTint");

            if (settings.shader != null)
            {
                if (_material == null || _material.shader != settings.shader)
                    _material = CoreUtils.CreateEngineMaterial(settings.shader);
            }

            _pass = new ScreenTintPass(_material)
            {
                renderPassEvent = settings.injectionPoint
            };
        }

        /// <inheritdoc />
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!ScreenTintState.ShouldRender || _material == null)
                return;

            _pass.renderPassEvent = settings.injectionPoint;
            renderer.EnqueuePass(_pass);
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(_material);
            _material = null;
        }
    }
}
