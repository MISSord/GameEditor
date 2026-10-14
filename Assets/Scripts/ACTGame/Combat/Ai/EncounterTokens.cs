namespace ACTGameEditor.Combat.Ai
{
    /// <summary>进攻权种类。B 切片只发放 Melee。</summary>
    public enum EncounterTokenKind : byte
    {
        Melee = 0,
        Ranged = 1,
        Special = 2,
    }

    /// <summary>还牌原因，便于调日志；不影响结算。</summary>
    public enum TokenReleaseReason : byte
    {
        None = 0,
        SkillFinished = 1,
        LeaseExpired = 2,
        Stolen = 3,
        Interrupted = 4,
        Death = 5,
        FailedLaunch = 6,
        Unregistered = 7,
        /// <summary>Relax 收回未承诺的牌。</summary>
        TempoRecall = 8,
    }

    /// <summary>战局节奏。E 落地 Build / Relax；Peak 以后再开。Punish（原失衡）已删除。</summary>
    public enum EncounterTempo : byte
    {
        Build = 0,
        Peak = 1,
        Relax = 2,
    }
}
