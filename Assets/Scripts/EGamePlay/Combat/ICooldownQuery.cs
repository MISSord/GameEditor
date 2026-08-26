namespace EGamePlay.Combat
{
    /// <summary>技能 CD 查询/启动，由 ACT 层 SkillCDTimer 实现。</summary>
    public interface ICooldownQuery
    {
        bool IsCDEnd(int skillId);
        void StartCooldown(int skillId);
    }
}
