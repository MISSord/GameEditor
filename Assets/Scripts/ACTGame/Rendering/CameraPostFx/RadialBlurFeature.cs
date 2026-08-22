using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ACTGameEditor
{
    /// <summary>
    /// 径向模糊全局状态（Renderer Feature 每帧读取）。
    /// </summary>
    public static class RadialBlurState
    {
        /// <summary>模糊强度 0~1。</summary>
        public static float Intensity;

        /// <summary>屏幕空间中心（0~1）。</summary>
        public static Vector2 Center = new Vector2(0.5f, 0.5f);

        /// <summary>采样次数。</summary>
        public static int SampleCount = 10;

        /// <summary>关闭。</summary>
        public static void Clear()
        {
            Intensity = 0f;
        }
    }

    /// <summary>
    /// URP 全屏径向模糊。
    /// </summary>
    public sealed class RadialBlurFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public sealed class Settings
        {
            public RenderPassEvent injectionPoint = RenderPassEvent.BeforeRenderingPostProcessing;
            public Shader shader;
        }

        sealed class RadialBlurPass : ScriptableRenderPass
        {
            static readonly int CenterId = Shader.PropertyToID("_Center");
            static readonly int IntensityId = Shader.PropertyToID("_Intensity");
            static readonly int SampleCountId = Shader.PropertyToID("_SampleCount");
            static readonly int TempId = Shader.PropertyToID("_ACT_RadialBlurTemp");

            readonly Material _material;
            RenderTargetIdentifier _source;

            public RadialBlurPass(Material material)
            {
                _material = material;
                profilingSampler = new ProfilingSampler("ACT Radial Blur");
            }

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                _source = renderingData.cameraData.renderer.cameraColorTarget;
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (_material == null || RadialBlurState.Intensity <= 0.001f)
                    return;

                if (!GraphicsFxService.Query(GraphicsFxId.RadialBlur))
                    return;

                Camera cam = renderingData.cameraData.camera;
                if (cam.cameraType != CameraType.Game && cam.cameraType != CameraType.SceneView)
                    return;

                _material.SetVector(CenterId, new Vector4(RadialBlurState.Center.x, RadialBlurState.Center.y, 0f, 0f));
                _material.SetFloat(IntensityId, RadialBlurState.Intensity);
                _material.SetFloat(SampleCountId, RadialBlurState.SampleCount);

                CommandBuffer cmd = CommandBufferPool.Get("ACT Radial Blur");
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
        RadialBlurPass _pass;

        /// <inheritdoc />
        public override void Create()
        {
            if (settings.shader == null)
                settings.shader = Shader.Find("ACT/RadialBlur");

            if (settings.shader != null)
            {
                if (_material == null || _material.shader != settings.shader)
                    _material = CoreUtils.CreateEngineMaterial(settings.shader);
            }

            _pass = new RadialBlurPass(_material)
            {
                renderPassEvent = settings.injectionPoint
            };
        }

        /// <inheritdoc />
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (RadialBlurState.Intensity <= 0.001f || _material == null)
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
