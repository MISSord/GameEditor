namespace EGamePlay.Combat
{
    /// <summary>战斗行动能力（挂在 ICombatUnit 上）。</summary>
    public interface IActionAbility
    {
        ICombatUnit OwnerEntity { get; }
        bool Enable { get; set; }
    }
}
