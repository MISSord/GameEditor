namespace EGamePlay.Combat
{
    /// <summary>时间轴消息中的战斗规则落地（Tag/状态等，不含 Unity 表现）。</summary>
    public static class CombatTimelineRules
    {
        /// <summary>尝试处理战斗侧消息；返回 true 表示已消费。</summary>
        public static bool TryApply(ICombatUnit unit, string msgName, float floatMsg, TagSource? source)
        {
            if (unit == null || unit.IsDisposed || string.IsNullOrEmpty(msgName))
                return false;

            if (msgName != PlayEventMsg.SetNoBreakTime)
                return false;

            unit.TagHost?.GrantUnstoppedFor(floatMsg, source ?? TagSource.Manual());
            return true;
        }
    }
}
