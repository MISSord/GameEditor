namespace EGamePlay.Combat
{
    /// <summary>技能身份表辅助，不随 Luban 生成覆盖。</summary>
    public sealed partial class SkillDemoSetting
    {
        /// <summary>升级组 Id；表里为 0 时等于 <see cref="SkillId"/>。</summary>
        public int ResolvedGroupId => SkillGroupId > 0 ? SkillGroupId : SkillId;

        /// <summary>技能等级上限；表里未填或非法时按 10。</summary>
        public int ResolvedMaxLevel => MaxLevel > 0 ? MaxLevel : 10;
    }
}
