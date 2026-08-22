
using ACTGameEditor;
using ACTGameEditor;
using Flux;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using XiaoCao;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace FluxEditor
{
    public class SaveSequenceData
    {
        public static AgentModelType curAgentName;
        private static List<FPlayAnimationEvent> tmpSeqFAnimEvents = new List<FPlayAnimationEvent>();

        public static void GetData()
        {
            TrySaveCurrentSequence();
        }

        /// <summary>
        /// 校验当前窗口 Sequence 后导出。失败弹窗；成功只还原场景 Animator，不改工程 Controller。
        /// </summary>
        public static bool TrySaveCurrentSequence()
        {
            if (FSequenceEditorWindow.instance == null)
            {
                EditorUtility.DisplayDialog("保存失败", "Flux 窗口未打开。", "确定");
                return false;
            }

            FSequence sequence = FSequenceEditorWindow.instance.GetSequenceEditor()?.Sequence;
            if (!TryValidateSequence(sequence, out string error))
            {
                EditorUtility.DisplayDialog("保存失败", error, "确定");
                return false;
            }

            if (!SavaOneSeq(sequence))
                return false;

            RestorePreviewAnimators(sequence);
            AssetDatabase.SaveAssets();
            return true;
        }

        /// <summary>
        /// 导出前检查 SkillId、FSeqSetting 和可导出的 Timeline。
        /// </summary>
        public static bool TryValidateSequence(FSequence sequence, out string error)
        {
            error = null;
            if (sequence == null)
            {
                error = "没有打开的 Sequence。";
                return false;
            }

            if (sequence.FSeqSetting == null)
            {
                error = $"Sequence \"{sequence.name}\" 未配置 FSeqSetting。";
                return false;
            }

            if (string.IsNullOrWhiteSpace(sequence.SkillId))
            {
                error = $"Sequence \"{sequence.name}\" 的 SkillId 为空。";
                return false;
            }

            if (!int.TryParse(sequence.SkillId, out _))
            {
                error = $"Sequence \"{sequence.name}\" 的 SkillId \"{sequence.SkillId}\" 不是整数。";
                return false;
            }

            if (sequence.Containers == null || sequence.Containers.Count == 0
                || sequence.Containers[0] == null || sequence.Containers[0].Timelines.Count == 0)
            {
                error = $"Sequence \"{sequence.name}\" 没有可导出的 Timeline（目前只导出第一个 Container）。";
                return false;
            }

            return true;
        }

        /// <summary>
        /// 保存后把场景 Animator 从预览副本还原到工程 Controller，不改 man_editor.controller。
        /// </summary>
        public static void RestorePreviewAnimators(FSequence sequence)
        {
            FAnimationTrackInspector.RestorePreviewAnimators(sequence);
        }


        [MenuItem("GameObject/XiaoCao/保存选中Seq技能", priority = 0)]
        private static void SavaSelectSeq()
        {
            foreach (var item in Selection.objects)
            {
                var seq = (item as GameObject)?.GetComponent<FSequence>();
                if (seq == null)
                    continue;

                if (!TryValidateSequence(seq, out string error))
                {
                    EditorUtility.DisplayDialog("保存失败", error, "确定");
                    continue;
                }

                SavaOneSeq(seq);
                RestorePreviewAnimators(seq);
            }
            AssetDatabase.SaveAssets();
        }

        [MenuItem("GameObject/XiaoCao/选中预制体", priority = 0)]
        private static void SelectPrefab()
        {
            List<Object> objs = new List<Object>();
            foreach (var item in Selection.objects)
            {
                var path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(item);
                var obj = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (obj != null)
                {
                    objs.Add(obj);
                }
            }
            Selection.objects = objs.ToArray();
        }

        [MenuItem("GameObject/XiaoCao/收集选中Seq技能Anim", priority = 0)]
        private static void MoveNeedAnim()
        {
            foreach (var item in Selection.objects)
            {
                var seq = (item as GameObject).GetComponent<FSequence>();
                if (seq)
                {
                    CheckMoveAnim(seq);
                }
            }
            AssetDatabase.SaveAssets();     //保存改动的资源
            AssetDatabase.Refresh();
        }

        private static void CheckMoveAnim(FSequence sequence)
        {
            AnimatorController ac = sequence.FSeqSetting.targetAnimtorController as AnimatorController;
            tmpSeqFAnimEvents.Clear();
            var _track = sequence.Containers[0].Timelines[0].Tracks[0];

            if (_track is FAnimationTrack)
            {
                foreach (var ev in _track.Events)
                {
                    var fEvent = ev as FPlayAnimationEvent;
                    tmpSeqFAnimEvents.Add(fEvent);
                }
            }

            foreach (var item in tmpSeqFAnimEvents)
            {
                string path = AssetDatabase.GetAssetPath(item._animationClip);
                string newPath = "Assets/_Res/Anim/Using/" + Path.GetFileName(path);
                AssetDatabase.MoveAsset(path, newPath);
            }
        }

        private static bool SavaOneSeq(FSequence Sequence)
        {
            tmpSeqFAnimEvents.Clear();
            if (!SavaSequnce(Sequence, Sequence.SkillId))
                return false;

            CheckAddAnim(Sequence);
            return true;
        }

        private static void CheckAddAnim(FSequence sequence)
        {
            AnimatorController ac = sequence.FSeqSetting.targetAnimtorController as AnimatorController;
            if (ac == null)
            {
                Debug.LogWarning($"FSeqSetting.targetAnimtorController 为空，已跳过写入 Animator。Sequence={sequence.name}");
                return;
            }

            bool ischage = false;

            Dictionary<string, float> otherSpeedDic = new Dictionary<string, float>();
            AnimatorStateMachine sm = ac.layers[0].stateMachine;

            foreach (var item in tmpSeqFAnimEvents)
            {
                if (!ac.animationClips.Contains(item._animationClip))
                {
                    AnimatorState state = sm.AddState(item._animationClip.name, sm.exitPosition + Vector3.up * Random.Range(100, 800));
                    //state.name = item.name;
                    state.motion = item._animationClip;
                    state.speed = item._speed;
                    if (state.tag == "NoExit")
                    {

                    }
                    else
                    {
                        //添加转出过渡
                        state.AddExitTransition(true);
                    }

                    ischage = true;
                    Debug.Log($"anim add {item._animationClip.name}");
                }
                else
                {
                    //检查state速度
                    otherSpeedDic[item._animationClip.name] = item._speed;
                }
            }

            foreach (var item in sm.states)
            {
                if (otherSpeedDic.ContainsKey(item.state.name))
                {
                    var Speed = otherSpeedDic[item.state.name];
                    if (!Mathf.Approximately(item.state.speed, Speed))
                    {
                        //Debug.Log("Speed Change");
                        item.state.speed = Speed;
                        ischage = true;
                    }
                }
            }

            if (ischage)
            {
                //Debug.Log($"anim controller Sava  ");
                //AssetDatabase.ForceReserializeAssets(new[] { path });
                EditorUtility.SetDirty(ac);
            }
        }


        //[MenuItem("Assets/AnimatorTool/Log选择中技能的动画名")]
        //private static void CheckSkillDataAnim()
        //{
        //    List<SkillEventData> list = new List<SkillEventData>();
        //    foreach (var item in Selection.objects)
        //    {
        //        var date = (item as GameObject).GetComponent<SkillEventData>();
        //        if (date != null)
        //        {
        //            list.Add(date);
        //        }
        //    }
        //    HashSet<string> nameSet = new HashSet<string>();
        //    foreach (var item in list)
        //    {
        //        foreach (var sub in item.AnimEvents.Events)
        //        {
        //            if (!nameSet.Contains(sub.eName))
        //            {
        //                nameSet.Add(sub.eName);
        //            }
        //        }
        //    }
        //    nameSet.IELogStr("动画");
        //}

        /// <summary>预览轨：不导出运行时，也不视为未知失败。</summary>
        static readonly HashSet<Type> IgnoredPreviewEventTypes = new HashSet<Type>
        {
            typeof(FCommentEvent),
            typeof(FPlayAudioEvent),
            typeof(FVolumeAudioEvent),
            typeof(FGlobalVolumeAudioEvent),
            typeof(FTimescaleEvent),
            typeof(FCameraManagerEvent),
        };

        private static bool SavaSequnce(FSequence Sequence, string SkillId)
        {
            float Speed = Sequence.Speed;
            int objIndex = 0;
            Transform playerTF = Sequence.Containers[0].Timelines[0]._owner;

            int sort = 0;
            var unknownEnabledTypes = new List<string>();
            var skippedPreviewTypes = new HashSet<string>();
            int SkillInt = int.Parse(SkillId);

            List<SkillNewEventData> skillEvents = new List<SkillNewEventData>();
            foreach (var _timeline in Sequence.Containers[0].Timelines)
            {
                string SkillName = objIndex > 0 ? SkillId + "_" + objIndex : SkillId;
                SkillNewEventData skillData = new SkillNewEventData();
                skillData.SkillName = SkillName;
                skillData.SkillId = SkillInt;
                skillData.Speed = Speed;
                skillData.SkillSort = sort;
                foreach (var _track in _timeline.Tracks)
                {
                    if (!_track.enabled)
                        continue;
                    TryReadTrack(skillData, _track, playerTF, unknownEnabledTypes, skippedPreviewTypes);
                }
                skillEvents.Add(skillData);
                objIndex++;
                sort++;
            }

            if (skippedPreviewTypes.Count > 0)
                Debug.Log($"[SaveSequenceData] 跳过预览轨 skillId={SkillId}: {string.Join(", ", skippedPreviewTypes)}");

            if (unknownEnabledTypes.Count > 0)
            {
                string unknown = string.Join("\n", unknownEnabledTypes);
                Debug.LogError($"[SaveSequenceData] 未映射的启用轨道，已中止保存 skillId={SkillId}: {unknown}");
                EditorUtility.DisplayDialog("保存失败", $"存在未映射的启用轨道，已中止保存：\n{unknown}", "确定");
                return false;
            }

            foreach(SkillNewEventData eventData in skillEvents)
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

            curAgentName = Sequence.FSeqSetting.agentName;
            string resDir = "Game/Config/" + curAgentName.GetSkillPath();
            FileTool.CheckDirOrCreat(Application.dataPath + "/" + resDir);

            string prefabPath = "Assets/" + resDir + SkillId + ".asset";
            SkillAllEventData data = AssetDatabase.LoadAssetAtPath<SkillAllEventData>(prefabPath);
            bool isNew = data == null;
            if (isNew)
                data = ScriptableObject.CreateInstance<SkillAllEventData>();

            data.SkillId = SkillInt;
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

        ////获取已保存的动画名
        //public static List<string> GetAllAnimName()
        //{
        //    var skills = Resources.LoadAll<SkillEventData>("SkillData");
        //    List<string> nameList = new List<string>();
        //    foreach (var item in skills)
        //    {
        //        foreach (var anim in item.AnimEvents.Events)
        //        {
        //            if (!nameList.Contains(anim.eName))
        //            {
        //                nameList.Add(anim.eName);
        //            }
        //        }
        //    }
        //    string[] AddNameList = { AnimNameStr.RollTree, AnimNameStr.Break }; //额外添加
        //    foreach (var item in AddNameList)
        //    {
        //        if (!nameList.Contains(item))
        //        {
        //            nameList.Add(item); //额外添加
        //        }
        //    }
        //    nameList.IELogStr();
        //    return nameList;
        //}

        //playerTF 作为参考坐标。同类型多轨追加，不再每轨 Clear。
        private static void TryReadTrack(SkillNewEventData res, FTrack _track, Transform playerTF, List<string> unknownEnabledTypes, HashSet<string> skippedPreviewTypes)
        {
            var trackType = _track.GetEventType();
            if (IgnoredPreviewEventTypes.Contains(trackType))
            {
                skippedPreviewTypes.Add(trackType != null ? trackType.Name : "null");
                return;
            }

            if (trackType == typeof(FPlayAnimationEvent))
            {
                foreach (var ev in _track.Events)
                {
                    var fEvent = ev as FPlayAnimationEvent;
                    tmpSeqFAnimEvents.Add(fEvent);
                    var xce = ToXCAnimEvent(fEvent);
                    res.AnimEvents.Events.Add(xce);
                }
            }
            else if (trackType == typeof(FTweenPositionEvent))
            {
                FTweenPositionEvent lastEvent = null;
                foreach (var ev in _track.Events)
                {
                    FTweenPositionEvent fEvent = ev as FTweenPositionEvent;
                    var xce = new XCMoveEventData();
                    SetLineEventTween_Ex(xce, fEvent);
                    if (lastEvent != null)
                    {
                        xce.StartDetal = xce.StartVec - lastEvent.Tween.To;
                    }
                    res.MoveEvents.Events.Add(xce);
                    lastEvent = fEvent;
                }
            }
            else if (trackType == typeof(FTweenScaleEvent))
            {
                foreach (var ev in _track.Events)
                {
                    var fEvent = ev as FTweenScaleEvent;
                    var xce = new XCScaleEventData();
                    SetLineEventTween(xce, fEvent);
                    res.ScaleEvents.Events.Add(xce);
                }
            }
            else if (trackType == typeof(FTweenRotationEvent))
            {
                foreach (var ev in _track.Events)
                {
                    var fEvent = ev as FTweenRotationEvent;
                    var xce = new XCRotateEventData();
                    SetLineEventTween(xce, fEvent);
                    res.RotateEvents.Events.Add(xce);
                }
            }
            else if (trackType == typeof(FPlayParticleEvent))
            {
                if (_track.Events.Count == 1)
                {
                    var fEvent = _track.Events[0] as FPlayParticleEvent;
                    var xce = TOXCObjEvent(fEvent, playerTF);
                    xce.IsEffect = true;
                    if(res.ObjEvent != null)
                    {
                        Debug.LogError($"ObjEvent已赋值，请检查轨道设置{trackType}");
                    }
                    res.ObjEvent = xce;
                }
                else
                {
                    Debug.LogError($"yns _track.Events.Count? {_track.Events.Count}");
                }
            }
            else if (trackType == typeof(FObjectEvent))
            {
                if (_track.Events.Count == 1)
                {
                    var fEvent = _track.Events[0] as FObjectEvent;
                    var xce = TOXCObjEvent(fEvent, playerTF);
                    if (res.ObjEvent != null)
                    {
                        Debug.LogError($"ObjEvent已赋值，请检查轨道设置{trackType}");
                    }
                    res.ObjEvent = xce;
                }
                else
                {
                    Debug.LogError($"yns _track.Events.Count? {_track.Events.Count}");
                }
            }
            else if (trackType == typeof(FSwitchEvent))
            {
                foreach (var ev in _track.Events)
                {
                    var fEvent = ev as FSwitchEvent;
                    var xce = TOXCSwitchEvent(fEvent);
                    res.SwitchEvents.Events.Add(xce);
                }
            }
            else if (trackType == typeof(FPlayMsgEvent))
            {
                foreach (var ev in _track.Events)
                {
                    var fEvent = ev as FPlayMsgEvent;
                    var xce = TOXCMsgEvent(fEvent);
                    res.MsgEvents.Events.Add(xce);
                }
            }
            else if (trackType == typeof(FTriggerRangeEvent))
            {
                foreach (var ev in _track.Events)
                {
                    var fEvent = ev as FTriggerRangeEvent;
                    var xce = TOXCTriggerEvent(fEvent);
                    res.TriggerEvents.Events.Add(xce);
                }
            }
            else if (trackType == typeof(FSkillInputEvent))
            {
                foreach (var ev in _track.Events)
                {
                    var fEvent = ev as FSkillInputEvent;
                    var xce = TOXCSkillInputEvent(fEvent);
                    res.SkillInputEvents.Events.Add(xce);
                }
            }
            else if (trackType == typeof(FPlayTagEvent))
            {
                foreach (var ev in _track.Events)
                {
                    var fEvent = ev as FPlayTagEvent;
                    var xce = TOXCEffectEvent(fEvent);
                    res.EffectEvents.Events.Add(xce);
                }
            }
            else
            {
                unknownEnabledTypes.Add(trackType != null ? trackType.Name : "null");
            }
        }

        public static XCEffectEventData TOXCEffectEvent(FPlayTagEvent fe)
        {
            var xce = new XCEffectEventData();
            xce.Range = new XCRange(fe.Start, fe.End);
            xce.IsLocalTrueOnly = fe.isLocalTrueOnly;
            xce.NormalEffectIds = fe.NormalEffectIds ?? new List<int>();
            xce.SkillEffectIds = fe.SkillEffectIds ?? new List<int>();
            xce.SkillTagList = fe.SkillTagList ?? new List<string>();
            return xce;
        }

        public static XCSkillInputEventData TOXCSkillInputEvent(FSkillInputEvent fe)
        {
            var xce = new XCSkillInputEventData();
            xce.Range = new XCRange(fe.Start, fe.End);
            xce.IsLocalTrueOnly = fe.isLocalTrueOnly;
            //排序一下
            fe.InputList.Sort((a, b) => b.SkillSort.CompareTo(a.SkillSort));
            List<ACTGameEditor.SkillInputData> data = new List<ACTGameEditor.SkillInputData>(fe.InputList.Count);
            for (int i = 0; i < fe.InputList.Count; i++)
            {
                data.Add(new ACTGameEditor.SkillInputData()
                {
                    ListernType = fe.InputList[i].ListernType,
                    PressType = fe.InputList[i].PressType,
                    InputCallBackType = fe.InputList[i].InputCallBackType,
                    SkillId = fe.InputList[i].SkillId,
                    SkillSort = (int)fe.InputList[i].SkillSort + fe.InputList[i].Offset,
                    RequiredTags = fe.InputList[i].RequiredTags,
                    BlockedTags = fe.InputList[i].BlockedTags,
                    InputTimeout = fe.InputList[i].InputTimeout,
                });
            }
            xce.InputDataList = data;
            return xce;
        }

        /// <summary>从 XiaoCao.SkillInputData 反转为 Flux.SkillInputData，用于从 Asset 加载回 FSequence</summary>
        static Flux.SkillInputData ToFluxSkillInputData(ACTGameEditor.SkillInputData data)
        {
            var skillSorts = (EGamePlay.Combat.SkillSort[])Enum.GetValues(typeof(EGamePlay.Combat.SkillSort));
            int baseVal = (int)EGamePlay.Combat.SkillSort.Normal;
            for (int i = skillSorts.Length - 1; i >= 0; i--)
            {
                int v = (int)skillSorts[i];
                if (data.SkillSort >= v) { baseVal = v; break; }
            }
            return new Flux.SkillInputData
            {
                ListernType = data.ListernType,
                PressType = data.PressType,
                InputCallBackType = data.InputCallBackType,
                SkillId = data.SkillId,
                SkillSort = (EGamePlay.Combat.SkillSort)baseVal,
                Offset = data.SkillSort - baseVal,
                RequiredTags = data.RequiredTags,
                BlockedTags = data.BlockedTags,
                InputTimeout = data.InputTimeout,
            };
        }

        /// <summary>从 XCSkillInputEventData 反转为 FSkillInputEvent，用于从 Asset 加载回 FSequence</summary>
        public static FSkillInputEvent TOFSkillInputEvent(XCSkillInputEventData xce)
        {
            var fe = FEvent.Create<FSkillInputEvent>(new FrameRange(xce.Range.Start, xce.Range.End));
            fe.isLocalTrueOnly = xce.IsLocalTrueOnly;
            fe.InputList = new List<Flux.SkillInputData>(xce.InputDataList.Count);
            for (int i = 0; i < xce.InputDataList.Count; i++)
                fe.InputList.Add(ToFluxSkillInputData(xce.InputDataList[i]));
            return fe;
        }

        /// <summary>从 SkillAllEventData 将 Input / Trigger / Effect 同步回 FSequence，不重建 Anim / Obj。</summary>
        [MenuItem("GameObject/XiaoCao/从 Asset 同步 Input/Trigger/Effect", priority = 1)]
        private static void LoadCombatEventsFromAsset()
        {
            var seq = Selection.activeGameObject?.GetComponent<FSequence>();
            if (seq == null)
            {
                Debug.LogWarning("请先选中包含 FSequence 的 GameObject。");
                return;
            }
            if (seq.FSeqSetting == null || string.IsNullOrEmpty(seq.SkillId))
            {
                Debug.LogWarning("FSequence 需配置 FSeqSetting 和 SkillId。");
                return;
            }
            curAgentName = seq.FSeqSetting.agentName;
            string resDir = "Assets/Game/Config/" + curAgentName.GetSkillPath() + seq.SkillId + ".asset";
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
                inputCount += SyncTrackEvents(timeline, skillEventData.SkillInputEvents?.Events, TOFSkillInputEvent);
                triggerCount += SyncTrackEvents(timeline, skillEventData.TriggerEvents?.Events, TOFTriggerEvent);
                effectCount += SyncTrackEvents(timeline, skillEventData.EffectEvents?.Events, TOFEffectEvent);
            }
            EditorUtility.SetDirty(seq);
            Debug.Log($"已从 Asset 同步 Input={inputCount} Trigger={triggerCount} Effect={effectCount}（SchemaVersion={skillData.SchemaVersion}）。");
        }

        /// <summary>将 SO 事件列表灌到 Timeline 上第一条匹配类型轨；没有则新建。空列表不改现有轨。</summary>
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

        /// <summary>Flux 触发轨 → 运行时数据，成对拷贝盒体、段索引、HitGroupId、EffectIds。</summary>
        public static XCTriggerEventData TOXCTriggerEvent(FTriggerRangeEvent fe)
        {
            var xce = new XCTriggerEventData();
            xce.Range = new XCRange(fe.Start, fe.End);
            xce.IsLocalTrueOnly = fe.isLocalTrueOnly;
            xce.CubeRange = CopyCubeRange(fe.cubeRange);
            xce.DamageSegmentIndex = fe.DamageSegmentIndex;
            xce.HitGroupId = fe.HitGroupId;
            xce.EffectIds = fe.EffectIds ?? new List<int>();
            return xce;
        }

        /// <summary>从 XCTriggerEventData 反转为 FTriggerRangeEvent，用于从 Asset 灌回 Sequence。</summary>
        public static FTriggerRangeEvent TOFTriggerEvent(XCTriggerEventData xce)
        {
            var fe = FEvent.Create<FTriggerRangeEvent>(new FrameRange(xce.Range.Start, xce.Range.End));
            fe.isLocalTrueOnly = xce.IsLocalTrueOnly;
            fe.cubeRange = CopyCubeRange(xce.CubeRange);
            fe.DamageSegmentIndex = xce.DamageSegmentIndex;
            fe.HitGroupId = xce.HitGroupId;
            fe.EffectIds = xce.EffectIds != null ? new List<int>(xce.EffectIds) : new List<int>();
            return fe;
        }

        /// <summary>从 XCEffectEventData 反转为 FPlayTagEvent，用于从 Asset 灌回 Sequence。</summary>
        public static FPlayTagEvent TOFEffectEvent(XCEffectEventData xce)
        {
            var fe = FEvent.Create<FPlayTagEvent>(new FrameRange(xce.Range.Start, xce.Range.End));
            fe.isLocalTrueOnly = xce.IsLocalTrueOnly;
            fe.SkillTagList = xce.SkillTagList != null ? new List<string>(xce.SkillTagList) : new List<string>();
            fe.NormalEffectIds = xce.NormalEffectIds != null ? new List<int>(xce.NormalEffectIds) : new List<int>();
            fe.SkillEffectIds = xce.SkillEffectIds != null ? new List<int>(xce.SkillEffectIds) : new List<int>();
            return fe;
        }

        static EGamePlay.Combat.CubeRange CopyCubeRange(EGamePlay.Combat.CubeRange src)
        {
            var range = new EGamePlay.Combat.CubeRange();
            if (src == null)
                return range;
            range.pos = src.pos;
            range.rotation = src.rotation;
            range.size = src.size;
            range.radius = src.radius;
            range.height = src.height;
            range.colliderType = src.colliderType;
            return range;
        }

        public static XCSwitchEventData TOXCSwitchEvent(FSwitchEvent fe)
        {
            var xce = new XCSwitchEventData();
            xce.Range = new XCRange(fe.Start, fe.End);
            xce.IsLocalTrueOnly = fe.isLocalTrueOnly;
            //xce.ToFrame = fe.ToFrame;
            //xce.UnMoveTime = fe.UnMoveFrames * XCSetting.FramePerSec;
            xce.InputType = fe.InputType;
            //xce.keyCode = fe.keyCode;
            return xce;
        }

        public static XCMsgEventData TOXCMsgEvent(FPlayMsgEvent fe)
        {
            var xce = new XCMsgEventData();
            xce.IsLocalTrueOnly = fe.isLocalTrueOnly;
            xce.Range = new XCRange(fe.Start, fe.End);
            xce.MsgEType = fe.msgType;
            xce.MsgName = fe.msgName.ToString();
            xce.BoolMsg = fe.boolMsg;
            xce.SetOppositeOnFinish = fe.setOppositeOnFinish;
            xce.StrMsg = fe.strMsg;
            xce.FloatdMsg = fe.floatMsg;
            return xce;
        }

        public static XCObjEventData TOXCObjEvent(FEvent fe, Transform playerTF)
        {
            XCObjEventData xce = new XCObjEventData();
            xce.Range = new XCRange(fe.Start, fe.End);
            xce.IsLocalTrueOnly = fe.isLocalTrueOnly;
            string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(fe.Owner);
            if (path == "")
            {
                Debug.LogError("yns  path null ");
                path = "SkillEffet/Using/Sword_1";
            }

            xce.BundlePath = PrefabABPathPostprocessor.GenerateBundleNameFromFolder(path) + "_prefab";
            xce.AssetPath = Path.GetFileNameWithoutExtension(Path.GetFileName(path));
            SetObjectEventTF(fe, playerTF, xce);
            return xce;
        }

        //static (string directoryPath, string fileName) SplitPathUsingPathClass(string fullPath)
        //{
        //    if (string.IsNullOrWhiteSpace(fullPath))
        //        return (string.Empty, string.Empty);

        //    try
        //    {
        //        // 获取文件名（包含扩展名）
        //        string fileName = Path.GetFileName(fullPath);

        //        // 获取目录路径
        //        string directoryPath = Path.GetDirectoryName(fullPath);

        //        // 处理可能的null值
        //        directoryPath = directoryPath ?? string.Empty;

        //        return (directoryPath, fileName);
        //    }
        //    catch (ArgumentException ex)
        //    {
        //        Console.WriteLine($"路径包含无效字符: {ex.Message}");
        //    }
        //    return (string.Empty, string.Empty);
        //}

        private static void SetObjectEventTF(FEvent fe, Transform playerTF, XCObjEventData xce)
        {
            xce.TransfromType = fe.Track.Timeline.transfromType;
            xce.StartPos = fe.Owner.transform.position - playerTF.transform.position;
            xce.StartRotation = fe.Owner.transform.localEulerAngles;
            xce.StartScale = fe.Owner.transform.localScale;
            if (fe.Owner.transform.parent == playerTF)
            {
                xce.StartScale /= playerTF.localScale.x; //player尽量不要有缩放
            }
        }

        public static XCAnimEventData ToXCAnimEvent(FPlayAnimationEvent fe)
        {
            var xce = new XCAnimEventData();
            xce.Range = new XCRange(fe.Start, fe.End);
            xce.IsLocalTrueOnly = fe.isLocalTrueOnly;
            xce.StartOffset = fe._startOffset * XCSetting.FramePerSec;
            xce.BlenderLength = fe._blendLength * XCSetting.FramePerSec;
            xce.IsBackToIdle = fe.isBackToIdle;
            xce.ExitPolicy = fe.ExitPolicy;
            xce.AnimName = fe._animationClip.name;
            return xce;
        }

        public static void SetLineEventTween(XCLineEventData xce, FTweenEvent<FTweenVector3> fe)
        {
            xce.Range = new XCRange(fe.Start, fe.End);
            xce.StartVec = fe.Tween.From;
            xce.IsLocalTrueOnly = fe.isLocalTrueOnly;
            xce.EndVec = fe.Tween.To;
            xce.EaseType = fe.Tween.EasingType.ToDotweenEase();
        }

        public static void SetLineEventTween_Ex(XCMoveEventData xce, FTweenEvent<FTweenVector3_Ex> fe)
        {
            xce.Range = new XCRange(fe.Start, fe.End);
            xce.StartVec = fe.Tween.From;
            xce.IsLocalTrueOnly = fe.isLocalTrueOnly;
            xce.EndVec = fe.Tween.To;
            xce.EaseType = fe.Tween.EasingType.ToDotweenEase();

            xce.IsBezier = fe.Tween.isBezier;
            xce.HandlePoint = fe.Tween.HandlePoint;
            xce.LookForward = fe.Tween.lookForward;
        }
    }

}
