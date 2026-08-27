using EGamePlay;
using EGamePlay.Combat;

namespace ACTGameEditor.Combat
{
    public static class CombatFxSourceResolver
    {
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
