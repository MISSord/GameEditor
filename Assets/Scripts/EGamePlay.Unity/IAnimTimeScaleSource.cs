using System;

namespace EGamePlay.Unity
{
    /// <summary>
    /// 动画系统使用的玩家层时间源（缩放、累计时间、变化通知）。
    /// </summary>
    public interface IAnimTimeScaleSource
    {
        /// <summary>玩家层时间缩放。</summary>
        float PlayerScale { get; }

        /// <summary>玩家层累计时间。</summary>
        float PlayerTime { get; }

        /// <summary>时间缩放变化时触发。</summary>
        event Action OnTimeScaleChanged;
    }
}
