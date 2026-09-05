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
            "Buff.Freeze",
            "Buff.Silence",
            "Buff.Weak",
            "Buff.Roll",
            "Buff.AttackDamageForbid",
            "Buff.MoveForbid",
            "Buff.SkillForbid",
            "Buff.UnStopped",
            "Buff.Debuff",
            "Buff.Buff",
            "Buff.Bind.Skill",
            "Buff.Bind.Form",
            "Buff.Bind.HitsTaken",
            "Buff.Bind.HitsDealt",
            "Buff.Fire.Normal",
            "Buff.Fire.Hight",
            "Buff.Fire.Ignite",
            "Stance.Form",
            "Locomotion.Airborne",
        };
    }

    /// <summary>
    /// 与战斗逻辑强相关的 Tag 名常量，集中管理以避免硬编码字符串。
    /// 须与 <see cref="TagCollection.AllTags"/> 中的条目保持一致。
    /// </summary>
    public static class CombatTags
    {
        public const string BuffAttackDamageForbid = "Buff.AttackDamageForbid";
        public const string BuffMoveForbid = "Buff.MoveForbid";
        public const string BuffSkillForbid = "Buff.SkillForbid";
        public const string BuffUnStopped = "Buff.UnStopped";
        public const string BuffRoll = "Buff.Roll";
        /// <summary>冻结：停动画与位移（实体 TimeScale=0），点燃仍走世界钟。</summary>
        public const string BuffFreeze = "Buff.Freeze";
        /// <summary>减益极性，净化 DebuffOnly 认表 BuffTag，不是运行时容器。</summary>
        public const string BuffDebuff = "Buff.Debuff";
        /// <summary>增益极性，净化 BuffOnly 认表 BuffTag。</summary>
        public const string BuffGain = "Buff.Buff";
        /// <summary>直到施加时那次技能轴结束。</summary>
        public const string BuffBindSkill = "Buff.Bind.Skill";
        /// <summary>直到持有者离开施加时的形态。</summary>
        public const string BuffBindForm = "Buff.Bind.Form";
        /// <summary>持有者被实际扣血 N 次后卸，N 用 BaseTimes。</summary>
        public const string BuffBindHitsTaken = "Buff.Bind.HitsTaken";
        /// <summary>持有者打出实际扣血 N 次后卸，N 用 BaseTimes。</summary>
        public const string BuffBindHitsDealt = "Buff.Bind.HitsDealt";
        /// <summary>处于非默认战斗形态（明心境、变身等）。</summary>
        public const string StanceForm = "Stance.Form";
        /// <summary>空中；Resolver 主要用 IsAirborne，此标签供技能边条件。</summary>
        public const string LocomotionAirborne = "Locomotion.Airborne";
    }
}
