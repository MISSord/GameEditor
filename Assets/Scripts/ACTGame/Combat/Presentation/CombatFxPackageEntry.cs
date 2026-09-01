using System;
using UnityEngine;

namespace ACTGameEditor.Combat
{
    /// <summary>表现包内单条原子效果。</summary>
    [Serializable]
    public struct CombatFxPackageEntry
    {
        public CombatFxKind Kind;
        public CombatFxTargetMode TargetMode;

        [Tooltip("时长（秒）；0 表示使用该 Kind 的默认。")]
        public float Duration;

        [Tooltip("WorldScale；HitStop / TimeFracture 使用。")]
        [Range(0f, 1f)]
        public float WorldScale;

        [Tooltip("PlayerScale；SkillTimeStop 使用。")]
        [Range(0f, 1f)]
        public float PlayerScale;

        [Tooltip("HitStop 是否联动镜头 CA/RadialBlur。")]
        public bool PlayCameraImpact;

        [Tooltip("是否尊重 GraphicsFx 总开关。")]
        public bool RespectGraphicsGate;

        public static CombatFxPackageEntry HitFlash(float duration = 0.12f) => new CombatFxPackageEntry
        {
            Kind = CombatFxKind.HitFlash,
            TargetMode = CombatFxTargetMode.ActionTarget,
            Duration = duration,
            RespectGraphicsGate = true,
        };

        public static CombatFxPackageEntry HitStop(float duration = 0.08f, float worldScale = 0.08f, bool camera = true) =>
            new CombatFxPackageEntry
            {
                Kind = CombatFxKind.HitStop,
                TargetMode = CombatFxTargetMode.None,
                Duration = duration,
                WorldScale = worldScale,
                PlayCameraImpact = camera,
                RespectGraphicsGate = true,
            };

        public static CombatFxPackageEntry TimeFracture(float duration = 0.5f, float worldScale = 0.3f) =>
            new CombatFxPackageEntry
            {
                Kind = CombatFxKind.TimeFracture,
                TargetMode = CombatFxTargetMode.None,
                Duration = duration,
                WorldScale = worldScale,
                RespectGraphicsGate = true,
            };

        public static CombatFxPackageEntry DeathDissolve(float duration = 1.2f) => new CombatFxPackageEntry
        {
            Kind = CombatFxKind.DeathDissolve,
            TargetMode = CombatFxTargetMode.Owner,
            Duration = duration,
            RespectGraphicsGate = true,
        };
    }
}
