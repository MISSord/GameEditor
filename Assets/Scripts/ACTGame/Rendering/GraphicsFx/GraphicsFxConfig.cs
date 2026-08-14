using UnityEngine;

namespace ACTGameEditor
{
    /// <summary>
    /// 图形效果默认开关配置（ScriptableObject）。
    /// </summary>
    [CreateAssetMenu(fileName = "GraphicsFxConfig", menuName = "ACTGame/Graphics Fx Config", order = 100)]
    public sealed class GraphicsFxConfig : ScriptableObject
    {
        [Header("Post Processing (Volume)")]
        public bool Bloom = true;
        public bool Vignette = true;
        public bool ColorAdjustments = true;
        public bool Tonemapping = true;

        [Header("URP")]
        public bool SSAO = true;
        public bool SoftShadows = true;

        [Header("Character / Gameplay FX")]
        public bool HitFlash = true;
        public bool Dissolve = true;
        public bool OcclusionOutline = true;
        public bool ForceOutline = true;
        public bool ScanPulse = true;
        public bool ScanEdgeHighlight = true;
        public bool RevealVision = true;
        public bool DepthVision = true;
        public bool PlayerFog = true;
        public bool ProximityDither = true;
        public bool Afterimage = true;

        /// <summary>
        /// 按 ID 读取默认值。
        /// </summary>
        public bool GetDefault(GraphicsFxId id)
        {
            return id switch
            {
                GraphicsFxId.Bloom => Bloom,
                GraphicsFxId.Vignette => Vignette,
                GraphicsFxId.ColorAdjustments => ColorAdjustments,
                GraphicsFxId.Tonemapping => Tonemapping,
                GraphicsFxId.SSAO => SSAO,
                GraphicsFxId.SoftShadows => SoftShadows,
                GraphicsFxId.HitFlash => HitFlash,
                GraphicsFxId.Dissolve => Dissolve,
                GraphicsFxId.OcclusionOutline => OcclusionOutline,
                GraphicsFxId.ForceOutline => ForceOutline,
                GraphicsFxId.ScanPulse => ScanPulse,
                GraphicsFxId.ScanEdgeHighlight => ScanEdgeHighlight,
                GraphicsFxId.RevealVision => RevealVision,
                GraphicsFxId.DepthVision => DepthVision,
                GraphicsFxId.PlayerFog => PlayerFog,
                GraphicsFxId.ProximityDither => ProximityDither,
                GraphicsFxId.Afterimage => Afterimage,
                _ => true,
            };
        }
    }
}
