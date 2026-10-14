using EGamePlay;

namespace ACTGameEditor.Combat
{
    /// <summary>敌人偏谐计量相位。满条 Ready 可 F，结束后 Vacuum 锁零。</summary>
    public enum DazePhase : byte
    {
        Idle = 0,
        Charging = 1,
        Ready = 2,
        Executing = 3,
        Vacuum = 4,
    }

    /// <summary>偏谐贡献来源。招架 / 部位破坏以后走 DirectPct。</summary>
    public enum DazeSource : byte
    {
        Skill = 0,
        Parry = 1,
        Debug = 2,
        DirectPct = 3,
    }

    /// <summary>DazeSetting.Id 档位。杂兵 1 / 精英 2 / 首领 3。表名仍叫 Daze，数字当偏谐用。</summary>
    public static class DazeTierId
    {
        public const int Grunt = 1;
        public const int Elite = 2;
        public const int Boss = 3;
    }

    /// <summary>
    /// 怪物表号段常量。运行时身份以 <c>MonsterSetting.MonsterId</c> 为准，禁止用 0 回退表第一行。
    /// </summary>
    public static class MonsterIds
    {
        /// <summary>测试杂兵。无韧条，仍可被打断。</summary>
        public const int Grunt = 101;
        /// <summary>测试精英。有韧条（第 2 步才扣）。</summary>
        public const int Elite = 201;
        /// <summary>测试首领。</summary>
        public const int Boss = 301;

        /// <summary>
        /// 刷怪未填 Id 时的号段默认：玩家 0；敌人显式 Id 优先，否则 EnemyA→201、其余→101。
        /// 这是生成默认值，不是 <c>GetMonsterOrNull</c> 缺行回退。
        /// </summary>
        public static int ResolveSpawnId(AgentTag tag, AgentModelType model, int monsterId)
        {
            if (tag != AgentTag.enemy)
                return 0;
            if (monsterId > 0)
                return monsterId;
            return model == AgentModelType.EnemyA ? Elite : Grunt;
        }
    }
}
