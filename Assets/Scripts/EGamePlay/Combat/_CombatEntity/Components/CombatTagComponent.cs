using ACTGameEditor;
using EGamePlay;
using System.Collections.Generic;

namespace EGamePlay.Combat
{
    /// <summary>
    /// Tag 读写与定时释放；底层容器来自 <see cref="StatusComponent.TagContainer"/>。
    /// </summary>
    public sealed class CombatTagComponent : Component
    {
        struct TimedTagRelease
        {
            public TagSource Source;
            public string TagName;
            public float EndTime;
        }

        GameplayTagContainer _container;
        readonly List<TimedTagRelease> _timedTagReleases = new List<TimedTagRelease>(4);

        public int AttackDamageForbidIndex { get; private set; }
        public int MoveForbidIndex { get; private set; }
        public int SkillForbidIndex { get; private set; }
        public int UnStoppedIndex { get; private set; }

        public override bool IsNeedUpdate { get; protected set; } = true;

        public override void Awake()
        {
            _container = Entity.GetComponent<StatusComponent>().TagContainer;
            AttackDamageForbidIndex = TagCollection.TagToIndexDic[CombatTags.BuffAttackDamageForbid];
            MoveForbidIndex = TagCollection.TagToIndexDic[CombatTags.BuffMoveForbid];
            SkillForbidIndex = TagCollection.TagToIndexDic[CombatTags.BuffSkillForbid];
            UnStoppedIndex = TagCollection.TagToIndexDic[CombatTags.BuffUnStopped];
        }

        public override void OnDestroy()
        {
            _timedTagReleases.Clear();
            _container = null;
        }

        public override void Update(float deltaTime)
        {
            TickTimedTags(GameTimeManager.PlayerTime);
        }

        /// <summary>按预缓存索引查询 Tag（门控热路径）。</summary>
        public bool HasIndex(int tagIndex)
        {
            return _container != null && _container.HasTag(tagIndex);
        }

        public bool HasTag(string tagName)
        {
            return _container != null && _container.HasTag(tagName);
        }

        /// <summary>按 RequiredTags/BlockedTags 判断是否可释放技能。</summary>
        public bool CanSpellSkillWithTagLists(List<string> required, List<string> blocked)
        {
            return _container != null && _container.CanSpellSkillWithTagLists(required, blocked);
        }

        /// <summary>兼容旧调用：Manual 源 Push。</summary>
        public void AddTag(string tagName)
        {
            _container?.Push(TagSource.Manual(), tagName);
        }

        /// <summary>兼容旧调用：无源 Remove（防负数）。</summary>
        public void RemoveTag(string tagName)
        {
            _container?.RemoveTag(tagName);
        }

        /// <summary>带来源压入 Tag。</summary>
        public void PushTag(TagSource source, string tagName)
        {
            _container?.Push(source, tagName);
        }

        /// <summary>配对弹出 Tag。</summary>
        public void PopTag(TagSource source, string tagName)
        {
            _container?.Pop(source, tagName);
        }

        /// <summary>移除某来源的全部 Tag（技能 Break / 切形态）。</summary>
        public void PopTagsFrom(TagSource source)
        {
            _container?.PopAll(source);
            for (int i = _timedTagReleases.Count - 1; i >= 0; i--)
            {
                if (_timedTagReleases[i].Source.Equals(source))
                    _timedTagReleases.RemoveAt(i);
            }
        }

        /// <summary>按秒授予 Tag，到期自动 Pop 一条同 Source 授予。</summary>
        public void GrantTagFor(TagSource source, string tagName, float durationSeconds)
        {
            if (_container == null || string.IsNullOrEmpty(tagName))
                return;
            PushTag(source, tagName);
            if (durationSeconds <= 0f)
                return;
            float end = GameTimeManager.PlayerTime + durationSeconds;
            _timedTagReleases.Add(new TimedTagRelease
            {
                Source = source,
                TagName = tagName,
                EndTime = end,
            });
        }

        /// <summary>技能时间轴：短时霸体。</summary>
        public void GrantUnstoppedFor(float durationSeconds, TagSource source)
        {
            GrantTagFor(source, CombatTags.BuffUnStopped, durationSeconds);
        }

        void TickTimedTags(float playerTime)
        {
            for (int i = _timedTagReleases.Count - 1; i >= 0; i--)
            {
                if (playerTime < _timedTagReleases[i].EndTime)
                    continue;
                TimedTagRelease entry = _timedTagReleases[i];
                _timedTagReleases.RemoveAt(i);
                PopTag(entry.Source, entry.TagName);
            }
        }
    }
}
