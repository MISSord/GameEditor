using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ACTGameEditor
{
    /// <summary>
    /// 深度视界全局状态（Renderer Feature 每帧读取，零查找）。
    /// </summary>
    public static class DepthVisionState
    {
        static readonly int IncludeCharacterDepthId = Shader.PropertyToID("_ACT_IncludeCharacterDepth");

        static bool _includeCharacterDepth = true;

        /// <summary>是否启用全屏深度着色。</summary>
        public static bool IsActive;

        /// <summary>
        /// ACT/Character 是否写入相机深度（ScanTarget / 角色要进深度视界时需开启）。
        /// </summary>
        public static bool IncludeCharacterDepth
        {
            get => _includeCharacterDepth;
            set
            {
                _includeCharacterDepth = value;
                ApplyCharacterDepthKeyword();
            }
        }

        /// <summary>近处颜色（默认白）。</summary>
        public static Color NearColor = Color.white;

        /// <summary>远处颜色（默认深灰）。</summary>
        public static Color FarColor = new Color(0.18f, 0.18f, 0.18f, 1f);

        /// <summary>近裁距离（米，线性眼深度）。</summary>
        public static float DepthNear = 1f;

        /// <summary>远裁距离（米）。</summary>
        public static float DepthFar = 35f;

        /// <summary>与原画面混合强度 0~1。</summary>
        public static float Intensity = 1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void InitGlobals()
        {
            ApplyCharacterDepthKeyword();
        }

        /// <summary>同步全局 Shader 参数。</summary>
        public static void ApplyCharacterDepthKeyword()
        {
            Shader.SetGlobalFloat(IncludeCharacterDepthId, _includeCharacterDepth ? 1f : 0f);
        }
    }

    /// <summary>
    /// URP Renderer Feature：按深度将画面映射为白→深灰。
    /// </summary>
    public sealed class DepthVisionFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public sealed class Settings
        {
            public RenderPassEvent injectionPoint = RenderPassEvent.BeforeRenderingPostProcessing;
            public Shader shader;
        }

        sealed class DepthVisionPass : ScriptableRenderPass
        {
            static readonly int NearColorId = Shader.PropertyToID("_NearColor");
            static readonly int FarColorId = Shader.PropertyToID("_FarColor");
            static readonly int DepthNearId = Shader.PropertyToID("_DepthNear");
            static readonly int DepthFarId = Shader.PropertyToID("_DepthFar");
            static readonly int IntensityId = Shader.PropertyToID("_Intensity");
            static readonly int TempId = Shader.PropertyToID("_ACT_DepthVisionTemp");

            readonly Material _material;
            RenderTargetIdentifier _source;

            public DepthVisionPass(Material material)
            {
                _material = material;
                profilingSampler = new ProfilingSampler("ACT Depth Vision");
            }

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                ConfigureInput(ScriptableRenderPassInput.Depth);
                _source = renderingData.cameraData.renderer.cameraColorTarget;
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (_material == null)
                    return;

                Camera cam = renderingData.cameraData.camera;
                if (cam.cameraType != CameraType.Game && cam.cameraType != CameraType.SceneView)
                    return;

                _material.SetColor(NearColorId, DepthVisionState.NearColor);
                _material.SetColor(FarColorId, DepthVisionState.FarColor);
                _material.SetFloat(DepthNearId, DepthVisionState.DepthNear);
                _material.SetFloat(DepthFarId, Mathf.Max(DepthVisionState.DepthNear + 0.01f, DepthVisionState.DepthFar));
                _material.SetFloat(IntensityId, DepthVisionState.Intensity);

                CommandBuffer cmd = CommandBufferPool.Get("ACT Depth Vision");
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
        DepthVisionPass _pass;

        /// <inheritdoc />
        public override void Create()
        {
            if (settings.shader == null)
                settings.shader = Shader.Find("ACT/DepthVision");

            if (settings.shader != null)
            {
                if (_material == null || _material.shader != settings.shader)
                    _material = CoreUtils.CreateEngineMaterial(settings.shader);
            }

            _pass = new DepthVisionPass(_material)
            {
                renderPassEvent = settings.injectionPoint
            };
        }

        /// <inheritdoc />
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!DepthVisionState.IsActive || _material == null)
                return;

            if (!GraphicsFxService.Query(GraphicsFxId.DepthVision))
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
