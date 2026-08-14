using System;

namespace ACTGameEditor
{
    /// <summary>
    /// 全局 / 对象效果开关 ID。
    /// </summary>
    public enum GraphicsFxId
    {
        Bloom = 0,
        Vignette = 1,
        ColorAdjustments = 2,
        Tonemapping = 3,
        SSAO = 4,
        SoftShadows = 5,
        HitFlash = 10,
        Dissolve = 11,
        OcclusionOutline = 12,
        ForceOutline = 13,
        ScanPulse = 20,
        ScanEdgeHighlight = 21,
        RevealVision = 22,
        DepthVision = 23,
        PlayerFog = 24,
        ProximityDither = 25,
        Afterimage = 26,
    }

    /// <summary>
    /// 单对象可覆盖的效果位（与全局开关做 AND）。
    /// </summary>
    [Flags]
    public enum ObjectFxFlags
    {
        None = 0,
        HitFlash = 1 << 0,
        Dissolve = 1 << 1,
        OcclusionOutline = 1 << 2,
        ForceOutline = 1 << 3,
        ScanEdgeHighlight = 1 << 4,
        ProximityDither = 1 << 5,
        Afterimage = 1 << 6,
        All = HitFlash | Dissolve | OcclusionOutline | ForceOutline | ScanEdgeHighlight | ProximityDither | Afterimage,
    }

    /// <summary>
    /// GraphicsFxId 与 ObjectFxFlags 映射辅助。
    /// </summary>
    public static class GraphicsFxMapping
    {
        /// <summary>
        /// 将对象位标志转为全局 FxId；非对象级返回 null。
        /// </summary>
        public static bool TryToFxId(ObjectFxFlags flag, out GraphicsFxId id)
        {
            switch (flag)
            {
                case ObjectFxFlags.HitFlash:
                    id = GraphicsFxId.HitFlash;
                    return true;
                case ObjectFxFlags.Dissolve:
                    id = GraphicsFxId.Dissolve;
                    return true;
                case ObjectFxFlags.OcclusionOutline:
                    id = GraphicsFxId.OcclusionOutline;
                    return true;
                case ObjectFxFlags.ForceOutline:
                    id = GraphicsFxId.ForceOutline;
                    return true;
                case ObjectFxFlags.ScanEdgeHighlight:
                    id = GraphicsFxId.ScanEdgeHighlight;
                    return true;
                case ObjectFxFlags.ProximityDither:
                    id = GraphicsFxId.ProximityDither;
                    return true;
                case ObjectFxFlags.Afterimage:
                    id = GraphicsFxId.Afterimage;
                    return true;
                default:
                    id = default;
                    return false;
            }
        }
    }
}
