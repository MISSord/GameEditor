using System.Collections.Generic;

namespace EGamePlay.Combat
{
    /// <summary>Tag 位图工具。</summary>
    public static class GameplayTagUtility
    {
        /// <summary>将 RequiredTags/BlockedTags 转为 TagMask，供 CanSpellSkill 使用。</summary>
        public static TagMask BuildTagMask(List<string> tags)
        {
            var mask = new TagMask();
            if (tags == null || tags.Count == 0)
                return mask;
            if (TagCollection.TagToIndexDic == null)
                return mask;

            for (int i = 0; i < tags.Count; i++)
            {
                string tag = tags[i];
                if (string.IsNullOrEmpty(tag))
                    continue;
                if (TagCollection.TagToIndexDic.TryGetValue(tag, out int idx))
                    mask.SetBit(idx);
            }
            return mask;
        }
    }
}
