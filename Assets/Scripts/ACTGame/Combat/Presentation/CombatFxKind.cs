namespace ACTGameEditor.Combat
{
    /// <summary>
    /// 战斗表现原子类型（Bridge 粒度，非玩法粒度）。
    /// 玩法组合见 <see cref="CombatFxPackageId"/>。
    /// </summary>
    public enum CombatFxKind : byte
    {
        // ── 时间域（TimeScaleFxBridge）──
        /// <summary>技能轴时停：玩家/镜头 scale 可控。</summary>
        SkillTimeStop = 0,
        /// <summary>时空断裂：世界减速、玩家正常（鸣潮极限闪避 / ZZZ Vital View 类）。</summary>
        TimeFracture = 1,
        /// <summary>命中顿帧：只压攻受双方实体钟，不改全局 WorldScale。</summary>
        HitStop = 2,

        // ── 镜头域（CameraPostFxBridge）──
        /// <summary>径向模糊冲击 pulse。</summary>
        RadialBlurImpact = 3,

        // ── 角色材质域（CharacterRenderFxBridge）──
        /// <summary>受击 MPB 闪白。</summary>
        HitFlash = 4,
        /// <summary>死亡噪声溶解。</summary>
        DeathDissolve = 5,

        // ── 角色 / 屏幕表现 ──
        /// <summary>闪避/冲刺残影（鸣潮、ZZZ Perfect Dodge）。</summary>
        Afterimage = 6,
        /// <summary>屏幕去饱和/灰屏（ZZZ Perfect Dodge 观感）。</summary>
        ScreenDesaturate = 7,
        /// <summary>命中粒子（刀光、元素溅射）。</summary>
        HitParticle = 8,
        /// <summary>战斗命中音效。</summary>
        HitAudio = 9,
    }
}
