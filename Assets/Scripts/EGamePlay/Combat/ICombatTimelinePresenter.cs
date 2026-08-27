namespace EGamePlay.Combat
{
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
}
