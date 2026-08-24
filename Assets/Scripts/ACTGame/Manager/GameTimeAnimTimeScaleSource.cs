using System;
using EGamePlay.Unity;

namespace ACTGameEditor
{
    /// <summary>将 <see cref="GameTimeManager"/> 桥接为动画时间源。</summary>
    public sealed class GameTimeAnimTimeScaleSource : IAnimTimeScaleSource
    {
        /// <summary>战斗场景默认时间源。</summary>
        public static readonly GameTimeAnimTimeScaleSource Default = new();

        /// <inheritdoc />
        public float PlayerScale => GameTimeManager.PlayerScale;

        /// <inheritdoc />
        public float PlayerTime => GameTimeManager.PlayerTime;

        /// <inheritdoc />
        public event Action OnTimeScaleChanged
        {
            add => GameTimeManager.OnTimeScaleChanged += value;
            remove => GameTimeManager.OnTimeScaleChanged -= value;
        }
    }
}
