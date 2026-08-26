using EGamePlay;
using EGamePlay.Combat;
using System.Collections.Generic;

namespace ACTGameEditor.Combat
{
    /// <summary>ACT 层技能时间轴资产加载；与 AbilityDefinition 表数据解耦。</summary>
    public static class ActSkillTimelineLoader
    {
        static readonly Dictionary<int, SkillAllEventData> Cache = new Dictionary<int, SkillAllEventData>(64);

        /// <summary>获取或加载主动技能时间轴；非主动技能或缺失配置返回 null。</summary>
        public static SkillAllEventData GetOrLoad(int skillId)
        {
            if (skillId <= 0)
                return null;

            if (Cache.TryGetValue(skillId, out SkillAllEventData cached))
                return cached;

            var config = SkillSettingMgr.Instance.GetSkillDemoSetting(skillId);
            if (config == null || config.Type != AbilityType.ActiveSkill.ToString())
                return null;

            var skillData = AssetBundleManager.Instance.LoadAssetSync<SkillAllEventData>(
                PrefabPath.GetSkillDataScriObjPath(false), config.SkillId.ToString());

            if (skillData != null)
                Cache[skillId] = skillData;

            return skillData;
        }

        /// <summary>预加载若干技能时间轴。</summary>
        public static void Preload(params int[] skillIds)
        {
            if (skillIds == null)
                return;

            for (int i = 0; i < skillIds.Length; i++)
            {
                if (skillIds[i] > 0)
                    GetOrLoad(skillIds[i]);
            }
        }

        /// <summary>清除缓存（场景切换等）。</summary>
        public static void Clear() => Cache.Clear();
    }
}
