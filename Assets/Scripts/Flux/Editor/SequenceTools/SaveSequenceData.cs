using EGamePlay;
using EGamePlay.Combat;
using Flux;
using SimpleJSON;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FluxEditor
{
    /// <summary>Flux Sequence 导出入口：校验、保存 SkillAllEventData、写回 Sequence 预制体、编辑器菜单。</summary>
    public class SaveSequenceData
    {
        public const string SequencePrefabFolder = "Assets/Editor/SkillSequences";

        public static AgentModelType curAgentName;

        public static void GetData()
        {
            TrySaveCurrentSequence();
        }

        /// <summary>
        /// 校验当前窗口 Sequence 后导出 SO，并写回 Sequence 预制体。
        /// 失败弹窗；成功会先还原预览 Animator，再存预制体，避免把预览 Controller 打进源。
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

            if (!SaveOneSeq(sequence))
                return false;

            RestorePreviewAnimators(sequence);
            if (!TrySaveSequencePrefab(sequence, out string prefabError))
            {
                EditorUtility.DisplayDialog(
                    "预制体未保存",
                    "技能数据已导出，但 Sequence 预制体没有写回：\n" + prefabError,
                    "确定");
                return false;
            }

            AssetDatabase.SaveAssets();
            return true;
        }

        /// <summary>导出前检查 SkillId、FSeqSetting、可导出 Timeline，以及判定盒段号与段表行。</summary>
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

            if (!int.TryParse(sequence.SkillId, out int skillId))
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

            return TryValidateTriggerDamageSegments(sequence, skillId, out error);
        }

        /// <summary>
        /// 校验第一个 Container 上每个判定盒：段号 ≥ 1，且 SkillDamage 表有对应行。
        /// 无判定盒（如闪避）直接通过。编辑器不依赖运行时 <c>SkillSettingMgr</c>。
        /// </summary>
        static bool TryValidateTriggerDamageSegments(FSequence sequence, int skillId, out string error)
        {
            error = null;
            var timelines = sequence.Containers[0].Timelines;
            SkillDamageReader reader = null;

            for (int t = 0; t < timelines.Count; t++)
            {
                var timeline = timelines[t];
                if (timeline == null || timeline.Tracks == null)
                    continue;

                for (int k = 0; k < timeline.Tracks.Count; k++)
                {
                    var track = timeline.Tracks[k];
                    if (track == null || track.Events == null)
                        continue;

                    for (int i = 0; i < track.Events.Count; i++)
                    {
                        var te = track.Events[i] as FTriggerRangeEvent;
                        if (te == null)
                            continue;

                        if (te.DamageSegmentIndex <= 0)
                        {
                            error = $"Sequence \"{sequence.name}\" Trigger [{te.Start}-{te.End}] 段号必须 ≥ 1，当前 {te.DamageSegmentIndex}。";
                            return false;
                        }

                        if (reader == null && !TryLoadSkillDamageReader(out reader, out error))
                            return false;

                        if (reader.Get(skillId, te.DamageSegmentIndex) == null)
                        {
                            error = $"Sequence \"{sequence.name}\" Trigger [{te.Start}-{te.End}] 在 SkillDamage 表没有 (SkillId={skillId}, Segment={te.DamageSegmentIndex})。";
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        /// <summary>从 Resources 解析段表；不走 <c>SkillSettingMgr.Instance</c>（编辑器非 Play 时为 null）。</summary>
        static bool TryLoadSkillDamageReader(out SkillDamageReader reader, out string error)
        {
            reader = null;
            error = null;
            var text = Resources.Load<TextAsset>("Config/Luban/skilldamagereader");
            if (text == null || string.IsNullOrEmpty(text.text))
            {
                error = "找不到 Resources/Config/Luban/skilldamagereader，无法校验段表。请先生成 Luban。";
                return false;
            }

            try
            {
                reader = new SkillDamageReader(JSON.Parse(text.text));
                return true;
            }
            catch (System.Exception e)
            {
                error = $"解析 skilldamagereader 失败：{e.Message}";
                return false;
            }
        }

        /// <summary>保存后把场景 Animator 从预览副本还原到工程 Controller，不改 man_editor.controller。</summary>
        public static void RestorePreviewAnimators(FSequence sequence)
        {
            FAnimationTrackInspector.RestorePreviewAnimators(sequence);
        }

        [MenuItem("GameObject/Flux/保存选中Seq技能", priority = 0)]
        static void SaveSelectSeq()
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

                if (!SaveOneSeq(seq))
                    continue;

                RestorePreviewAnimators(seq);
                if (!TrySaveSequencePrefab(seq, out string prefabError))
                {
                    EditorUtility.DisplayDialog(
                        "预制体未保存",
                        $"技能 {seq.SkillId} 数据已导出，但 Sequence 预制体没有写回：\n{prefabError}",
                        "确定");
                }
            }
            AssetDatabase.SaveAssets();
        }

        [MenuItem("GameObject/Flux/选中预制体", priority = 0)]
        static void SelectPrefab()
        {
            var objs = new List<Object>();
            foreach (var item in Selection.objects)
            {
                var path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(item);
                var obj = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (obj != null)
                    objs.Add(obj);
            }
            Selection.objects = objs.ToArray();
        }

        [MenuItem("GameObject/Flux/收集选中Seq技能Anim", priority = 0)]
        static void MoveNeedAnim()
        {
            foreach (var item in Selection.objects)
            {
                var seq = (item as GameObject)?.GetComponent<FSequence>();
                if (seq)
                    SaveSequenceAnimExporter.CheckMoveAnim(seq);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [MenuItem("GameObject/Flux/从 Asset 同步 Input/Trigger/Effect", priority = 1)]
        static void LoadCombatEventsFromAsset()
        {
            var seq = Selection.activeGameObject?.GetComponent<FSequence>();
            if (seq == null)
            {
                Debug.LogWarning("请先选中包含 FSequence 的 GameObject。");
                return;
            }

            curAgentName = seq.FSeqSetting.agentName;
            SaveSequenceAssetSync.LoadCombatEventsFromAsset(seq, curAgentName);
        }

        static bool SaveOneSeq(FSequence sequence)
        {
            SaveSequenceAnimExporter.Clear();
            curAgentName = sequence.FSeqSetting.agentName;
            if (!SaveSequenceTrackExporter.Export(sequence, sequence.SkillId, curAgentName))
                return false;

            SaveSequenceAnimExporter.CheckAddAnim(sequence);
            return true;
        }

        /// <summary>规范路径：<c>Assets/Editor/SkillSequences/{SkillId}.prefab</c>。</summary>
        public static string GetCanonicalSequencePrefabPath(string skillId)
        {
            return SequencePrefabFolder + "/" + skillId + ".prefab";
        }

        /// <summary>
        /// 把当前编辑的 Sequence 写回预制体。须在 <see cref="RestorePreviewAnimators"/> 之后调用。
        /// Prefab 模式存正在编的资产；场景实例 Apply 到源；SkillId 与源路径不一致时另存为规范路径，避免误覆盖其它技能。
        /// </summary>
        public static bool TrySaveSequencePrefab(FSequence sequence, out string error)
        {
            error = null;
            if (sequence == null)
            {
                error = "没有打开的 Sequence。";
                return false;
            }

            GameObject go = sequence.gameObject;
            if (go == null)
            {
                error = "Sequence 没有 GameObject。";
                return false;
            }

            EditorUtility.SetDirty(sequence);
            EditorUtility.SetDirty(go);

            try
            {
                PrefabStage stage = PrefabStageUtility.GetPrefabStage(go);
                if (stage != null && !string.IsNullOrEmpty(stage.assetPath))
                {
                    PrefabUtility.SaveAsPrefabAsset(stage.prefabContentsRoot, stage.assetPath, out bool stageOk);
                    if (!stageOk)
                    {
                        error = "Prefab 模式保存失败：" + stage.assetPath;
                        return false;
                    }

                    LogPrefabSaved(stage.assetPath);
                    return true;
                }

                string skillId = sequence.SkillId;
                string expectedPath = string.IsNullOrWhiteSpace(skillId)
                    ? null
                    : GetCanonicalSequencePrefabPath(skillId.Trim());

                if (PrefabUtility.IsPartOfPrefabInstance(go))
                {
                    GameObject instanceRoot = PrefabUtility.GetNearestPrefabInstanceRoot(go);
                    if (instanceRoot == null)
                        instanceRoot = go;

                    string sourcePath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(instanceRoot);
                    if (IsSameAssetPath(sourcePath, expectedPath))
                    {
                        PrefabUtility.ApplyPrefabInstance(instanceRoot, InteractionMode.AutomatedAction);
                        LogPrefabSaved(sourcePath);
                        return true;
                    }

                    if (string.IsNullOrEmpty(expectedPath))
                    {
                        PrefabUtility.ApplyPrefabInstance(instanceRoot, InteractionMode.AutomatedAction);
                        LogPrefabSaved(sourcePath);
                        return true;
                    }

                    PrefabUtility.UnpackPrefabInstance(
                        instanceRoot,
                        PrefabUnpackMode.Completely,
                        InteractionMode.AutomatedAction);
                    GameObject forked = PrefabUtility.SaveAsPrefabAssetAndConnect(
                        sequence.gameObject,
                        expectedPath,
                        InteractionMode.AutomatedAction);
                    if (forked == null)
                    {
                        error = "另存预制体失败：" + expectedPath;
                        return false;
                    }

                    LogPrefabSaved(expectedPath + "（SkillId 与原预制体不同，已另存，未覆盖 " + sourcePath + "）");
                    return true;
                }

                if (string.IsNullOrEmpty(expectedPath))
                {
                    error = "不是预制体实例，且 SkillId 为空，无法确定保存路径。";
                    return false;
                }

                GameObject created = PrefabUtility.SaveAsPrefabAssetAndConnect(
                    go,
                    expectedPath,
                    InteractionMode.AutomatedAction);
                if (created == null)
                {
                    error = "保存预制体失败：" + expectedPath;
                    return false;
                }
                LogPrefabSaved(expectedPath);
                return true;
            }
            catch (System.Exception e)
            {
                error = e.Message;
                return false;
            }
        }

        static bool IsSameAssetPath(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
                return false;
            a = a.Replace('\\', '/');
            b = b.Replace('\\', '/');
            return string.Equals(a, b, System.StringComparison.OrdinalIgnoreCase);
        }

        static void LogPrefabSaved(string path)
        {
            string msg = "已保存 Sequence 预制体 " + path;
            Debug.Log("[SaveSequenceData] " + msg);
            if (FSequenceEditorWindow.instance != null)
                FSequenceEditorWindow.instance.ShowNotification(new GUIContent(msg));
        }
    }
}
