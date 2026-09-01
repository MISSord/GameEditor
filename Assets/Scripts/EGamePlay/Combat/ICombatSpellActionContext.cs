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
        /// <summary>本次出手技能 ID。</summary>
        int SkillId { get; }
        /// <summary>本次出手 Sort（槽位/连招边，用于区分普攻、闪避、大招等）。</summary>
        int Sort { get; }
    }
}
