using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ACTGameEditor
{
    /// <summary>
    /// 玩家立方体迷雾状态（Renderer Feature 每帧读取）。
    /// </summary>
    public static class PlayerFogState
    {
        /// <summary>是否启用迷雾后处理。</summary>
        public static bool IsActive;

        /// <summary>迷雾盒中心（世界坐标，生成后固定）。</summary>
        public static Vector3 Center;

        /// <summary>可视清晰中心（跟随玩家）。</summary>
        public static Vector3 ClearCenter;

        /// <summary>迷雾盒尺寸（长宽高，完整边长）。</summary>
        public static Vector3 BoxSize = new Vector3(24f, 12f, 24f);

        /// <summary>玩家可视清晰半径（米）；之外逐渐被雾笼罩。</summary>
        public static float ClearRadius = 6f;

        /// <summary>清晰→全雾过渡宽度（米）。</summary>
        public static float FogFade = 4f;

        /// <summary>雾颜色。</summary>
        public static Color FogColor = new Color(0.55f, 0.62f, 0.72f, 1f);

        /// <summary>整体强度 0~1。</summary>
        public static float Intensity = 1f;

        /// <summary>0=水平距离(XZ)，1=三维距离。</summary>
        public static float HeightFalloff = 0f;

        /// <summary>天空是否也罩雾（默认开）。</summary>
        public static bool FogSky = true;

        /// <summary>写入状态。</summary>
        public static void Set(
            bool active,
            Vector3 boxCenter,
            Vector3 clearCenter,
            Vector3 boxSize,
            float clearRadius,
            float fogFade,
            Color fogColor,
            float intensity,
            float heightFalloff,
            bool fogSky)
        {
            IsActive = active;
            Center = boxCenter;
            ClearCenter = clearCenter;
            BoxSize = boxSize;
            ClearRadius = Mathf.Max(0.01f, clearRadius);
            FogFade = Mathf.Max(0.01f, fogFade);
            FogColor = fogColor;
            Intensity = Mathf.Clamp01(intensity);
            HeightFalloff = Mathf.Clamp01(heightFalloff);
            FogSky = fogSky;
        }

        /// <summary>关闭。</summary>
        public static void Clear()
        {
            IsActive = false;
            Intensity = 0f;
        }
    }

    /// <summary>
    /// URP 全屏玩家立方体迷雾。
    /// </summary>
    public sealed class PlayerFogFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public sealed class Settings
        {
            public RenderPassEvent injectionPoint = RenderPassEvent.BeforeRenderingPostProcessing;
            public Shader shader;
        }

        sealed class PlayerFogPass : ScriptableRenderPass
        {
            static readonly int FogColorId = Shader.PropertyToID("_FogColor");
            static readonly int IntensityId = Shader.PropertyToID("_Intensity");
            static readonly int FogCenterId = Shader.PropertyToID("_FogCenter");
            static readonly int ClearCenterId = Shader.PropertyToID("_ClearCenter");
            static readonly int FogHalfExtentsId = Shader.PropertyToID("_FogHalfExtents");
            static readonly int ClearRadiusId = Shader.PropertyToID("_ClearRadius");
            static readonly int FogFadeId = Shader.PropertyToID("_FogFade");
            static readonly int HeightFalloffId = Shader.PropertyToID("_HeightFalloff");
            static readonly int FogSkyId = Shader.PropertyToID("_FogSky");
            static readonly int TempId = Shader.PropertyToID("_ACT_PlayerFogTemp");

            readonly Material _material;
            RenderTargetIdentifier _source;

            public PlayerFogPass(Material material)
            {
                _material = material;
                profilingSampler = new ProfilingSampler("ACT Player Fog");
            }

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                ConfigureInput(ScriptableRenderPassInput.Depth);
                _source = renderingData.cameraData.renderer.cameraColorTarget;
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (_material == null || !PlayerFogState.IsActive || PlayerFogState.Intensity <= 0.001f)
                    return;

                Camera cam = renderingData.cameraData.camera;
                if (cam.cameraType != CameraType.Game)
                    return;

                Vector3 half = PlayerFogState.BoxSize * 0.5f;
                half.x = Mathf.Max(0.01f, half.x);
                half.y = Mathf.Max(0.01f, half.y);
                half.z = Mathf.Max(0.01f, half.z);

                _material.SetColor(FogColorId, PlayerFogState.FogColor);
                _material.SetFloat(IntensityId, PlayerFogState.Intensity);
                _material.SetVector(FogCenterId, PlayerFogState.Center);
                _material.SetVector(ClearCenterId, PlayerFogState.ClearCenter);
                _material.SetVector(FogHalfExtentsId, half);
                _material.SetFloat(ClearRadiusId, PlayerFogState.ClearRadius);
                _material.SetFloat(FogFadeId, PlayerFogState.FogFade);
                _material.SetFloat(HeightFalloffId, PlayerFogState.HeightFalloff);
                _material.SetFloat(FogSkyId, PlayerFogState.FogSky ? 1f : 0f);

                CommandBuffer cmd = CommandBufferPool.Get("ACT Player Fog");
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
        PlayerFogPass _pass;

        /// <inheritdoc />
        public override void Create()
        {
            if (settings.shader == null)
                settings.shader = Shader.Find("ACT/PlayerFog");

            if (settings.shader != null)
            {
                if (_material == null || _material.shader != settings.shader)
                    _material = CoreUtils.CreateEngineMaterial(settings.shader);
            }

            _pass = new PlayerFogPass(_material)
            {
                renderPassEvent = settings.injectionPoint
            };
        }

        /// <inheritdoc />
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!PlayerFogState.IsActive || _material == null)
                return;

            if (!GraphicsFxService.Query(GraphicsFxId.PlayerFog))
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
