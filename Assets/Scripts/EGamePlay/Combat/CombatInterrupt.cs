namespace EGamePlay.Combat
{
    /// <summary>
    /// 出手打断 vs 受击抗打断。热路径只比两个 int，不扫 Buff 列表。
    /// 霸体 / 档位把抗打断抬高；硬控（MoveForbid / Freeze）不走这里。
    /// </summary>
    public static class CombatInterrupt
    {
        /// <summary>0 表示这刀不断招（轻受击默认）。</summary>
        public const int None = 0;

        /// <summary>段表 InterruptLevel=0 且 HitReaction=Heavy 时的回退。</summary>
        public const int DefaultHeavy = 3;

        /// <summary>玩家 / 杂兵站立时的抗打断初值。</summary>
        public const int DefaultBaseAnti = 1;

        /// <summary>精英站立抗打断（无 Profile 时）。</summary>
        public const int DefaultEliteAnti = 3;

        /// <summary><see cref="CombatTags.BuffUnStopped"/> 存在时叠加的抗打断。</summary>
        public const int DefaultSuperArmorBonus = 3;

        /// <summary>段表 0 时按轻重回退，避免旧行漏填导致全员不断招或全员可断。</summary>
        public static int ResolveFromSegment(int interruptLevel, HitReactionType hitReaction)
        {
            if (interruptLevel > 0)
                return interruptLevel;
            return hitReaction == HitReactionType.Heavy ? DefaultHeavy : None;
        }

        /// <summary>
        /// 技能直伤且打断等级 ≥ 抗打断才断招。Buff/DoT 不断招。
        /// </summary>
        public static bool ShouldBreakSkill(DamageAction damage, int antiInterruptLevel)
        {
            if (damage == null || damage.DamageSource != DamageSource.Skill)
                return false;
            int interrupt = damage.InterruptLevel;
            if (interrupt <= None)
                return false;
            return interrupt >= antiInterruptLevel;
        }
    }
}
