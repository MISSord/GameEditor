using System;
using UnityEngine;

namespace EGamePlay.Unity
{
    /// <summary>未注入战斗时间源时的兜底（无缩放、无事件）。</summary>
    public sealed class UnityAnimTimeScaleSource : IAnimTimeScaleSource
    {
        /// <summary>默认兜底实例。</summary>
        public static readonly UnityAnimTimeScaleSource Default = new();

        /// <inheritdoc />
        public float PlayerScale => 1f;

        /// <inheritdoc />
        public float PlayerTime => Time.time;

#pragma warning disable 67
        /// <inheritdoc />
        public event Action OnTimeScaleChanged;
#pragma warning restore 67
    }
}
