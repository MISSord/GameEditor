namespace EGamePlay.Combat
{
    /// <summary>技能序列运行时消息名常量。</summary>
    public static class PlayEventMsg
    {
        public static string SetCanMove = "SetCanMove";
        public static string SetCanRotate = "SetCanRotate";
        public static string SetUnMoveTime = "SetUnMoveTime";
        public static string ActivePlayerRender = "ActivePlayerRender";
        public static string TimeStop = "TimeStop";
        /// <summary>时空断裂：世界减速，玩家时间正常。</summary>
        public static string TimeFracture = "TimeFracture";
        /// <summary>播放表现包；FloatMsg 填 CombatFxPackageId 整型值。</summary>
        public static string PlayFxPackage = "PlayFxPackage";
        public static string SetNoGravityT = "SetNoGravityT";
        public static string SetNoBreakTime = "SetNoBreakTime";
        public static string PlayAudio = "PlayAudio";
    }

    /// <summary>技能时间轴消息的表现层落地（动画/移动/渲染等）。</summary>
    public interface ICombatTimelinePresenter
    {
        /// <summary>处理非战斗规则类时间轴消息。</summary>
        void ApplyPresentationMessage(
            string msgName,
            float floatMsg,
            bool boolMsg,
            string strMsg = null,
            TagSource? timelineSource = null);
    }

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
