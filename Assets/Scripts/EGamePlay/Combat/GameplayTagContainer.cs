using System;
using System.Collections.Generic;

namespace EGamePlay.Combat
{
    /// <summary>
    /// 谁授予了这条 Tag。与 <see cref="TagSource.Id"/> 组成配对钥匙：
    /// 同一 <c>State.Freeze</c> 可被 Buff 与 Modify 同时推，计数 &gt;1；卸一路只 Pop 这一路。
    /// 技能 Break / 切形态走 <see cref="GameplayTagContainer.PopAll"/>。
    /// </summary>
    public enum TagSourceKind : byte
    {
        /// <summary>代码临时挂（小队换人 i-frame、时间轴 Msg 霸体等）。Id 由调用方自定，0 表示匿名 Manual。</summary>
        Manual = 0,
        /// <summary>Buff 激活推 <c>GrantedTags</c>。Id = Buff 实体 Id。Deactivate 时 Pop。</summary>
        Buff = 1,
        /// <summary>技能轴 Tag 轨，或出招当下抢先挂的 Roll。Id = 技能 runner Id。轴结束 / Break 时 PopAll。</summary>
        Skill = 2,
        /// <summary>形态 <c>GrantedTags</c> 与 <see cref="CombatTags.StanceForm"/>。Id = FormId。切形态 PopAll。</summary>
        Form = 3,
        /// <summary>Buff Modify <c>PlayerControll</c> 推 Forbid / Freeze。Id = Buff 实体 Id。Modify 撤销时 Pop。可与 Buff 源叠同一 Tag。</summary>
        Modify = 4,
    }

    /// <summary>Tag 推送来源，用于配对 Pop / PopAll。不要只用字符串减计数，否则会卸错路。</summary>
    public readonly struct TagSource : IEquatable<TagSource>
    {
        /// <summary>授予种类，见 <see cref="TagSourceKind"/>。</summary>
        public readonly TagSourceKind Kind;
        /// <summary>该种类下的实例 Id（Buff/Modify=Buff.Id，Skill=runner，Form=FormId）。</summary>
        public readonly long Id;

        public TagSource(TagSourceKind kind, long id)
        {
            Kind = kind;
            Id = id;
        }

        /// <summary>匿名或带自定义 Id 的临时授予。</summary>
        public static TagSource Manual(long id = 0) => new TagSource(TagSourceKind.Manual, id);
        /// <summary><paramref name="buffId"/> 为 Buff 实体 Id，不是表 BuffId。</summary>
        public static TagSource Buff(long buffId) => new TagSource(TagSourceKind.Buff, buffId);
        /// <summary><paramref name="runnerId"/> 为技能轴 runner Id。</summary>
        public static TagSource Skill(long runnerId) => new TagSource(TagSourceKind.Skill, runnerId);
        /// <summary><paramref name="formId"/> 为形态配置 Id。</summary>
        public static TagSource Form(int formId) => new TagSource(TagSourceKind.Form, formId);
        /// <summary><paramref name="buffId"/> 为持有该 Modify 的 Buff 实体 Id。</summary>
        public static TagSource Modify(long buffId) => new TagSource(TagSourceKind.Modify, buffId);

        public bool Equals(TagSource other) => Kind == other.Kind && Id == other.Id;
        public override bool Equals(object obj) => obj is TagSource other && Equals(other);
        public override int GetHashCode() => ((int)Kind * 397) ^ Id.GetHashCode();
    }

    /// <summary>
    /// Owned Tag 的 Matching 位图：某 intern index 计数 &gt;0 则对应 bit 亮。
    /// 子 Tag 会点亮祖先 bit，所以 Required=<c>State.Fire</c> 在持有 <c>State.Fire.Normal</c> 时也能过。
    /// </summary>
    public struct TagMask
    {
        /// <summary>intern index 1..63。0 未使用（哨兵）。</summary>
        public ulong Part0;
        /// <summary>intern index 64..127。</summary>
        public ulong Part1;

        /// <summary>点亮 intern index 对应位。越界忽略。</summary>
        public void SetBit(int index)
        {
            if ((uint)index < 64u)
                Part0 |= 1UL << index;
            else if ((uint)index < (uint)TagCollection.MaxTagCount)
                Part1 |= 1UL << (index - 64);
        }

