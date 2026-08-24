using ACTGameEditor;
using EGamePlay;
using Flux;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FluxEditor
{
    /// <summary>将 FSequence 轨道导出为 SkillAllEventData。</summary>
    internal static class SaveSequenceTrackExporter
    {
        static readonly HashSet<Type> IgnoredPreviewEventTypes = new HashSet<Type>
        {
            typeof(FCommentEvent),
            typeof(FPlayAudioEvent),
            typeof(FVolumeAudioEvent),
            typeof(FGlobalVolumeAudioEvent),
            typeof(FTimescaleEvent),
            typeof(FCameraManagerEvent),
        };

        public static bool Export(FSequence sequence, string skillId, AgentModelType agentName)
        {
            float speed = sequence.Speed;
            int objIndex = 0;
            Transform playerTF = sequence.Containers[0].Timelines[0]._owner;

            int sort = 0;
            var unknownEnabledTypes = new List<string>();
            var skippedPreviewTypes = new HashSet<string>();
            int skillInt = int.Parse(skillId);

            var skillEvents = new List<SkillNewEventData>();
            foreach (var timeline in sequence.Containers[0].Timelines)
            {
                string skillName = objIndex > 0 ? skillId + "_" + objIndex : skillId;
                var skillData = new SkillNewEventData
                {
                    SkillName = skillName,
                    SkillId = skillInt,
                    Speed = speed,
                    SkillSort = sort
                };

                foreach (var track in timeline.Tracks)
                {
                    if (!track.enabled)
                        continue;
                    TryReadTrack(skillData, track, playerTF, unknownEnabledTypes, skippedPreviewTypes);
                }

                skillEvents.Add(skillData);
                objIndex++;
                sort++;
            }

            if (skippedPreviewTypes.Count > 0)
                Debug.Log($"[SaveSequenceData] 跳过预览轨 skillId={skillId}: {string.Join(", ", skippedPreviewTypes)}");

            if (unknownEnabledTypes.Count > 0)
            {
                string unknown = string.Join("\n", unknownEnabledTypes);
                Debug.LogError($"[SaveSequenceData] 未映射的启用轨道，已中止保存 skillId={skillId}: {unknown}");
                EditorUtility.DisplayDialog("保存失败", $"存在未映射的启用轨道，已中止保存：\n{unknown}", "确定");
                return false;
            }

            SortSkillEvents(skillEvents);

            string resDir = "Game/Config/" + agentName.GetSkillPath();
            FileTool.CheckDirOrCreat(Application.dataPath + "/" + resDir);

            string prefabPath = "Assets/" + resDir + skillId + ".asset";
            SkillAllEventData data = AssetDatabase.LoadAssetAtPath<SkillAllEventData>(prefabPath);
            bool isNew = data == null;
            if (isNew)
                data = ScriptableObject.CreateInstance<SkillAllEventData>();

            data.SkillId = skillInt;
            data.SchemaVersion = SkillAllEventData.CurrentSchemaVersion;
            data.skillAllEventDatas = skillEvents;

            if (isNew)
                AssetDatabase.CreateAsset(data, prefabPath);
            else
                EditorUtility.SetDirty(data);

            AssetDatabase.SaveAssetIfDirty(data);
            EditorGUIUtility.PingObject(data);

            string notify = isNew ? $"已新建 {prefabPath}" : $"已更新 {prefabPath}";
            if (FSequenceEditorWindow.instance != null)
                FSequenceEditorWindow.instance.ShowNotification(new GUIContent(notify));
            else
                Debug.Log($"[SaveSequenceData] {notify}");

            return true;
        }

        static void SortSkillEvents(List<SkillNewEventData> skillEvents)
        {
            foreach (SkillNewEventData eventData in skillEvents)
            {
                eventData.AnimEvents.Events.Sort((a, b) => a.Range.Start.CompareTo(b.Range.Start));
                eventData.MoveEvents.Events.Sort((a, b) => a.Range.Start.CompareTo(b.Range.Start));
                eventData.RotateEvents.Events.Sort((a, b) => a.Range.Start.CompareTo(b.Range.Start));
                eventData.ScaleEvents.Events.Sort((a, b) => a.Range.Start.CompareTo(b.Range.Start));
                eventData.SwitchEvents.Events.Sort((a, b) => a.Range.Start.CompareTo(b.Range.Start));
                eventData.MsgEvents.Events.Sort((a, b) => a.Range.Start.CompareTo(b.Range.Start));
                eventData.TriggerEvents.Events.Sort((a, b) => a.Range.Start.CompareTo(b.Range.Start));
                eventData.SkillInputEvents.Events.Sort((a, b) => a.Range.Start.CompareTo(b.Range.Start));
                eventData.EffectEvents.Events.Sort((a, b) => a.Range.Start.CompareTo(b.Range.Start));
            }
        }

        static void TryReadTrack(
            SkillNewEventData res,
            FTrack track,
            Transform playerTF,
            List<string> unknownEnabledTypes,
            HashSet<string> skippedPreviewTypes)
        {
            var trackType = track.GetEventType();
            if (IgnoredPreviewEventTypes.Contains(trackType))
            {
                skippedPreviewTypes.Add(trackType != null ? trackType.Name : "null");
                return;
            }

            if (trackType == typeof(FPlayAnimationEvent))
            {
                foreach (var ev in track.Events)
                {
                    var fEvent = ev as FPlayAnimationEvent;
                    SaveSequenceAnimExporter.AnimEvents.Add(fEvent);
                    res.AnimEvents.Events.Add(SaveSequenceEventConverters.ToXCAnimEvent(fEvent));
                }
            }
            else if (trackType == typeof(FTweenPositionEvent))
            {
                FTweenPositionEvent lastEvent = null;
                foreach (var ev in track.Events)
                {
                    var fEvent = ev as FTweenPositionEvent;
                    var xce = new XCMoveEventData();
                    SaveSequenceEventConverters.SetLineEventTween_Ex(xce, fEvent);
                    if (lastEvent != null)
                        xce.StartDetal = xce.StartVec - lastEvent.Tween.To;
                    res.MoveEvents.Events.Add(xce);
                    lastEvent = fEvent;
                }
            }
            else if (trackType == typeof(FTweenScaleEvent))
            {
                foreach (var ev in track.Events)
                {
                    var fEvent = ev as FTweenScaleEvent;
                    var xce = new XCScaleEventData();
                    SaveSequenceEventConverters.SetLineEventTween(xce, fEvent);
                    res.ScaleEvents.Events.Add(xce);
                }
            }
            else if (trackType == typeof(FTweenRotationEvent))
            {
                foreach (var ev in track.Events)
                {
                    var fEvent = ev as FTweenRotationEvent;
                    var xce = new XCRotateEventData();
                    SaveSequenceEventConverters.SetLineEventTween(xce, fEvent);
                    res.RotateEvents.Events.Add(xce);
                }
            }
            else if (trackType == typeof(FPlayParticleEvent))
            {
                if (track.Events.Count == 1)
                {
                    var fEvent = track.Events[0] as FPlayParticleEvent;
                    var xce = SaveSequenceEventConverters.TOXCObjEvent(fEvent, playerTF);
                    xce.IsEffect = true;
                    if (res.ObjEvent != null)
                        Debug.LogError($"ObjEvent已赋值，请检查轨道设置{trackType}");
                    res.ObjEvent = xce;
                }
                else
                {
                    Debug.LogError($"yns _track.Events.Count? {track.Events.Count}");
                }
            }
            else if (trackType == typeof(FObjectEvent))
            {
                if (track.Events.Count == 1)
                {
                    var fEvent = track.Events[0] as FObjectEvent;
                    var xce = SaveSequenceEventConverters.TOXCObjEvent(fEvent, playerTF);
                    if (res.ObjEvent != null)
                        Debug.LogError($"ObjEvent已赋值，请检查轨道设置{trackType}");
                    res.ObjEvent = xce;
                }
                else
                {
                    Debug.LogError($"yns _track.Events.Count? {track.Events.Count}");
                }
            }
            else if (trackType == typeof(FSwitchEvent))
            {
                foreach (var ev in track.Events)
                    res.SwitchEvents.Events.Add(SaveSequenceEventConverters.TOXCSwitchEvent(ev as FSwitchEvent));
            }
            else if (trackType == typeof(FPlayMsgEvent))
            {
                foreach (var ev in track.Events)
                    res.MsgEvents.Events.Add(SaveSequenceEventConverters.TOXCMsgEvent(ev as FPlayMsgEvent));
            }
            else if (trackType == typeof(FTriggerRangeEvent))
            {
                foreach (var ev in track.Events)
                    res.TriggerEvents.Events.Add(SaveSequenceEventConverters.TOXCTriggerEvent(ev as FTriggerRangeEvent));
            }
            else if (trackType == typeof(FSkillInputEvent))
            {
                foreach (var ev in track.Events)
                    res.SkillInputEvents.Events.Add(SaveSequenceEventConverters.TOXCSkillInputEvent(ev as FSkillInputEvent));
            }
            else if (trackType == typeof(FPlayTagEvent))
            {
                foreach (var ev in track.Events)
                    res.EffectEvents.Events.Add(SaveSequenceEventConverters.TOXCEffectEvent(ev as FPlayTagEvent));
            }
            else
            {
                unknownEnabledTypes.Add(trackType != null ? trackType.Name : "null");
            }
        }
    }
}
