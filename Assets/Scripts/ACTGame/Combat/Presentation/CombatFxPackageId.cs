namespace ACTGameEditor.Combat
{
    /// <summary>
    /// 表现包 ID：Luban / 技能表 / 时间轴 Msg 统一引用。
    /// 一个 Package = 一组 <see cref="CombatFxKind"/> 有序组合。
    /// </summary>
    public enum CombatFxPackageId : int
    {
        None = 0,

        // ── 系统：ActionPoint 默认规则 ──
        /// <summary>轻受击：闪白（ZZZ/鸣潮通用）。</summary>
        HitTakenLight = 100,
        /// <summary>重受击：闪白加长 + 可选镜头（预留）。</summary>
        HitTakenHeavy = 101,
        /// <summary>轻命中：短 HitStop + 镜头冲击（鸣潮偏轻）。</summary>
        HitCausedLight = 110,
        /// <summary>重命中：长 HitStop + 强镜头（ZZZ 风格）。</summary>
        HitCausedHeavy = 111,
        /// <summary>暴击命中（在 HitCaused 基础上加长）。</summary>
        HitCausedCrit = 112,

        // ── 闪避 / 时间（多数走技能轴 Msg，也可 Package 引用）──
        /// <summary>纯闪避残影，无时空断裂。</summary>
        DodgePlain = 200,
        /// <summary>极限闪避：时空断裂（鸣潮）。</summary>
        DodgeTimeFracture = 201,
        /// <summary>Perfect Dodge 套：断裂 + 残影 + 灰屏（ZZZ 向，部分待 Bridge）。</summary>
        DodgePerfect = 202,

        // ── 状态 / 异常（Buff、破韧演出）──
        /// <summary>破韧/瘫痪入场（敌人 Stun）。</summary>
        StaggerBreak = 300,
        /// <summary>元素异常爆发（Disorder / 协奏反应）。</summary>
        AnomalyBurst = 301,

        // ── 换人 / 连携 ──
        /// <summary>切人入场（变奏 / Quick Assist）。</summary>
        SwitchIn = 400,
        /// <summary>切人退场（延奏）。</summary>
        SwitchOut = 401,
        /// <summary>连携/Chain Attack 演出。</summary>
        ChainAttack = 402,

        // ── 大招 / 演出 ──
        /// <summary>终结技短演出：时停 + 背景虚化（时间轴主导）。</summary>
        UltimateCinematic = 500,

        // ── 死亡 ──
        /// <summary>死亡溶解退场。</summary>
        DeathDissolve = 600,
    }
}
