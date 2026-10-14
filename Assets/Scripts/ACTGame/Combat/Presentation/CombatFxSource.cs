using System;
using EGamePlay.Combat;

namespace ACTGameEditor.Combat
{
    /// <summary>表现效果来源，与 <see cref="TagSource"/> 对齐，便于 StopBySource 按技能同生命周期撤销。</summary>
    public readonly struct CombatFxSource : IEquatable<CombatFxSource>
    {
        /// <summary>与 <see cref="TagSource.Kind"/> 同一套枚举，技能 Break 时可同时撤 Tag 和特效。</summary>
        public readonly TagSourceKind Kind;
        /// <summary>与对应 <see cref="TagSource.Id"/> 对齐（Skill=runner，Buff=实体 Id）。</summary>
        public readonly long Id;

        public CombatFxSource(TagSourceKind kind, long id)
        {
            Kind = kind;
            Id = id;
        }

        public static CombatFxSource From(TagSource tag) => new CombatFxSource(tag.Kind, tag.Id);
        public static CombatFxSource Manual(long id = 0) => new CombatFxSource(TagSourceKind.Manual, id);
        public static CombatFxSource Skill(long runnerId) => new CombatFxSource(TagSourceKind.Skill, runnerId);
        public static CombatFxSource Buff(long buffId) => new CombatFxSource(TagSourceKind.Buff, buffId);
        public static CombatFxSource Entity(long entityId) => new CombatFxSource(TagSourceKind.Manual, entityId);

        public bool Equals(CombatFxSource other) => Kind == other.Kind && Id == other.Id;
        public override bool Equals(object obj) => obj is CombatFxSource other && Equals(other);
        public override int GetHashCode() => ((int)Kind * 397) ^ Id.GetHashCode();
    }
}
