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

        [Tooltip("WorldScale（断裂）或 HitStop 实体倍率。")]
        [Range(0f, 1f)]
        public float WorldScale;

        [Tooltip("PlayerScale；SkillTimeStop 使用。")]
        [Range(0f, 1f)]
        public float PlayerScale;

        [Tooltip("HitStop 是否联动镜头 CA/RadialBlur。")]
        public bool PlayCameraImpact;

        [Tooltip("时间效果 Priority；同类型刷新时更高者覆盖。0 用 Kind 默认。")]
        public int TimePriority;

        [Tooltip("是否尊重 GraphicsFx 总开关。")]
        public bool RespectGraphicsGate;

        public static CombatFxPackageEntry HitFlash(float duration = 0.12f) => new CombatFxPackageEntry
        {
            Kind = CombatFxKind.HitFlash,
            TargetMode = CombatFxTargetMode.ActionTarget,
            Duration = duration,
            RespectGraphicsGate = true,
        };

        public static CombatFxPackageEntry HitStop(float duration = 0.08f, float entityScale = 0.08f, bool camera = true, int timePriority = 10) =>
            new CombatFxPackageEntry
            {
                Kind = CombatFxKind.HitStop,
                TargetMode = CombatFxTargetMode.None,
                Duration = duration,
                WorldScale = entityScale,
                PlayCameraImpact = camera,
                TimePriority = timePriority,
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

        /// <summary>闪避残影（Owner 模型快照）。</summary>
        public static CombatFxPackageEntry Afterimage() => new CombatFxPackageEntry
        {
            Kind = CombatFxKind.Afterimage,
            TargetMode = CombatFxTargetMode.Owner,
            RespectGraphicsGate = true,
        };

        /// <summary>灰屏；Duration 与断裂窗口对齐，走 unscaled。</summary>
        public static CombatFxPackageEntry ScreenDesaturate(float duration = 0.5f) => new CombatFxPackageEntry
        {
            Kind = CombatFxKind.ScreenDesaturate,
            TargetMode = CombatFxTargetMode.None,
            Duration = duration,
            RespectGraphicsGate = true,
        };
    }
}
