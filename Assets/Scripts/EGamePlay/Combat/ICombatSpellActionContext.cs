using UnityEngine;

namespace EGamePlay.Combat
{
    /// <summary>施法行动上下文，供 PreSpell/PostSpell 行动点消费。</summary>
    public interface ICombatSpellActionContext
    {
        ICombatUnit Caster { get; }
        ICombatUnit InputTarget { get; }
        Vector3 InputPoint { get; }
        Vector3 InputDirection { get; }
    }
}
