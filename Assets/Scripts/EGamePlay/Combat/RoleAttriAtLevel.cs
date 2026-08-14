using UnityEngine;

namespace EGamePlay.Combat
{
    /// <summary>
    /// 按等级计算后的角色属性（基础值 + 等级 * 增长值），无 buff 等外部影响。
    /// 用于从 <see cref="RoleAttriSetting"/> 和等级得到当前数值。
    /// </summary>
    public readonly struct RoleAttriAtLevel
    {
        private readonly RoleAttriSetting _setting;
        private readonly int _level;

        public RoleAttriSetting Setting => _setting;
        public int Level => _level;

        public RoleAttriAtLevel(RoleAttriSetting setting, int level)
        {
            _setting = setting;
            _level = level;
        }

        /// <summary>当前血量上限（公式：基础值 + 等级 * 增长值）。用作资源上限时建议取整。</summary>
        public float HealthPointMax => _setting.BaseHealthPointMax + _level * _setting.HealthPointMaxAdd;

        /// <summary>当前攻击力。</summary>
        public float Attack => _setting.BaseAttack + _level * _setting.AttackAdd;

        /// <summary>当前防御力。</summary>
        public float Defense => _setting.BaseDefense + _level * _setting.DefenseAdd;

        /// <summary>当前能量上限。用作资源上限时建议取整。</summary>
        public float ManaMax => _setting.BaseManaMax + _level * _setting.ManaMaxAdd;

        /// <summary>当前暴击率（万分比）。</summary>
        public float CriticalProbability => _setting.BaseCriticalProbability + _level * _setting.CriticalProbabilityAdd;

        /// <summary>当前暴击倍率（万分比）。</summary>
        public float CriticalValue => _setting.BaseCriticalValue + _level * _setting.CriticalValueAdd;

        /// <summary>血量上限取整，便于作为资源上限使用。</summary>
        public int HealthPointMaxInt => Mathf.RoundToInt(HealthPointMax);

        /// <summary>能量上限取整，便于作为资源上限使用。</summary>
        public int ManaMaxInt => Mathf.RoundToInt(ManaMax);
    }
}
