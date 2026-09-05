using System;
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
        /// <summary>效果从列表移除时调用一次（SkillTimeStop 释放玩家钟 hold）。</summary>
        public Action OnRemoved;

        bool _removedNotified;

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

        /// <summary>从管理器移除时调用一次，幂等。</summary>
        public void NotifyRemoved()
        {
            if (_removedNotified)
                return;
            _removedNotified = true;
            OnRemoved?.Invoke();
            OnRemoved = null;
        }
    }

    /// <summary>
    /// 时间流速效果管理器。统一管理 Pause、时空断裂、HitStop、技能时停 等效果。
    /// 效果计时使用 unscaledDeltaTime，暂停（IsGameplayPaused）时 delta=0，计时冻结，符合崩坏3表现。
    /// 同类型只保留一份（低 Priority 无法覆盖高 Priority）；不同类型按各层取 min。
    /// HitStop 的全局 scale 应为 1，顿帧打在实体 TimeScale 上，避免连打把整场叠死。
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
                {
                    e.NotifyRemoved();
                    _effects.RemoveAt(i);
                }
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

        /// <summary>
        /// 添加效果。同类型已有且新 Priority 更低则拒绝并返回 null；否则替换旧的。
        /// </summary>
        public static TimeScaleEffect AddEffect(TimeScaleEffectType type, float worldScale, float playerScale,
            float cameraScale, float duration, int priority = 0)
        {
            TimeScaleEffect existing = FindByType(type);
            if (existing != null)
            {
                if (priority < existing.Priority)
                    return null;
                RemoveEffect(existing);
            }

            var e = new TimeScaleEffect(type, worldScale, playerScale, cameraScale, duration, priority);
            _effects.Add(e);
            return e;
        }

        /// <summary>取指定类型当前效果；没有则 null。</summary>
        public static TimeScaleEffect FindByType(TimeScaleEffectType type)
        {
            for (int i = 0; i < _effects.Count; i++)
            {
                if (_effects[i].Type == type)
                    return _effects[i];
            }
            return null;
        }

        /// <summary> 移除指定效果实例。 </summary>
        public static bool RemoveEffect(TimeScaleEffect effect)
        {
            if (!_effects.Remove(effect))
                return false;
            effect.NotifyRemoved();
            return true;
        }

        /// <summary> 是否存在指定类型的效果。 </summary>
        public static bool HasEffect(TimeScaleEffectType type) => FindByType(type) != null;

        /// <summary> 移除指定类型的所有效果。 </summary>
        public static void RemoveByType(TimeScaleEffectType type)
        {
            for (int i = _effects.Count - 1; i >= 0; i--)
            {
                if (_effects[i].Type == type)
                {
                    _effects[i].NotifyRemoved();
                    _effects.RemoveAt(i);
                }
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

        /// <summary> 触发 HitStop 计时槽（全局 scale 保持 1；实体顿帧由表现层写入 TimeScale）。 </summary>
        /// <param name="duration">持续时间（ unscaled 秒）。</param>
        /// <param name="priority">同类型刷新用；更高可覆盖较短的轻顿。</param>
        public static TimeScaleEffect AddHitStop(float duration, int priority = 10)
        {
            return AddEffect(TimeScaleEffectType.HitStop, 1f, 1f, 1f, duration, priority);
        }

        /// <summary> 清空所有效果。 </summary>
        public static void ClearAll()
        {
            for (int i = 0; i < _effects.Count; i++)
                _effects[i].NotifyRemoved();
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
