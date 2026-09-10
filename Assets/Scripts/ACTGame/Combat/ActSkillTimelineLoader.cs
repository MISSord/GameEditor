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
            if (skillData == null)
            {
                // 敌人专用轴（12000 号段）在 SkillData_Enemy 目录，与玩家技能分离演进
                skillData = AssetBundleManager.Instance.LoadAssetSync<SkillAllEventData>(
                    PrefabPath.GetSkillDataScriObjPath(true), config.SkillId.ToString());
            }

            if (skillData == null && skillId == CombatChainSkill.SkillId)
            {
#if UNITY_EDITOR
                GameLog.CombatDebug($"[Chain] {CombatChainSkill.SkillId} 轴未导出，暂用 {CombatChainSkill.FallbackTimelineSkillId}");
#endif
                return GetOrLoad(CombatChainSkill.FallbackTimelineSkillId);
            }

            if (skillData == null && CombatParry.IsPlayerParrySkill(skillId))
            {
#if UNITY_EDITOR
                GameLog.CombatDebug($"[Parry] {CombatParry.PlayerSkillId} 轴未导出，暂用 {CombatParry.PlayerFallbackTimelineSkillId}");
#endif
                skillData = GetOrLoad(CombatParry.PlayerFallbackTimelineSkillId);
            }
            else if (skillData == null && CombatParry.IsEnemyYellowSkill(skillId))
            {
#if UNITY_EDITOR
                GameLog.CombatDebug($"[Parry] {CombatParry.EnemyYellowSkillId} 轴未导出，暂用 {CombatParry.EnemyFallbackTimelineSkillId}");
#endif
                skillData = GetOrLoad(CombatParry.EnemyFallbackTimelineSkillId);
            }

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
