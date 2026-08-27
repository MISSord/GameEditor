using System;
using EGamePlay.Combat;

namespace ACTGameEditor.Combat
{
    /// <summary>??????????????</summary>
    public struct CombatFxPlayContext
    {
        public CombatFxSource Source;
        public ICombatUnit Owner;
        public ICombatUnit ActionTarget;
        public ICombatUnit ActionCreator;
        public ICombatUnit ExplicitTarget;
        public Action OnComplete;
        public float DurationOverride;

        public static CombatFxPlayContext ForOwner(ICombatUnit owner, in CombatFxSource source) =>
            new CombatFxPlayContext { Owner = owner, Source = source };
    }
}