        /// <summary>熄灭 intern index 对应位。越界忽略。</summary>
        public void ClearBit(int index)
        {
            if ((uint)index < 64u)
                Part0 &= ~(1UL << index);
            else if ((uint)index < (uint)TagCollection.MaxTagCount)
                Part1 &= ~(1UL << (index - 64));
        }

        /// <summary>Required 全中（HasAll）且 Blocked 一个都不中（HasAny 为假）。空 Required 视为满足。</summary>
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
    /// 标签容器：Matching 位图 + Explicit 计数；Push/Pop 带 Source，Break 时可 PopAll。
    /// <see cref="HasTag(string)"/> = GAS HasMatching（含子）；<see cref="HasExactTag"/> = 仅显式授予。
    /// </summary>
    public class GameplayTagContainer
    {
        const int Capacity = TagCollection.MaxTagCount;

        /// <summary>Matching 位图缓存，与 <see cref="_matchingCounts"/> 同步，供 Required/Blocked 按位检查。</summary>
        TagMask _currentMask;
        /// <summary>
        /// GAS HasMatching 计数：Push 叶子时祖先一并 +1。
        /// <c>HasTag("State")</c> 在只挂了 <c>State.Freeze</c> 时为真。热路径门控读这个。
        /// </summary>
        readonly short[] _matchingCounts = new short[Capacity];
        /// <summary>
        /// 仅被显式 Push 的那条路径 +1，不含祖先。
        /// <c>HasExactTag("State")</c> 在只挂了 <c>State.Freeze</c> 时为假；要父路径 Exact 必须 Push 父路径本身。
        /// </summary>
        readonly short[] _explicitCounts = new short[Capacity];
        /// <summary>每条 Push 一条记录，Pop / PopAll 按 <see cref="TagSource"/> 配对，避免只按名字减错路。</summary>
        readonly List<TagGrant> _grants = new List<TagGrant>(16);

        /// <summary>该路径 Matching 计数 0↔1 时回调（index, present）。硬控/冻结沿变用，无分配。</summary>
        public Action<int, bool> LeafCountChanged;

        /// <summary>一次授予：谁（Source）推了哪条路径（TagName，不含自动展开的祖先）。</summary>
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
            if (!TryAddTagInternal(tagName, out int exactIndex, out bool becamePresent))
                return;
            _grants.Add(new TagGrant { Source = source, TagName = tagName });
            if (becamePresent)
                LeafCountChanged?.Invoke(exactIndex, true);
        }

