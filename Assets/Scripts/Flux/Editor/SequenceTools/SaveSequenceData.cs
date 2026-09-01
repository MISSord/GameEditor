using EGamePlay;
using Flux;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FluxEditor
{
    /// <summary>Flux Sequence 导出入口：校验、保存 SkillAllEventData、编辑器菜单。</summary>
    public class SaveSequenceData
    {
        public static AgentModelType curAgentName;

        public static void GetData()
        {
            TrySaveCurrentSequence();
        }

        /// <summary>校验当前窗口 Sequence 后导出。失败弹窗；成功只还原场景 Animator，不改工程 Controller。</summary>
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
            AssetDatabase.SaveAssets();
            return true;
        }

        /// <summary>导出前检查 SkillId、FSeqSetting 和可导出的 Timeline。</summary>
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

                SaveOneSeq(seq);
                RestorePreviewAnimators(seq);
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
    }
}
