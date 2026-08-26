using EGamePlay;
using UnityEngine;

namespace EGamePlay.Unity.Locomotion
{
    /// <summary>桥接 <see cref="GameTimeManager"/>（战斗场景）。</summary>
    public sealed class GameTimeLocomotionTimeSource : ILocomotionTimeSource
    {
        public float PlayerTime => GameTimeManager.PlayerTime;
        public float PlayerDelta => GameTimeManager.PlayerDelta;
        public float PlayerScale => GameTimeManager.PlayerScale;
        public float FixedPlayerDelta => Time.fixedDeltaTime * GameTimeManager.PlayerScale;
    }
}
