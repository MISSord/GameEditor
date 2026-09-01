using System;
using System.Collections.Generic;
using UnityEngine;

namespace ACTGameEditor
{
    /// <summary>
    /// 被动技能 → BuffId 映射集合。
    /// 约定：在 Resources 根目录下放置名为 PassiveSkillBuffMaps 的该类型资源。
    /// </summary>
    [CreateAssetMenu(fileName = "PassiveSkillBuffMaps", menuName = "ACTGame/PassiveSkillBuffMaps")]
    public class PassiveSkillBuffMapCollection : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            [Tooltip("被动技能 ID（SkillDemoSetting.SkillId）")]
            public int PassiveSkillId;
            [Tooltip("对应挂载的 BuffId（BuffDemoSetting.BuffId）")]
            public int BuffId;
        }

        [Tooltip("被动技能 → BuffId 映射列表")]
        public List<Entry> Entries = new List<Entry>();

        private static PassiveSkillBuffMapCollection _instance;
        private static Dictionary<int, int> _map;

        /// <summary>普攻/大招后加攻被动（SkillDemoSetting）。</summary>
        public const int AttackUpOnAttackSkillId = 19001;
        /// <summary>常驻触发 Buff（监听 PostSpell）。</summary>
        public const int AttackUpOnAttackTriggerBuffId = 31000;
        /// <summary>主动技能命中点燃被动。</summary>
        public const int IgniteOnHitSkillId = 19002;
        /// <summary>常驻触发 Buff（监听 PostCauseDamage）。</summary>
        public const int IgniteOnHitTriggerBuffId = 31010;

        /// <summary>
        /// 获取被动技能对应的 BuffId。若未配置则返回 0。
        /// </summary>
        public static int GetBuffId(int passiveSkillId)
        {
            if (passiveSkillId <= 0) return 0;

            EnsureMap();
            return _map.TryGetValue(passiveSkillId, out var buffId) ? buffId : 0;
        }

        static void EnsureMap()
        {
            if (_map != null) return;

            if (_instance == null)
                _instance = Resources.Load<PassiveSkillBuffMapCollection>("PassiveSkillBuffMaps");

            int capacity = _instance != null && _instance.Entries != null ? _instance.Entries.Count + 4 : 4;
            _map = new Dictionary<int, int>(capacity);

            if (_instance != null && _instance.Entries != null)
            {
                for (int i = 0; i < _instance.Entries.Count; i++)
                {
                    var e = _instance.Entries[i];
                    if (e == null || e.PassiveSkillId <= 0 || e.BuffId <= 0) continue;
                    _map[e.PassiveSkillId] = e.BuffId;
                }
            }

            if (!_map.ContainsKey(AttackUpOnAttackSkillId))
                _map[AttackUpOnAttackSkillId] = AttackUpOnAttackTriggerBuffId;
            if (!_map.ContainsKey(IgniteOnHitSkillId))
                _map[IgniteOnHitSkillId] = IgniteOnHitTriggerBuffId;
        }
    }
}

