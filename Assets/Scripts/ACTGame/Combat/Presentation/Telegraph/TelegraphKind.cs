namespace ACTGameEditor.Combat
{
    /// <summary>
    /// 招式可读性预警类型（该闪 / 可招架 / 该跳 / 不可招架）。
    /// 招表（<see cref="Ai.EnemyMoveEntry"/>）与 XC 轴 AiTelegraph 消息共用；表现落地见 <see cref="CombatTelegraph"/>。
    /// 口径学绝区零黄红闪，不是鸣潮金圈弹刀。
    /// </summary>
    public enum TelegraphKind : byte
    {
        None = 0,
        /// <summary>该闪避：暖白闪光。</summary>
        Dodge = 1,
        /// <summary>可招架：黄光（绝区零黄闪 / 支援招架）。不是鸣潮金圈弹刀。</summary>
        Parry = 2,
        /// <summary>该跳 / 对地招：青光。</summary>
        Jump = 3,
        /// <summary>不可招架：红光（绝区零红闪，只能闪）。</summary>
        Unblockable = 4,
    }
}
