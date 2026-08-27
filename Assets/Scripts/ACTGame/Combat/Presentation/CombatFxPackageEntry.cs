using System;
using UnityEngine;

namespace ACTGameEditor.Combat
{
    /// <summary>???????????</summary>
    [Serializable]
    public struct CombatFxPackageEntry
    {
        public CombatFxKind Kind;
        public CombatFxTargetMode TargetMode;

        [Tooltip("Duration seconds; 0 uses Kind default.")]
        public float Duration;

        [Tooltip("WorldScale for HitStop / TimeFracture.")]
        [Range(0f, 1f)]
        public float WorldScale;

        [Tooltip("PlayerScale for SkillTimeStop.")]
        [Range(0f, 1f)]
        public float PlayerScale;

        [Tooltip("Link camera CA/RadialBlur on HitStop.")]
        public bool PlayCameraImpact;

        [Tooltip("Respect GraphicsFx global gate.")]
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
