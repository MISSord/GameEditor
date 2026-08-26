using System.Collections.Generic;
using UnityEngine;

namespace EGamePlay
{
    /// <summary>
    /// 时间流速效果类型，用于区分不同来源的 scale 控制。
    /// </summary>
    public enum TimeScaleEffectType
    {
        Pause,
        TimeFracture,
        HitStop,
        SkillTimescale,
        DebugSlow,
    }

    /// <summary>
    /// 单个时间流速效果，有时长与优先级。
    /// </summary>
    public class TimeScaleEffect
    {
        public TimeScaleEffectType Type;
        public float WorldScale;
        public float PlayerScale;
        public float CameraScale;
        public float Duration;
        public float Elapsed;
        public int Priority;

        public bool IsExpired => Duration > 0 && Elapsed >= Duration;

        public TimeScaleEffect(TimeScaleEffectType type, float worldScale, float playerScale, float cameraScale,
            float duration, int priority = 0)
        {
            Type = type;
            WorldScale = Mathf.Max(0f, worldScale);
            PlayerScale = Mathf.Max(0f, playerScale);
            CameraScale = Mathf.Max(0f, cameraScale);
            Duration = duration;
            Priority = priority;
        }
    }

    /// <summary>
    /// 时间流速效果管理器。统一管理 Pause、时空断裂、HitStop、技能时停 等效果。
    /// 效果计时使用 unscaledDeltaTime，暂停（IsGameplayPaused）时 delta=0，计时冻结，符合崩坏3表现。
    /// 合成规则：每层取当前生效效果的最小 scale（Pause 的 0 会覆盖一切）。
    /// </summary>
    public static class TimeScaleEffectManager
    {
        private static readonly List<TimeScaleEffect> _effects = new List<TimeScaleEffect>(8);

        /// <summary> gameplay 是否暂停（如打开菜单）。为 true 时效果计时不推进。 </summary>
        public static bool IsGameplayPaused { get; set; }

        /// <summary> 当前活跃效果数量。 </summary>
        public static int ActiveEffectCount => _effects.Count;

        /// <summary>
        /// 每帧 Update 最前调用，必须在 GameTimeManager.Tick 之前。
        /// </summary>
        public static void Tick()
        {
            float delta = IsGameplayPaused ? 0f : Time.unscaledDeltaTime;

            for (int i = _effects.Count - 1; i >= 0; i--)
            {
                var e = _effects[i];
                e.Elapsed += delta;
                if (e.IsExpired)
                    _effects.RemoveAt(i);
            }

            float world = 1f, player = 1f, camera = 1f;
            foreach (var e in _effects)
            {
                world = Mathf.Min(world, e.WorldScale);
                player = Mathf.Min(player, e.PlayerScale);
                camera = Mathf.Min(camera, e.CameraScale);
            }

            GameTimeManager.WorldScale = world;
            GameTimeManager.PlayerScale = player;
            GameTimeManager.CameraScale = camera;
        }

        /// <summary> 添加效果，返回实例便于移除。 </summary>
        public static TimeScaleEffect AddEffect(TimeScaleEffectType type, float worldScale, float playerScale,
            float cameraScale, float duration, int priority = 0)
        {
            var e = new TimeScaleEffect(type, worldScale, playerScale, cameraScale, duration, priority);
            _effects.Add(e);
            return e;
        }

        /// <summary> 移除指定效果实例。 </summary>
        public static bool RemoveEffect(TimeScaleEffect effect)
        {
            return _effects.Remove(effect);
        }

        /// <summary> 是否存在指定类型的效果。 </summary>
        public static bool HasEffect(TimeScaleEffectType type)
        {
            for (int i = 0; i < _effects.Count; i++)
            {
                if (_effects[i].Type == type) return true;
            }
            return false;
        }

        /// <summary> 移除指定类型的所有效果。 </summary>
        public static void RemoveByType(TimeScaleEffectType type)
        {
            for (int i = _effects.Count - 1; i >= 0; i--)
            {
                if (_effects[i].Type == type)
                    _effects.RemoveAt(i);
            }
        }

        /// <summary> 暂停 gameplay（如打开菜单）。效果计时冻结，世界/玩家/相机 scale=0。 </summary>
        public static TimeScaleEffect AddPause()
        {
            IsGameplayPaused = true;
            return AddEffect(TimeScaleEffectType.Pause, 0f, 0f, 0f, float.MaxValue, int.MaxValue);
        }

        /// <summary> 移除暂停。 </summary>
        public static void RemovePause()
        {
            IsGameplayPaused = false;
            RemoveByType(TimeScaleEffectType.Pause);
        }

        /// <summary> 触发时空断裂。世界减速，玩家正常。 </summary>
        /// <param name="duration">持续时间（ unscaled 秒），暂停时不计入。</param>
        /// <param name="worldScale">世界 scale，如 0.3。</param>
        public static TimeScaleEffect AddTimeFracture(float duration, float worldScale = 0.3f)
        {
            return AddEffect(TimeScaleEffectType.TimeFracture, worldScale, 1f, 1f, duration, 50);
        }

        /// <summary> 触发 HitStop（命中顿帧）。 </summary>
        /// <param name="duration">持续时间（ unscaled 秒）。</param>
        /// <param name="worldScale">如 0.1。</param>
        public static TimeScaleEffect AddHitStop(float duration, float worldScale = 0.1f)
        {
            return AddEffect(TimeScaleEffectType.HitStop, worldScale, 1f, 1f, duration, 10);
        }

        /// <summary> 清空所有效果。 </summary>
        public static void ClearAll()
        {
            _effects.Clear();
            IsGameplayPaused = false;
            GameTimeManager.ResetAllScales();
        }

        /// <summary> 调试用：添加长时间减速效果。 </summary>
        public static TimeScaleEffect AddDebugSlow(float worldScale = 0.25f)
        {
            return AddEffect(TimeScaleEffectType.DebugSlow, worldScale, 1f, 1f, float.MaxValue, 5);
        }

        /// <summary> 移除调试减速效果。 </summary>
        public static void RemoveDebugSlow()
        {
            RemoveByType(TimeScaleEffectType.DebugSlow);
        }
    }
}
