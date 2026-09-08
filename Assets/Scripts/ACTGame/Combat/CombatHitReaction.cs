using EGamePlay.Combat;

namespace ACTGameEditor.Combat
{
    /// <summary>
    /// 受击表现分级。闪白/顿帧读段表 <see cref="SkillDamageSetting.HitReaction"/>。
    /// 断招走 <see cref="CombatInterrupt"/>，不按 Light/Heavy 布尔，也不在这里问霸体 Tag。
    /// </summary>
    public static class CombatHitReaction
    {
        /// <summary>是否进入短硬直并断招。仅技能直伤且打断 ≥ 抗打断。</summary>
        public static bool ShouldInterruptSkill(DamageAction damage, int antiInterruptLevel)
        {
            return CombatInterrupt.ShouldBreakSkill(damage, antiInterruptLevel);
        }
    }
}
