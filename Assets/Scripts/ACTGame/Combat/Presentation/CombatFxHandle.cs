using System;

namespace ACTGameEditor.Combat
{
    public readonly struct CombatFxHandle : IEquatable<CombatFxHandle>
    {
        public static readonly CombatFxHandle Invalid = default;
        public readonly int Id;

        public CombatFxHandle(int id) => Id = id;
        public bool IsValid => Id != 0;
        public bool Equals(CombatFxHandle other) => Id == other.Id;
        public override bool Equals(object obj) => obj is CombatFxHandle other && Equals(other);
        public override int GetHashCode() => Id;
    }
}
