using System;

namespace ACTGameEditor
{
    /// <summary>
    /// 全局 / 对象效果开关 ID。
    /// </summary>
    public enum GraphicsFxId
    {
        /// <summary>泛光（Volume Bloom）</summary>
        Bloom = 0,
        /// <summary>暗角（Volume Vignette）</summary>
        Vignette = 1,
        /// <summary>色彩调整（曝光 / 对比 / 饱和）</summary>
        ColorAdjustments = 2,
        /// <summary>色调映射（HDR → LDR）</summary>
        Tonemapping = 3,
        /// <summary>屏幕空间环境光遮蔽</summary>
        SSAO = 4,
        /// <summary>主光软阴影</summary>
        SoftShadows = 5,

        /// <summary>受击闪白（角色 MPB _HitFlash）</summary>
        HitFlash = 10,
        /// <summary>噪声溶解（角色 MPB _Dissolve）</summary>
        Dissolve = 11,
        /// <summary>遮挡描边（被墙挡时显示）</summary>
        OcclusionOutline = 12,
        /// <summary>强制外轮廓（扫描揭示等）</summary>
        ForceOutline = 13,

        /// <summary>扫描脉冲（扩球 + 扫描逻辑）</summary>
        ScanPulse = 20,
        /// <summary>扫描边缘高亮（Fresnel 叠加）</summary>
        ScanEdgeHighlight = 21,
        /// <summary>球形 / 圆锥显现（Reveal + ScreenTint）</summary>
        RevealVision = 22,
        /// <summary>深度视界（近白远灰后处理）</summary>
        DepthVision = 23,
        /// <summary>玩家迷雾（盒体雾 + 清晰半径）</summary>
        PlayerFog = 24,
        /// <summary>近距镂空渐隐（相机贴脸 dither）</summary>
        ProximityDither = 25,
        /// <summary>闪避残影（Mesh 快照 Ghost）</summary>
        Afterimage = 26,
        /// <summary>色差（Volume CA，HitStop 冲击 pulse）</summary>
        ChromaticAberration = 27,
        /// <summary>径向模糊（RadialBlur Feature，HitStop 冲击 pulse）</summary>
        RadialBlur = 28,
    }

    /// <summary>
    /// 单对象可覆盖的效果位（与全局开关做 AND）。
    /// </summary>
    [Flags]
    public enum ObjectFxFlags
    {
        /// <summary>无</summary>
        None = 0,
        /// <summary>受击闪白</summary>
        HitFlash = 1 << 0,
        /// <summary>噪声溶解</summary>
        Dissolve = 1 << 1,
        /// <summary>遮挡描边（被墙挡时）</summary>
        OcclusionOutline = 1 << 2,
        /// <summary>强制外轮廓</summary>
        ForceOutline = 1 << 3,
        /// <summary>扫描边缘高亮</summary>
        ScanEdgeHighlight = 1 << 4,
        /// <summary>近距镂空渐隐</summary>
        ProximityDither = 1 << 5,
        /// <summary>闪避残影</summary>
        Afterimage = 1 << 6,
        /// <summary>全部对象级效果</summary>
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
