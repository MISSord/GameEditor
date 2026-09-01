using System;
using System.Collections.Generic;

namespace EGamePlay.Combat
{
    /// <summary>Tag 来源种类。</summary>
    public enum TagSourceKind : byte
    {
        Manual = 0,
        Buff = 1,
        Skill = 2,
        Form = 3,
        Modify = 4,
    }

    /// <summary>Tag 推送来源，用于配对 Pop / PopAll。</summary>
    public readonly struct TagSource : IEquatable<TagSource>
    {
        public readonly TagSourceKind Kind;
        public readonly long Id;

        public TagSource(TagSourceKind kind, long id)
        {
            Kind = kind;
            Id = id;
        }

        public static TagSource Manual(long id = 0) => new TagSource(TagSourceKind.Manual, id);
        public static TagSource Buff(long buffId) => new TagSource(TagSourceKind.Buff, buffId);
        public static TagSource Skill(long runnerId) => new TagSource(TagSourceKind.Skill, runnerId);
        public static TagSource Form(int formId) => new TagSource(TagSourceKind.Form, formId);
        public static TagSource Modify(long buffId) => new TagSource(TagSourceKind.Modify, buffId);

        public bool Equals(TagSource other) => Kind == other.Kind && Id == other.Id;
        public override bool Equals(object obj) => obj is TagSource other && Equals(other);
        public override int GetHashCode() => ((int)Kind * 397) ^ Id.GetHashCode();
    }

    public struct TagMask
    {
        public ulong Part0;
        public ulong Part1;

        public void SetBit(int index)
        {
            if (index < 64) Part0 |= (1UL << index);
            else Part1 |= (1UL << (index - 64));
        }

        public void ClearBit(int index)
        {
            if (index < 64) Part0 &= ~(1UL << index);
            else Part1 &= ~(1UL << (index - 64));
        }

        public static bool Check(TagMask current, TagMask required, TagMask blocked)
        {
            bool hasRequired = (current.Part0 & required.Part0) == required.Part0
                && (current.Part1 & required.Part1) == required.Part1;

            bool hasBlocked = (current.Part0 & blocked.Part0) != 0
                || (current.Part1 & blocked.Part1) != 0;

            return hasRequired && !hasBlocked;
        }
    }

    /// <summary>
    /// 标签容器：位图 + 计数；Push/Pop 带 Source，Break 时可 PopAll。
    /// </summary>
    public class GameplayTagContainer
    {
        private TagMask _currentMask;
        private readonly short[] _tagCounts = new short[128];
        private readonly List<TagGrant> _grants = new List<TagGrant>(16);

        struct TagGrant
        {
            public TagSource Source;
            public string TagName;
        }

        /// <summary>带来源压入（计数 + 记录，供 PopAll）。</summary>
        public void Push(TagSource source, string tagName)
        {
            if (string.IsNullOrEmpty(tagName))
                return;
            if (!TryAddTagInternal(tagName))
                return;
            _grants.Add(new TagGrant { Source = source, TagName = tagName });
        }

        /// <summary>配对弹出一条同名授予。</summary>
        public void Pop(TagSource source, string tagName)
        {
            if (string.IsNullOrEmpty(tagName))
                return;

            for (int i = _grants.Count - 1; i >= 0; i--)
            {
                TagGrant g = _grants[i];
                if (!g.Source.Equals(source) || g.TagName != tagName)
                    continue;
                _grants.RemoveAt(i);
                TryRemoveTagInternal(tagName);
                return;
            }

            TryRemoveTagInternal(tagName);
        }

        /// <summary>移除某来源的全部授予（技能 Break / 切形态）。</summary>
        public void PopAll(TagSource source)
        {
            for (int i = _grants.Count - 1; i >= 0; i--)
            {
                if (!_grants[i].Source.Equals(source))
                    continue;
                string name = _grants[i].TagName;
                _grants.RemoveAt(i);
                TryRemoveTagInternal(name);
            }
        }

        /// <summary>兼容旧 API：等价 Manual 源 Push。</summary>
        public void AddTag(string tagName) => Push(TagSource.Manual(), tagName);

        /// <summary>兼容旧 API：无 Source 时按叶子名减计数（防负数）。</summary>
        public void RemoveTag(string tagName)
        {
            if (string.IsNullOrEmpty(tagName))
                return;

            for (int i = _grants.Count - 1; i >= 0; i--)
            {
                if (_grants[i].TagName != tagName)
                    continue;
                _grants.RemoveAt(i);
                break;
            }

            TryRemoveTagInternal(tagName);
        }

        bool TryAddTagInternal(string tagName)
        {
            if (TagCollection.TagKeyValueDic == null
                || !TagCollection.TagKeyValueDic.TryGetValue(tagName, out var hierarchy))
                return false;

            foreach (var tag in hierarchy)
            {
                if (_tagCounts[tag] < short.MaxValue)
                    _tagCounts[tag]++;
                if (_tagCounts[tag] > 0)
                    _currentMask.SetBit(tag);
            }
            return true;
        }

        bool TryRemoveTagInternal(string tagName)
        {
            if (TagCollection.TagKeyValueDic == null
                || !TagCollection.TagKeyValueDic.TryGetValue(tagName, out var hierarchy))
                return false;

            foreach (var tag in hierarchy)
            {
                if (_tagCounts[tag] <= 0)
                    continue;
                _tagCounts[tag]--;
                if (_tagCounts[tag] <= 0)
                {
                    _tagCounts[tag] = 0;
                    _currentMask.ClearBit(tag);
                }
            }
            return true;
        }

        public bool HasTag(string tagName)
        {
            if (TagCollection.TagToIndexDic == null
                || !TagCollection.TagToIndexDic.TryGetValue(tagName, out int index))
                return false;
            return _tagCounts[index] > 0;
        }

        public bool HasTag(int tagIndex)
        {
            if (tagIndex < 0 || tagIndex >= _tagCounts.Length)
                return false;
            return _tagCounts[tagIndex] > 0;
        }

        public bool CanSpellSkill(TagMask required, TagMask blocked) =>
            TagMask.Check(_currentMask, required, blocked);

        public bool CanSpellSkillWithTagLists(List<string> required, List<string> blocked)
        {
            return CanSpellSkill(BuildTagMask(required), BuildTagMask(blocked));
        }

        /// <summary>将 RequiredTags/BlockedTags 转为 TagMask，供 CanSpellSkill 使用。</summary>
        private static TagMask BuildTagMask(List<string> tags)
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

        public void Reset()
        {
            for (int i = 0; i < 128; i++)
                _tagCounts[i] = 0;
            _currentMask = default;
            _grants.Clear();
        }
    }
}
