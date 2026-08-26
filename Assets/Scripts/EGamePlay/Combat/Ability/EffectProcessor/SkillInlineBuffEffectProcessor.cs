namespace EGamePlay.Combat
{
    /// <summary>
    /// 技能内联效果入口。实现已收到 EffectApplier；保留本类型以免旧调用点断裂。
    /// </summary>
    public static class SkillInlineBuffEffectProcessor
    {
        /// <summary>执行一条技能内联效果（开火即忘）。</summary>
        public static void Execute(
            BuffModifySetting setting,
            ICombatUnit caster,
            Entity target,
            Ability sourceAbility = null,
            int damageSegmentIndex = 0)
        {
            EffectApplier.ApplySkillInline(setting, caster, target, sourceAbility, damageSegmentIndex);
        }
    }
}
