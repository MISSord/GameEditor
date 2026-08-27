using System;

namespace ACTGameEditor.Combat
{
    public enum CombatFxTriggerKind : byte
    {
        ActionPoint = 0,
        TimelineMessage = 1,
        EntityDead = 2,
        Manual = 3,
    }

    [Flags]
    public enum CombatFxTriggerFlags : byte
    {
        None = 0,
        LocalTruePlayerOnly = 1 << 0,
        SkipOnDodge = 1 << 1,
        SkipOnImmunity = 1 << 2,
        SkipOnInterrupt = 1 << 3,
        RequirePositiveDamage = 1 << 4,
    }
}
