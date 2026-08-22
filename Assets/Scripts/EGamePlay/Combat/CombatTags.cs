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
        /// <summary>处于非默认战斗形态（明心境、变身等）。</summary>
        public const string StanceForm = "Stance.Form";
        /// <summary>空中；Resolver 主要用 IsAirborne，此标签供技能边条件。</summary>
        public const string LocomotionAirborne = "Locomotion.Airborne";
    }
}

