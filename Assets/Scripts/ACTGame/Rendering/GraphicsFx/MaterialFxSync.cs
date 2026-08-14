using UnityEngine;
using UnityEngine.Rendering;

namespace ACTGameEditor
{
    /// <summary>
    /// 按对象能力同步材质 Keyword / Shader Pass，避免冷效果仍占变体或多余 Pass。
    /// </summary>
    public static class MaterialFxSync
    {
        public const string KwProximityDither = "_PROXIMITY_DITHER_ON";
        public const string PassOcclusionOutline = "OcclusionOutline";
        public const string PassForcedOutline = "ForcedOutline";

        /// <summary>
        /// 将 <paramref name="flags"/>（通常已与全局开关 AND）同步到 Renderer 的 sharedMaterials。
        /// </summary>
        public static void ApplyToRenderers(Renderer[] renderers, ObjectFxFlags flags)
        {
            if (renderers == null)
                return;

            bool dither = (flags & ObjectFxFlags.ProximityDither) != 0;
            bool occlusionOutline = (flags & ObjectFxFlags.OcclusionOutline) != 0;
            bool forceOutline = (flags & ObjectFxFlags.ForceOutline) != 0;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null)
                    continue;

                Material[] mats = r.sharedMaterials;
                if (mats == null)
                    continue;

                for (int m = 0; m < mats.Length; m++)
                {
                    Material mat = mats[m];
                    if (mat == null)
                        continue;

                    ApplyToMaterial(mat, dither, occlusionOutline, forceOutline);
                }
            }
        }

        /// <summary>同步单材质。</summary>
        public static void ApplyToMaterial(Material mat, bool proximityDither, bool occlusionOutline, bool forceOutline)
        {
            if (mat == null)
                return;

            // shader_feature_local：无能力则变体不含镂空指令
            if (proximityDither)
                mat.EnableKeyword(KwProximityDither);
            else
                mat.DisableKeyword(KwProximityDither);

            // 整 Pass 裁剪（比参数=0 更能省 DrawCall）
            if (mat.GetShaderPassEnabled(PassOcclusionOutline) != occlusionOutline)
                mat.SetShaderPassEnabled(PassOcclusionOutline, occlusionOutline);

            if (mat.GetShaderPassEnabled(PassForcedOutline) != forceOutline)
                mat.SetShaderPassEnabled(PassForcedOutline, forceOutline);
        }

        /// <summary>
        /// 解析「对象允许 ∧ 全局开启」后的有效 Flags（无 ObjectFx 时视为全开再与全局 AND）。
        /// </summary>
        public static ObjectFxFlags ResolveEffectiveFlags(ObjectFxController objectFx)
        {
            ObjectFxFlags local = objectFx != null ? objectFx.EnabledFx : ObjectFxFlags.All;
            ObjectFxFlags result = ObjectFxFlags.None;

            TryAdd(ref result, local, ObjectFxFlags.HitFlash, GraphicsFxId.HitFlash);
            TryAdd(ref result, local, ObjectFxFlags.Dissolve, GraphicsFxId.Dissolve);
            TryAdd(ref result, local, ObjectFxFlags.OcclusionOutline, GraphicsFxId.OcclusionOutline);
            TryAdd(ref result, local, ObjectFxFlags.ForceOutline, GraphicsFxId.ForceOutline);
            TryAdd(ref result, local, ObjectFxFlags.ScanEdgeHighlight, GraphicsFxId.ScanEdgeHighlight);
            TryAdd(ref result, local, ObjectFxFlags.ProximityDither, GraphicsFxId.ProximityDither);
            TryAdd(ref result, local, ObjectFxFlags.Afterimage, GraphicsFxId.Afterimage);
            return result;
        }

        static void TryAdd(ref ObjectFxFlags result, ObjectFxFlags local, ObjectFxFlags flag, GraphicsFxId id)
        {
            if ((local & flag) == 0)
                return;
            if (!GraphicsFxService.Query(id))
                return;
            result |= flag;
        }
    }
}
