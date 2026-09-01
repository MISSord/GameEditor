using System;
using EGamePlay.Combat;

namespace ACTGameEditor.Combat
{
    /// <summary>播放表现包时的运行时上下文。</summary>
    public struct CombatFxPlayContext
    {
        public CombatFxSource Source;
        public ICombatUnit Owner;
        public ICombatUnit ActionTarget;
        public ICombatUnit ActionCreator;
        public ICombatUnit ExplicitTarget;
        public Action OnComplete;
        /// <summary>&gt; 0 时覆盖 Package Entry 默认 Duration（如死亡溶解时长）。</summary>
        public float DurationOverride;

        public static CombatFxPlayContext ForOwner(ICombatUnit owner, in CombatFxSource source) =>
            new CombatFxPlayContext { Owner = owner, Source = source };
    }
}
