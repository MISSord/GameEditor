using System.Collections.Generic;
using EGamePlay.Combat;

namespace ACTGameEditor
{
    public struct TagMask
    {
        public ulong Part0;
        public ulong Part1;

        // 添加一个位
        public void SetBit(int index)
        {
            if (index < 64) Part0 |= (1UL << index);
            else Part1 |= (1UL << (index - 64));
        }

        // 清除一个位
        public void ClearBit(int index)
        {
            if (index < 64) Part0 &= ~(1UL << index);
            else Part1 &= ~(1UL << (index - 64));
        }

        // 核心：一键判定逻辑
        public static bool Check(TagMask current, TagMask required, TagMask blocked)
        {
            // 判定逻辑：必须包含 Required 的所有位，且不能包含 Blocked 的任何位
            bool hasRequired = (current.Part0 & required.Part0) == required.Part0 &&
                               (current.Part1 & required.Part1) == required.Part1;

            bool hasBlocked = (current.Part0 & blocked.Part0) != 0 ||
                              (current.Part1 & blocked.Part1) != 0;

            return hasRequired && !hasBlocked;
        }
    }

    //标签的加入与移除主要是由技能和Buff来完成
    public class GameplayTagContainer
    {
        // 运行时快照：当前角色状态的位图
        private TagMask _currentMask;
        // 计数器：处理多个 Buff 叠加
        private short[] _tagCounts = new short[128];

        // 添加标签：会自动增加所有父级的计数
        public void AddTag(string tagName)
        {
            var hierarchy = TagCollection.TagKeyValueDic[tagName];
            foreach (var tag in hierarchy)
            {
                _tagCounts[tag]++;
                if (_tagCounts[tag] > 0)
                    _currentMask.SetBit(tag);
            }
        }

        // 移除标签：递减计数，降为 0 时逻辑上移除
        public void RemoveTag(string tagName)
        {
            var hierarchy = TagCollection.TagKeyValueDic[tagName];
            foreach (var tag in hierarchy)
            {
                _tagCounts[tag]--;
                if (_tagCounts[tag] <= 0)
                    _currentMask.ClearBit(tag);
            }
        }

        // 极速匹配：检查中是否存在该键
        public bool HasTag(string tagName)
        {
            int index = TagCollection.TagToIndexDic[tagName];
            return _tagCounts[index] > 0;
        }

        public bool HasTag(int tagIndex)
        {
            return _tagCounts[tagIndex] > 0;
        }

        /// <summary>是否能释放某个技能（Required 必须全满足，Blocked 不能有任一位）。</summary>
        public bool CanSpellSkill(TagMask required, TagMask blocked)
        {
            return TagMask.Check(_currentMask, required, blocked);
        }

        /// <summary>按 RequiredTags/BlockedTags 判断是否可释放技能。required 为空=无要求；blocked 任一存在=不可释放。</summary>
        public bool CanSpellSkillWithTagLists(List<string> required, List<string> blocked)
        {
            var reqMask = GameplayTagUtility.BuildTagMask(required);
            var blkMask = GameplayTagUtility.BuildTagMask(blocked);
            return CanSpellSkill(reqMask, blkMask);
        }

        public void Reset()
        {
            for(int i = 0; i < 128; i++)
            {
                _tagCounts[i] = 0;
            }
        }
    }
}
