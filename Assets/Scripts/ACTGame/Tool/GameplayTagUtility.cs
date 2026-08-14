using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EGamePlay.Combat;

namespace ACTGameEditor
{
    public static class GameplayTagUtility
    {
        /// <summary>将 RequiredTags/BlockedTags 转为 TagMask，供 CanSpellSkill 使用。</summary>
        public static TagMask BuildTagMask(List<string> tags)
        {
            var mask = new TagMask();
            if (tags == null || tags.Count == 0) return mask;
            if (TagCollection.TagToIndexDic == null) return mask;
            foreach (var tag in tags)
            {
                if (string.IsNullOrEmpty(tag)) continue;
                if (TagCollection.TagToIndexDic.TryGetValue(tag, out int idx))
                    mask.SetBit(idx);
            }
            return mask;
        }
    }
}
