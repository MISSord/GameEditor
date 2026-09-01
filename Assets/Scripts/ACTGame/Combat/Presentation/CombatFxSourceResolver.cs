using EGamePlay;
using EGamePlay.Combat;

namespace ACTGameEditor.Combat
{
    /// <summary>从伤害行动解析表现来源（优先技能 Runner Id）。</summary>
    public static class CombatFxSourceResolver
    {
        /// <summary>优先当前施法技能，其次 Trigger 来源技能，否则 Manual。</summary>
        public static CombatFxSource FromDamage(DamageAction damage)
        {
            if (TryResolveSkillSource(damage.Creator, out CombatFxSource source))
                return source;

            if (damage.TriggerContext.SourceAbility?.OwnerEntity is ICombatUnit owner
                && TryResolveSkillSource(owner, out source))
            {
                return source;
            }

            return CombatFxSource.Manual(damage.Id);
        }

        static bool TryResolveSkillSource(ICombatUnit unit, out CombatFxSource source)
        {
            source = default;
            if (unit is not CombatEntity combat || combat.SpellingExecution == null)
                return false;

            source = CombatFxSource.Skill(combat.SpellingExecution.Id);
            return true;
        }
    }
}
