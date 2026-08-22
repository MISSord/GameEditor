namespace EGamePlay.Combat
{
    /// <summary>当前轴与新技能的替换裁决。currentSort 更高则不换，对齐 SpellComponent 原注释。</summary>
    public static class SkillCancelService
    {
        /// <summary>当前无轴，或 incomingSort 不低于当前轴时允许替换。</summary>
        public static bool ShouldReplace(int currentSort, int incomingSort)
        {
            return currentSort <= incomingSort;
        }

        /// <summary>更高优先级槽位立刻打断（闪避顶普攻）。同级连招仍走时间轴窗口。</summary>
        public static bool IsHardInterrupt(int currentSort, int incomingSort)
        {
            return incomingSort > currentSort;
        }
    }
}
