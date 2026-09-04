using System.Collections.Generic;
using EGamePlay;

namespace EGamePlay.Combat
{
    /// <summary>
    /// 技能等级：按升级组存档。连招多个 SkillId 共用一组，缺省 1。
    /// </summary>
    public sealed class SkillLevelComponent : Component
    {
        readonly Dictionary<int, int> _groupLevels = new Dictionary<int, int>(8);

        public override void OnDestroy()
        {
            _groupLevels.Clear();
        }

        public override void OnReset()
        {
            _groupLevels.Clear();
            base.OnReset();
        }

        /// <summary>读技能当前等级；未设过的组为 1，并夹到该技能 <c>MaxLevel</c>。</summary>
        public int GetLevel(int skillId)
        {
            if (skillId <= 0)
                return 1;
            var config = SkillSettingMgr.Instance?.GetSkillDemoSettingOrNull(skillId);
            int groupId = config != null ? config.ResolvedGroupId : skillId;
            int max = config != null ? config.ResolvedMaxLevel : 10;
            return GetGroupLevelClamped(groupId, max);
        }

        /// <summary>用已加载的技能定义读等级，避免命中路径再查 SkillDemo。</summary>
        public int GetLevel(Ability ability)
        {
            var config = ability?.Definition?.Config;
            if (config == null)
                return GetLevel(ability?.SkillID ?? 0);
            return GetGroupLevelClamped(config.ResolvedGroupId, config.ResolvedMaxLevel);
        }

        /// <summary>按升级组设等级，夹到该组技能 MaxLevel 的最大者。返回实际写入值。</summary>
        public int SetLevel(int groupId, int level)
        {
            if (groupId <= 0)
                return 1;

            int max = 10;
            var mgr = SkillSettingMgr.Instance;
            if (mgr != null)
                max = mgr.ResolveMaxLevelForGroup(groupId);

            level = ClampLevel(level, max);
            _groupLevels[groupId] = level;
            return level;
        }

        /// <summary>按 SkillId 解析升级组后再 <see cref="SetLevel"/>。</summary>
        public int SetLevelBySkill(int skillId, int level)
        {
            if (skillId <= 0)
                return 1;
            var config = SkillSettingMgr.Instance?.GetSkillDemoSettingOrNull(skillId);
            int groupId = config != null ? config.ResolvedGroupId : skillId;
            return SetLevel(groupId, level);
        }

        int GetGroupLevelClamped(int groupId, int max)
        {
            if (!_groupLevels.TryGetValue(groupId, out int level))
                level = 1;
            return ClampLevel(level, max);
        }

        static int ClampLevel(int level, int max)
        {
            if (max < 1)
                max = 10;
            if (level < 1)
                return 1;
            return level > max ? max : level;
        }
    }
}
