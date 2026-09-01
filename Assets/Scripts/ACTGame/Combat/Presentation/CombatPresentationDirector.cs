using System.Collections.Generic;
using EGamePlay.Combat;

namespace ACTGameEditor.Combat
{
    /// <summary>战斗表现编排：Play / Stop / StopBySource，路由到底层 Bridge。</summary>
    public static class CombatPresentationDirector
    {
        sealed class CombatFxEntry
        {
            public CombatFxHandle Handle;
            public CombatFxSource Source;
            public CombatFxKind Kind;
            public object BackendToken;
            public long TargetEntityId;
        }

        static readonly Dictionary<int, CombatFxEntry> _entries = new Dictionary<int, CombatFxEntry>(16);
        static readonly Dictionary<CombatFxSource, List<int>> _bySource = new Dictionary<CombatFxSource, List<int>>(8);
        static readonly List<int> _removeBuffer = new List<int>(8);
        static int _nextHandleId = 1;

#if UNITY
        static readonly TimeScaleFxBridge TimeScaleBridge = new TimeScaleFxBridge();
        static readonly CameraPostFxBridge CameraBridge = new CameraPostFxBridge();
        static readonly CharacterRenderFxBridge CharacterBridge = new CharacterRenderFxBridge();
#endif

        /// <summary>播放表现；失败返回 <see cref="CombatFxHandle.Invalid"/>。</summary>
        public static CombatFxHandle Play(in CombatFxSpec spec)
        {
            if (spec.Kind == CombatFxKind.SkillTimeStop && spec.Duration <= 0f)
                return CombatFxHandle.Invalid;

            StopSameSourceKind(spec.Source, spec.Kind);

#if UNITY
            object token = null;
            ICombatFxBridge bridge = ResolveBridge(spec.Kind);
            if (bridge != null)
            {
                if (!bridge.CanPlay(spec))
                    return CombatFxHandle.Invalid;
                token = bridge.Play(spec);
            }

            if (spec.PlayCameraImpact && spec.Kind == CombatFxKind.HitStop)
                CameraBridge.Play(spec);

            if (token == null && spec.Kind != CombatFxKind.RadialBlurImpact && !spec.PlayCameraImpact)
            {
                if (spec.Kind is CombatFxKind.HitFlash or CombatFxKind.DeathDissolve)
                    return CombatFxHandle.Invalid;
            }
#else
            object token = null;
#endif

            int handleId = _nextHandleId++;
            var handle = new CombatFxHandle(handleId);
            var entry = new CombatFxEntry
            {
                Handle = handle,
                Source = spec.Source,
                Kind = spec.Kind,
                BackendToken = token,
                TargetEntityId = spec.Target?.Id ?? 0,
            };

            _entries[handleId] = entry;
            AddSourceIndex(spec.Source, handleId);
            return handle;
        }

        /// <summary>撤销单个句柄。</summary>
        public static void Stop(CombatFxHandle handle)
        {
            if (!handle.IsValid || !_entries.TryGetValue(handle.Id, out CombatFxEntry entry))
                return;

            StopEntry(entry);
            _entries.Remove(handle.Id);
            RemoveSourceIndex(entry.Source, handle.Id);
        }

        /// <summary>撤销某来源下全部效果（技能 Break 必调）。</summary>
        public static void StopBySource(in CombatFxSource source)
        {
            if (!_bySource.TryGetValue(source, out List<int> list) || list.Count == 0)
                return;

            _removeBuffer.Clear();
            _removeBuffer.AddRange(list);
            for (int i = 0; i < _removeBuffer.Count; i++)
            {
                int handleId = _removeBuffer[i];
                if (_entries.TryGetValue(handleId, out CombatFxEntry entry))
                    StopEntry(entry);
                _entries.Remove(handleId);
            }

            list.Clear();
        }

        /// <summary>撤销绑定实体上的效果，可选保留死亡溶解。</summary>
        public static void StopByEntity(long entityId, bool keepDeathDissolve = false)
        {
            _removeBuffer.Clear();
            foreach (var pair in _entries)
            {
                CombatFxEntry entry = pair.Value;
                if (entry.TargetEntityId != entityId && !(entry.Source.Kind == TagSourceKind.Manual && entry.Source.Id == entityId))
                    continue;
                if (keepDeathDissolve && entry.Kind == CombatFxKind.DeathDissolve)
                    continue;
                _removeBuffer.Add(pair.Key);
            }

            for (int i = 0; i < _removeBuffer.Count; i++)
                Stop(new CombatFxHandle(_removeBuffer[i]));
        }

        /// <summary>清场（切场景 / 退出）。</summary>
        public static void ClearAll()
        {
            foreach (var pair in _entries)
                StopEntry(pair.Value);
            _entries.Clear();
            _bySource.Clear();
            _nextHandleId = 1;
        }

        static void StopSameSourceKind(in CombatFxSource source, CombatFxKind kind)
        {
            if (!_bySource.TryGetValue(source, out List<int> list))
                return;

            _removeBuffer.Clear();
            for (int i = 0; i < list.Count; i++)
            {
                int handleId = list[i];
                if (_entries.TryGetValue(handleId, out CombatFxEntry entry) && entry.Kind == kind)
                    _removeBuffer.Add(handleId);
            }

            for (int i = 0; i < _removeBuffer.Count; i++)
                Stop(new CombatFxHandle(_removeBuffer[i]));
        }

        static void StopEntry(CombatFxEntry entry)
        {
#if UNITY
            ICombatFxBridge bridge = ResolveBridge(entry.Kind);
            bridge?.Stop(entry.BackendToken, entry.Kind);
#endif
        }

#if UNITY
        static ICombatFxBridge ResolveBridge(CombatFxKind kind)
        {
            return kind switch
            {
                CombatFxKind.SkillTimeStop or CombatFxKind.TimeFracture or CombatFxKind.HitStop => TimeScaleBridge,
                CombatFxKind.RadialBlurImpact => CameraBridge,
                CombatFxKind.HitFlash or CombatFxKind.DeathDissolve => CharacterBridge,
                _ => null,
            };
        }
#endif

        static void AddSourceIndex(in CombatFxSource source, int handleId)
        {
            if (!_bySource.TryGetValue(source, out List<int> list))
            {
                list = new List<int>(2);
                _bySource[source] = list;
            }
            list.Add(handleId);
        }

        static void RemoveSourceIndex(in CombatFxSource source, int handleId)
        {
            if (!_bySource.TryGetValue(source, out List<int> list))
                return;
            list.Remove(handleId);
            if (list.Count == 0)
                _bySource.Remove(source);
        }
    }
}
