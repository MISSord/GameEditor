using System;
using System.Collections.Generic;
using UnityEngine;

namespace EGamePlay.Combat
{
    public class TagCollection : Singleton<TagCollection>
    {
        public static Dictionary<string, List<int>> TagKeyValueDic;
        public static Dictionary<string, int> TagToIndexDic;
        private bool _isInitialized = false;
        private int _keyIndex = 0;

        /// <summary>
        /// 初始化：将字符串切割并转换为整数索引
        /// </summary>
        public void Initialize()
        {
            if (_isInitialized) return;
            TagKeyValueDic = new Dictionary<string, List<int>>();
            TagToIndexDic = new Dictionary<string, int>();

            foreach (var entry in AllTags)
            {
                _keyIndex++;

                if (!TagKeyValueDic.ContainsKey(entry))
                    TagKeyValueDic[entry] = new List<int>();

                TagToIndexDic[entry] = _keyIndex;
                TagKeyValueDic[entry].Add(_keyIndex);
                ParseHierarchy(entry, TagKeyValueDic[entry]);
            }

            _isInitialized = true;
            GameLog.CombatDebug($"Tag Library Initialized. Primary tags={TagKeyValueDic.Count}, TotalIds={_keyIndex}");
        }

        // 辅助：切割字符串并转换 ID
        private void ParseHierarchy(string fullPath, List<int> targetSet)
        {
            // 输入: "State.Control.Stun"
            // 应该加入: Hash("State"), Hash("State.Control")

            string[] parts = fullPath.Split('.');
            if (parts.Length <= 1) return;

            string current = "";
            for (int i = 0; i < parts.Length - 1; i++) // 不包含最后一段，因为那是自身
            {
                if (i > 0) current += ".";
                current += parts[i];
                _keyIndex++;
                TagToIndexDic[current] = _keyIndex;
                targetSet.Add(_keyIndex);
            }
        }

        public List<int> GetExpansion(string tag)
        {
            if (TagKeyValueDic.TryGetValue(tag, out var set))
                return set;

            return null;
        }

        public int GetTagIndex(string tag)
        {
            return TagToIndexDic[tag] | 0;
        }

        public List<string> AllTags = new List<string>()
        {
            "Buff.Dizziness",
            "Buff.Silence",
            "Buff.Weak",
            "Buff.Roll",
            "Buff.AttackDamageForbid",
            "Buff.MoveForbid",
            "Buff.SkillForbid",
            "Buff.UnStopped",
            "Buff.Fire.Normal",
            "Buff.Fire.Hight",
            "Buff.Fire.Ignite",
            "Stance.Form",
            "Locomotion.Airborne",
        };
    }
}
