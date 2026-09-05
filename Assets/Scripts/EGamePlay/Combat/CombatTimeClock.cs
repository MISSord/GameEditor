using UnityEngine;

namespace EGamePlay.Combat
{
    /// <summary>
    /// 战斗逻辑选层：本地玩家（及 SkillTimeStop 发起者）走玩家钟，其余走世界钟，再乘实体 TimeScale。
    /// 不分配；供 CombatContext / 动画 / 电机每帧调用。
    /// </summary>
    public static class CombatTimeClock
    {
        /// <summary>是否走玩家层（断裂时仍能出招）。</summary>
        public static bool UsesPlayerLayer(ICombatUnit unit) =>
            unit != null && !unit.IsDisposed && unit.UsesPlayerCombatClock;

        /// <summary>本帧战斗逻辑 delta（已含实体 TimeScale）。</summary>
        public static float GetDelta(ICombatUnit unit)
        {
            float layer = UsesPlayerLayer(unit) ? GameTimeManager.PlayerDelta : GameTimeManager.WorldDelta;
            return layer * GetEntityScale(unit);
        }

        /// <summary>本帧 Fixed 战斗 delta（已含实体 TimeScale）。</summary>
        public static float GetFixedDelta(ICombatUnit unit)
        {
            float scale = UsesPlayerLayer(unit) ? GameTimeManager.PlayerScale : GameTimeManager.WorldScale;
            return Time.fixedDeltaTime * scale * GetEntityScale(unit);
        }

        /// <summary>动画 / 电机用的层 scale（不含实体）。</summary>
        public static float GetLayerScale(ICombatUnit unit) =>
            UsesPlayerLayer(unit) ? GameTimeManager.PlayerScale : GameTimeManager.WorldScale;

        /// <summary>层累计时间（受击槽、反应自动交回）。</summary>
        public static float GetLayerTime(ICombatUnit unit) =>
            UsesPlayerLayer(unit) ? GameTimeManager.PlayerTime : GameTimeManager.WorldTime;

        /// <summary>命中顿帧写在实体 TimeScale 上的来源 Id，刷新时整段替换。</summary>
        public const int HitStopSourceId = -1001;

        /// <summary>冻结 Buff 写在实体 TimeScale 上的来源 Id（scale=0）。</summary>
        public const int FreezeSourceId = -1002;

        /// <summary>层 delta（不含实体倍率）。闪白等表现用，冻结时仍能播完。</summary>
        public static float GetLayerDelta(ICombatUnit unit)
        {
            if (unit == null || unit.IsDisposed)
                return GameTimeManager.WorldDelta;
            return UsesPlayerLayer(unit) ? GameTimeManager.PlayerDelta : GameTimeManager.WorldDelta;
        }

        /// <summary>技能粒子 simulationSpeed：层 scale × 实体 scale。</summary>
        public static float GetSimulationSpeed(ICombatUnit unit)
        {
            if (unit == null || unit.IsDisposed)
                return 1f;
            return GetLayerScale(unit) * GetEntityScale(unit);
        }

        /// <summary>写入粒子播放速度；speed=0 时刀光与宿主一起停。</summary>
        public static void ApplySimulationSpeed(ParticleSystem ps, float speed)
        {
            if (ps == null)
                return;
            ParticleSystem.MainModule main = ps.main;
            if (Mathf.Abs(main.simulationSpeed - speed) <= 0.0001f)
                return;
            main.simulationSpeed = speed;
        }

        static float GetEntityScale(ICombatUnit unit)
        {
            if (unit == null || unit.IsDisposed)
                return 1f;
            return Mathf.Max(0f, unit.GetTimeScale());
        }
    }
}
