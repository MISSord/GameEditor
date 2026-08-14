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

        /// <summary>
        /// 获取被动技能对应的 BuffId。若未配置则返回 0。
        /// </summary>
        public static int GetBuffId(int passiveSkillId)
        {
            if (passiveSkillId <= 0) return 0;

            if (_instance == null)
            {
                _instance = Resources.Load<PassiveSkillBuffMapCollection>("PassiveSkillBuffMaps");
                if (_instance == null) return 0;
            }

            if (_map == null)
            {
                _map = new Dictionary<int, int>(_instance.Entries != null ? _instance.Entries.Count : 16);
                if (_instance.Entries != null)
                {
                    for (int i = 0; i < _instance.Entries.Count; i++)
                    {
                        var e = _instance.Entries[i];
                        if (e == null || e.PassiveSkillId <= 0 || e.BuffId <= 0) continue;
                        _map[e.PassiveSkillId] = e.BuffId;
                    }
                }
            }

            return _map.TryGetValue(passiveSkillId, out var buffId) ? buffId : 0;
        }
    }
}

