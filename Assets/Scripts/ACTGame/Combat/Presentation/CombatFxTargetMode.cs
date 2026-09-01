namespace ACTGameEditor.Combat
{
    /// <summary>Package 条目播放时如何解析 <see cref="CombatFxSpec.Target"/>。</summary>
    public enum CombatFxTargetMode : byte
    {
        /// <summary>不需要 Target（全局时间/镜头）。</summary>
        None = 0,
        /// <summary>路由宿主实体（CombatEntity）。</summary>
        Owner = 1,
        /// <summary>战斗行动的目标（如 DamageAction.Target）。</summary>
        ActionTarget = 2,
        /// <summary>战斗行动的施加者（如 DamageAction.Creator）。</summary>
        ActionCreator = 3,
        /// <summary>显式指定 <see cref="CombatFxPlayContext.ExplicitTarget"/>。</summary>
        Explicit = 4,
    }
}
