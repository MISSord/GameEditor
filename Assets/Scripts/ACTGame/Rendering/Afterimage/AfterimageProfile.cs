using System;
using UnityEngine;

namespace ACTGameEditor
{
    /// <summary>
    /// 闪避残影参数：3 次快照时间点与初始透明度。
    /// </summary>
    [Serializable]
    public sealed class AfterimageProfile
    {
        public const int DefaultSnapshotCount = 3;

        [Tooltip("每个快照存活时间（秒）")]
        [Min(0.01f)]
        public float Lifetime = 0.15f;

        [Tooltip("相对 Play 时刻的采样延迟（秒），长度即快照数")]
        public float[] SnapshotDelays = { 0f, 0.04f, 0.08f };

        [Tooltip("各快照初始 Alpha，与 SnapshotDelays 对齐")]
        public float[] SnapshotAlphas = { 0.45f, 0.30f, 0.18f };

        [ColorUsage(true, true)]
        public Color GhostColor = new Color(0.35f, 0.75f, 1f, 0.85f);

        [Range(0f, 3f)]
        public float EmissionBoost = 1.2f;

        /// <summary>快照数量（取 Delays 长度，至少 1）。</summary>
        public int SnapshotCount => SnapshotDelays != null && SnapshotDelays.Length > 0
            ? SnapshotDelays.Length
            : DefaultSnapshotCount;

        /// <summary>拷贝默认闪避配置。</summary>
        public static AfterimageProfile CreateDodgeDefault() => new AfterimageProfile();
    }
}
