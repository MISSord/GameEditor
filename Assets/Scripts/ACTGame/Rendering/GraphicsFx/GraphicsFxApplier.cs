using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ACTGameEditor
{
    /// <summary>
    /// 将 <see cref="GraphicsFxService"/> 开关应用到 Volume / URP Feature / 灯光。
    /// </summary>
    public static class GraphicsFxApplier
    {
        static VolumeProfile _runtimeProfile;
        static ScriptableRendererFeature _ssaoFeature;
        static bool _ssaoResolved;

        /// <summary>
        /// 应用全部全局效果。
        /// </summary>
        public static void ApplyAll(GraphicsFxService service)
        {
            if (service == null)
                return;

            ApplyVolume(service);
            ApplySsao(service);
            ApplySoftShadows(service);
        }

        /// <summary>
        /// 应用单个全局效果。
        /// </summary>
        public static void Apply(GraphicsFxService service, GraphicsFxId id)
        {
            if (service == null)
                return;

            switch (id)
            {
                case GraphicsFxId.Bloom:
                case GraphicsFxId.Vignette:
                case GraphicsFxId.ColorAdjustments:
                case GraphicsFxId.Tonemapping:
                    ApplyVolume(service);
                    break;
                case GraphicsFxId.SSAO:
                    ApplySsao(service);
                    break;
                case GraphicsFxId.SoftShadows:
                    ApplySoftShadows(service);
                    break;
                // 角色/扫描类由各组件入口查询 GraphicsFxService.Query，无需全局 Apply
            }
        }

        static void ApplyVolume(GraphicsFxService service)
        {
            EnsureRuntimeProfile();
            if (_runtimeProfile == null)
                return;

            SetVolumeActive<Bloom>(_runtimeProfile, service.IsEnabled(GraphicsFxId.Bloom));
            SetVolumeActive<Vignette>(_runtimeProfile, service.IsEnabled(GraphicsFxId.Vignette));
            SetVolumeActive<ColorAdjustments>(_runtimeProfile, service.IsEnabled(GraphicsFxId.ColorAdjustments));
            SetVolumeActive<Tonemapping>(_runtimeProfile, service.IsEnabled(GraphicsFxId.Tonemapping));
        }

        static void EnsureRuntimeProfile()
        {
            if (_runtimeProfile != null)
                return;

            var volumes = Object.FindObjectsOfType<Volume>();
            if (volumes == null || volumes.Length == 0)
                return;

            // 优先 Global Volume
            Volume target = null;
            for (int i = 0; i < volumes.Length; i++)
            {
                if (volumes[i] != null && volumes[i].isGlobal)
                {
                    target = volumes[i];
                    break;
                }
            }

            target ??= volumes[0];
            if (target.sharedProfile == null)
                return;

            // 运行时副本，避免改脏工程里的 shared asset
            _runtimeProfile = Object.Instantiate(target.sharedProfile);
            _runtimeProfile.name = target.sharedProfile.name + " (Runtime)";
            target.profile = _runtimeProfile;
        }

        static void SetVolumeActive<T>(VolumeProfile profile, bool active) where T : VolumeComponent
        {
            if (profile.TryGet(out T component))
                component.active = active;
        }

        static void ApplySsao(GraphicsFxService service)
        {
            if (!_ssaoResolved)
            {
                _ssaoFeature = FindSsaoFeature();
                _ssaoResolved = true;
            }

            if (_ssaoFeature != null)
                _ssaoFeature.SetActive(service.IsEnabled(GraphicsFxId.SSAO));
        }

        static ScriptableRendererFeature FindSsaoFeature()
        {
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urp == null)
                return null;

            // 优先从 RendererData 资产列表取 Feature（可直接 SetActive，且不依赖运行时 Renderer 实例）
            var dataListField = typeof(UniversalRenderPipelineAsset).GetField(
                "m_RendererDataList",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (dataListField?.GetValue(urp) is ScriptableRendererData[] dataList)
            {
                for (int d = 0; d < dataList.Length; d++)
                {
                    ScriptableRendererData data = dataList[d];
                    if (data == null)
                        continue;

                    var features = data.rendererFeatures;
                    if (features == null)
                        continue;

                    for (int i = 0; i < features.Count; i++)
                    {
                        ScriptableRendererFeature f = features[i];
                        if (f != null && f.GetType().Name.Contains("ScreenSpaceAmbientOcclusion"))
                            return f;
                    }
                }
            }

            // 回退：运行时 ScriptableRenderer.rendererFeatures
            var getRenderer = typeof(UniversalRenderPipelineAsset).GetMethod(
                "GetRenderer",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (getRenderer == null)
                return null;

            object renderer = getRenderer.Invoke(urp, new object[] { 0 });
            if (renderer == null)
                return null;

            var featuresProp = typeof(ScriptableRenderer).GetProperty(
                "rendererFeatures",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (featuresProp?.GetValue(renderer) is not System.Collections.Generic.List<ScriptableRendererFeature> list)
                return null;

            for (int i = 0; i < list.Count; i++)
            {
                ScriptableRendererFeature f = list[i];
                if (f != null && f.GetType().Name.Contains("ScreenSpaceAmbientOcclusion"))
                    return f;
            }

            return null;
        }

        static void ApplySoftShadows(GraphicsFxService service)
        {
            bool soft = service.IsEnabled(GraphicsFxId.SoftShadows);
            var lights = Object.FindObjectsOfType<Light>();
            for (int i = 0; i < lights.Length; i++)
            {
                Light light = lights[i];
                if (light == null || light.shadows == LightShadows.None)
                    continue;

                // 仅在 Soft/Hard 间切换，不关掉阴影本身
                light.shadows = soft ? LightShadows.Soft : LightShadows.Hard;
            }
        }

        /// <summary>
        /// 场景切换后可调用，重新绑定 Volume / Feature。
        /// </summary>
        public static void InvalidateCache()
        {
            _runtimeProfile = null;
            _ssaoFeature = null;
            _ssaoResolved = false;
        }
    }
}
