using UnityEngine;

namespace ACTGameEditor
{
    /// <summary>大招构图预设。轴 Msg <c>SkillCamera</c> 的 StrMsg 填枚举名。</summary>
    public enum CameraShotPreset : byte
    {
        None = 0,
        /// <summary>起手 3/4 英雄镜，相对角色朝向。</summary>
        HeroThreeQuarter = 1,
        /// <summary>出手过肩战镜，相对锁敌连线。</summary>
        OverShoulder = 2,
        /// <summary>命中近景。</summary>
        CloseImpact = 3,
    }

    /// <summary>构图空间：方位角相对谁。</summary>
    public enum CameraShotSpace : byte
    {
        /// <summary>相对角色朝向。0=正后方拍背，180=正脸。</summary>
        CasterFacing = 0,
        /// <summary>相对玩家→锁定（或构图）目标。无目标时回退朝向。</summary>
        LockLine = 1,
    }

    /// <summary>
    /// 镜头配方：每帧相对角色解成朝向/距离，不存死世界坐标。
    /// </summary>
    public struct CameraShotRecipe
    {
        public CameraShotPreset Preset;
        public CameraShotSpace Space;
        public float Azimuth;
        public float Pitch;
        public float Distance;
        public float LookHeight;
        public float Weight;

        /// <summary>距离和权重都有效才参与混合。</summary>
        public bool IsValid => Distance > 0f && Weight > 0f;

        /// <summary>按预设取配方。未知预设返回 default（IsValid=false）。</summary>
        public static CameraShotRecipe FromPreset(CameraShotPreset preset)
        {
            switch (preset)
            {
                case CameraShotPreset.HeroThreeQuarter:
                    return new CameraShotRecipe
                    {
                        Preset = preset,
                        Space = CameraShotSpace.CasterFacing,
                        Azimuth = 140f,
                        Pitch = 8f,
                        Distance = 3.2f,
                        LookHeight = 0.35f,
                        Weight = 1f,
                    };
                case CameraShotPreset.OverShoulder:
                    return new CameraShotRecipe
                    {
                        Preset = preset,
                        Space = CameraShotSpace.LockLine,
                        Azimuth = 30f,
                        Pitch = 12f,
                        Distance = 4.5f,
                        LookHeight = 0.2f,
                        Weight = 0.75f,
                    };
                case CameraShotPreset.CloseImpact:
                    return new CameraShotRecipe
                    {
                        Preset = preset,
                        Space = CameraShotSpace.LockLine,
                        Azimuth = 20f,
                        Pitch = 10f,
                        Distance = 2.6f,
                        LookHeight = 0.15f,
                        Weight = 1f,
                    };
                default:
                    return default;
            }
        }

        /// <summary>解析轴 StrMsg。大小写不敏感，未识别返回 false。</summary>
        public static bool TryFromName(string name, out CameraShotRecipe recipe)
        {
            recipe = default;
            if (string.IsNullOrEmpty(name))
                return false;
            if (name.Equals(nameof(CameraShotPreset.HeroThreeQuarter), System.StringComparison.OrdinalIgnoreCase))
            {
                recipe = FromPreset(CameraShotPreset.HeroThreeQuarter);
                return true;
            }
            if (name.Equals(nameof(CameraShotPreset.OverShoulder), System.StringComparison.OrdinalIgnoreCase))
            {
                recipe = FromPreset(CameraShotPreset.OverShoulder);
                return true;
            }
            if (name.Equals(nameof(CameraShotPreset.CloseImpact), System.StringComparison.OrdinalIgnoreCase))
            {
                recipe = FromPreset(CameraShotPreset.CloseImpact);
                return true;
            }

            return false;
        }
    }
}
