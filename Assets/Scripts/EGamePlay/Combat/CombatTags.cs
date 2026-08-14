using System;

namespace EGamePlay.Combat
{
    /// <summary>
    /// 与战斗逻辑强相关的 Tag 名常量，集中管理以避免硬编码字符串。
    /// </summary>
    public static class CombatTags
    {
        public const string BuffAttackDamageForbid = "Buff.AttackDamageForbid";
        public const string BuffMoveForbid = "Buff.MoveForbid";
        public const string BuffSkillForbid = "Buff.SkillForbid";
        public const string BuffUnStopped = "Buff.UnStopped";
        public const string BuffRoll = "Buff.Roll";
    }
}

