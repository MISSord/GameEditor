using System.Collections.Generic;
using EGamePlay;
using UnityEngine;

namespace EGamePlay.Combat
{
    /// <summary>
    /// 单个时间流速 modifier，支持叠加与移除。
    /// </summary>
    public struct TimeScaleModifier
    {
        /// <summary>来源 ID，用于批量移除（如 BuffId、技能 ID）。0 表示无来源。</summary>
        public int SourceId;
        /// <summary>乘数，如 0.5=半速，1.5=1.5 倍速。最终 scale = 各 modifier 的 Scale 相乘。</summary>
        public float Scale;

        public TimeScaleModifier(int sourceId, float scale)
        {
            SourceId = sourceId;
            Scale = Mathf.Max(0.0001f, scale);
        }
    }

    /// <summary>
    /// 战斗实体专属的时间流速组件。支持运行时叠加多种时间效果（如 Buff 减速、技能加速）。
    /// 最终 scale = WorldScale × 所有 modifier 的 Scale 相乘。
    /// </summary>
    public class EntityTimeScaleComponent : Component
    {
        private readonly List<TimeScaleModifier> _modifiers = new List<TimeScaleModifier>(4);
        private float _cachedScale = 1f;

        /// <summary>当前实体时间流速乘数（不含世界 scale）。无 modifier 时为 1。仅在增减 modifier 时重算。</summary>
        public float EntityScale => _cachedScale;

        private void Recalculate()
        {
            if (_modifiers.Count == 0)
            {
                _cachedScale = 1f;
                return;
            }
            float product = 1f;
            for (int i = 0; i < _modifiers.Count; i++)
                product *= _modifiers[i].Scale;
            _cachedScale = product;
        }

        /// <summary>添加时间流速 modifier，可叠加多个。</summary>
        /// <param name="sourceId">来源 ID，用于 RemoveBySource。0 表示无来源。</param>
        /// <param name="scale">乘数，如 0.5=半速，1.5=1.5 倍速。</param>
        public void AddModifier(int sourceId, float scale)
        {
            _modifiers.Add(new TimeScaleModifier(sourceId, scale));
            Recalculate();
        }

        /// <summary>移除指定来源的所有 modifier。</summary>
        public void RemoveBySource(int sourceId)
        {
            if (sourceId == 0) return;
            for (int i = _modifiers.Count - 1; i >= 0; i--)
            {
                if (_modifiers[i].SourceId == sourceId)
                    _modifiers.RemoveAt(i);
            }
            Recalculate();
        }

        /// <summary>移除指定 modifier 实例（按 sourceId + scale 匹配，仅移除第一个）。</summary>
        public bool RemoveModifier(int sourceId, float scale)
        {
            for (int i = 0; i < _modifiers.Count; i++)
            {
                var m = _modifiers[i];
                if (m.SourceId == sourceId && Mathf.Approximately(m.Scale, scale))
                {
                    _modifiers.RemoveAt(i);
                    Recalculate();
                    return true;
                }
            }
            return false;
        }

        /// <summary>清空所有 modifier。</summary>
        public void Clear()
        {
            _modifiers.Clear();
            _cachedScale = 1f;
        }

        /// <summary>当前 modifier 数量。</summary>
        public int ModifierCount => _modifiers.Count;
    }
}
