using ACTGameEditor;
using EGamePlay;
using Flux;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FluxEditor
{
    /// <summary>从 SkillAllEventData 向 FSequence 同步 Input / Trigger / Effect 轨道。</summary>
    internal static class SaveSequenceAssetSync
    {
        public static void LoadCombatEventsFromAsset(FSequence seq, AgentModelType agentName)
        {
            if (seq.FSeqSetting == null || string.IsNullOrEmpty(seq.SkillId))
            {
                Debug.LogWarning("FSequence 需配置 FSeqSetting 和 SkillId。");
                return;
            }

            string resDir = "Assets/Game/Config/" + agentName.GetSkillPath() + seq.SkillId + ".asset";
            var skillData = AssetDatabase.LoadAssetAtPath<SkillAllEventData>(resDir);
            if (skillData == null)
            {
                Debug.LogWarning($"未找到 SkillAllEventData: {resDir}");
                return;
            }

            var container = seq.Containers[0];
            if (container == null || container.Timelines.Count == 0)
            {
                Debug.LogWarning("FSequence 无有效 Container/Timeline。");
                return;
            }

            Undo.RegisterCompleteObjectUndo(seq, "Load Input/Trigger/Effect");
            int inputCount = 0;
            int triggerCount = 0;
            int effectCount = 0;
            for (int i = 0; i < skillData.skillAllEventDatas.Count && i < container.Timelines.Count; i++)
            {
                var skillEventData = skillData.skillAllEventDatas[i];
                var timeline = container.Timelines[i];
                inputCount += SyncTrackEvents(timeline, skillEventData.SkillInputEvents?.Events, SaveSequenceEventConverters.TOFSkillInputEvent);
                triggerCount += SyncTrackEvents(timeline, skillEventData.TriggerEvents?.Events, SaveSequenceEventConverters.TOFTriggerEvent);
                effectCount += SyncTrackEvents(timeline, skillEventData.EffectEvents?.Events, SaveSequenceEventConverters.TOFEffectEvent);
            }

            EditorUtility.SetDirty(seq);
            Debug.Log($"已从 Asset 同步 Input={inputCount} Trigger={triggerCount} Effect={effectCount}（SchemaVersion={skillData.SchemaVersion}）。");
        }

        static int SyncTrackEvents<TEvent, TData>(FTimeline timeline, List<TData> events, Func<TData, TEvent> toF)
            where TEvent : FEvent
        {
            if (events == null || events.Count == 0)
                return 0;

            FTrack track = null;
            foreach (var t in timeline.Tracks)
            {
                if (t.GetEventType() == typeof(TEvent))
                {
                    track = t;
                    break;
                }
            }

            if (track == null)
            {
                track = FTrack.Create<TEvent>();
                timeline.Add(track);
            }

            var toRemove = new List<FEvent>();
            foreach (var ev in track.Events)
            {
                if (ev is TEvent)
                    toRemove.Add(ev);
            }

            foreach (var ev in toRemove)
            {
                track.Remove(ev);
                if (ev.gameObject != null)
                    Object.DestroyImmediate(ev.gameObject);
            }

            int n = 0;
            for (int i = 0; i < events.Count; i++)
            {
                track.Add(toF(events[i]));
                n++;
            }
            return n;
        }
    }
}
