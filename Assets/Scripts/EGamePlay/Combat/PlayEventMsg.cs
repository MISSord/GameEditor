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
}
