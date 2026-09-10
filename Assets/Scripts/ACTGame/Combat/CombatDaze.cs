namespace ACTGameEditor.Combat
{
    /// <summary>失衡计量相位。满条前只是数字，Opened 起才改行为态。</summary>
    public enum DazePhase : byte
    {
        Idle = 0,
        Charging = 1,
        Opened = 2,
        Draining = 3,
        Recover = 4,
    }

    /// <summary>失衡贡献来源。招架 / 部位破坏以后走 DirectPct。</summary>
    public enum DazeSource : byte
    {
        Skill = 0,
        Parry = 1,
        Debug = 2,
        DirectPct = 3,
    }

    /// <summary>DazeSetting.Id 档位。杂兵 1 / 精英 2 / 首领 3。</summary>
    public static class DazeTierId
    {
        public const int Grunt = 1;
        public const int Elite = 2;
        public const int Boss = 3;
    }
}