        /// <summary>配对弹出一条同名授予。找不到 grant 时不减计数。</summary>
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
                NotifyRemove(tagName);
                return;
            }
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
                NotifyRemove(name);
            }
        }

        /// <summary>兼容旧 API：等价 Manual 源 Push。</summary>
        public void AddTag(string tagName) => Push(TagSource.Manual(), tagName);

        /// <summary>兼容旧 API：按名字减一条授予（忽略 Source）。</summary>
        public void RemoveTag(string tagName)
        {
            if (string.IsNullOrEmpty(tagName))
                return;

            for (int i = _grants.Count - 1; i >= 0; i--)
            {
                if (_grants[i].TagName != tagName)
                    continue;
                _grants.RemoveAt(i);
                NotifyRemove(tagName);
                return;
            }
        }

        void NotifyRemove(string tagName)
        {
            if (!TryRemoveTagInternal(tagName, out int exactIndex, out bool becameAbsent))
                return;
            if (becameAbsent)
                LeafCountChanged?.Invoke(exactIndex, false);
        }

        bool TryAddTagInternal(string tagName, out int exactIndex, out bool becamePresent)
        {
            exactIndex = 0;
            becamePresent = false;
            if (!TagCollection.TryGetHierarchy(tagName, out int[] hierarchy)
                || hierarchy == null
                || hierarchy.Length == 0)
                return false;

            exactIndex = hierarchy[0];
            if ((uint)exactIndex >= (uint)Capacity)
                return false;

            for (int i = 0; i < hierarchy.Length; i++)
            {
                int id = hierarchy[i];
                if ((uint)id >= (uint)Capacity)
                    continue;
                if (_matchingCounts[id] < short.MaxValue)
                    _matchingCounts[id]++;
                if (_matchingCounts[id] > 0)
                    _currentMask.SetBit(id);
            }

            if (_explicitCounts[exactIndex] < short.MaxValue)
                _explicitCounts[exactIndex]++;

            becamePresent = _matchingCounts[exactIndex] == 1;
            return true;
        }

        bool TryRemoveTagInternal(string tagName, out int exactIndex, out bool becameAbsent)
        {
            exactIndex = 0;
            becameAbsent = false;
            if (!TagCollection.TryGetHierarchy(tagName, out int[] hierarchy)
                || hierarchy == null
                || hierarchy.Length == 0)
                return false;

            exactIndex = hierarchy[0];
            if ((uint)exactIndex >= (uint)Capacity)
                return false;

            bool wasPresent = _matchingCounts[exactIndex] > 0;
            for (int i = 0; i < hierarchy.Length; i++)
            {
                int id = hierarchy[i];
                if ((uint)id >= (uint)Capacity)
                    continue;
                if (_matchingCounts[id] <= 0)
                    continue;
                _matchingCounts[id]--;
                if (_matchingCounts[id] <= 0)
                {
                    _matchingCounts[id] = 0;
                    _currentMask.ClearBit(id);
                }
            }

            if (_explicitCounts[exactIndex] > 0)
                _explicitCounts[exactIndex]--;

            becameAbsent = wasPresent && _matchingCounts[exactIndex] <= 0;
            return true;
        }

        /// <summary>GAS HasMatching：该路径或任一子孙被授予则为真。</summary>
        public bool HasTag(string tagName)
        {
            if (!TagCollection.TryGetIndex(tagName, out int index))
                return false;
            return HasTag(index);
        }

        /// <summary>按 intern index 查 Matching 计数（门控热路径）。0 为哨兵，恒为假。</summary>
        public bool HasTag(int tagIndex)
        {
            if (tagIndex <= 0 || tagIndex >= _matchingCounts.Length)
                return false;
            return _matchingCounts[tagIndex] > 0;
        }

        /// <summary>仅显式 Push 过该路径。子 Tag 不会让父路径 Exact 为真。</summary>
        public bool HasExactTag(string tagName)
        {
            if (!TagCollection.TryGetIndex(tagName, out int index))
                return false;
            if (index <= 0 || index >= _explicitCounts.Length)
                return false;
            return _explicitCounts[index] > 0;
        }

        /// <summary>预编译 mask 版 Tag 门控。Required 空 mask 视为满足。</summary>
        public bool CanSpellSkill(TagMask required, TagMask blocked) =>
            TagMask.Check(_currentMask, required, blocked);

        /// <summary>技能表 Required（HasAll）/ Blocked（HasAny）。只做 Tag 条件，不含死亡/硬控总闸。</summary>
        public bool CanSpellSkillWithTagLists(List<string> required, List<string> blocked)
        {
            return CanSpellSkill(BuildTagMask(required), BuildTagMask(blocked));
        }

        /// <summary>将 RequiredTags/BlockedTags 转为 TagMask。父路径 bit 会被子授予点亮，因此父条件按 Matching 生效。</summary>
        static TagMask BuildTagMask(List<string> tags)
        {
            var mask = new TagMask();
            if (tags == null || tags.Count == 0)
                return mask;

            for (int i = 0; i < tags.Count; i++)
            {
                string tag = tags[i];
                if (string.IsNullOrEmpty(tag))
                    continue;
                if (TagCollection.TryGetIndex(tag, out int idx))
                    mask.SetBit(idx);
            }

            return mask;
        }

        /// <summary>清空 Matching / Explicit / 位图 / 授予记录。实体回收时用。</summary>
        public void Reset()
        {
            Array.Clear(_matchingCounts, 0, Capacity);
            Array.Clear(_explicitCounts, 0, Capacity);
            _currentMask = default;
            _grants.Clear();
        }
    }
}
