#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ACTGameEditor
{
    /// <summary>
    /// 编辑器工具：从 SkillInputEvents 遍历收集技能链，写入 SlotConfig 或 CharacterSlotConfig。
    /// </summary>
    public static class SkillSlotComboCollector
    {
        private const string SkillDataBasePath = "Assets/Game/Config/SkillDataScriptable";
        private const string SkillDataEnemyPath = "Assets/Game/Config/SkillDataScriptable/SkillData_Enemy";

        [MenuItem("ACTGame/收集槽位技能链（选中 SkillSlotConfig 或 CharacterSlotConfig）")]
        public static void CollectComboSkillIds()
        {
            var slotConfig = Selection.activeObject as SkillSlotConfig;
            if (slotConfig != null)
            {
                CollectForSkillSlotConfig(slotConfig);
                return;
            }

            var charConfig = Selection.activeObject as CharacterSlotConfig;
            if (charConfig != null)
            {
                CollectForCharacterSlotConfig(charConfig);
                return;
            }

            EditorUtility.DisplayDialog("错误", "请先选中一个 SkillSlotConfig 或 CharacterSlotConfig 资源。", "确定");
        }

        /// <summary>为 SkillSlotConfig 的每个槽位收集技能链。</summary>
        private static void CollectForSkillSlotConfig(SkillSlotConfig config)
        {
            int totalCollected = 0;
            foreach (var entry in config.Slots)
            {
                entry.ComboSkillIds.Clear();
                if (entry.DefaultSkillId <= 0) continue;

                var collected = CollectReachableSkillIds(entry.DefaultSkillId);
                entry.ComboSkillIds.AddRange(collected);
                totalCollected += entry.ComboSkillIds.Count;
            }

            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("完成", $"已为 SkillSlotConfig 的 {config.Slots.Count} 个槽位收集技能链，共 {totalCollected} 个技能 ID。", "确定");
        }

        /// <summary>为 CharacterSlotConfig 的每个覆盖收集技能链。需要关联 SkillSlotConfig 以获取槽位列表。</summary>
        private static void CollectForCharacterSlotConfig(CharacterSlotConfig config)
        {
            if (config.Overrides == null || config.Overrides.Count == 0)
            {
                EditorUtility.DisplayDialog("提示", "CharacterSlotConfig 没有配置任何 Overrides。", "确定");
                return;
            }

            int totalCollected = 0;
            foreach (var ov in config.Overrides)
            {
                ov.ComboSkillIds.Clear();
                if (ov.SkillId <= 0) continue;

                var collected = CollectReachableSkillIds(ov.SkillId);
                ov.ComboSkillIds.AddRange(collected);
                totalCollected += ov.ComboSkillIds.Count;
            }

            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("完成", $"已为 CharacterSlotConfig 的 {config.Overrides.Count} 个覆盖收集技能链，共 {totalCollected} 个技能 ID。", "确定");
        }

        /// <summary>
        /// 从入口 skillId 出发，沿 SkillInputEvents 的 InputDataList.SkillId 递归收集所有可达技能。
        /// </summary>
        private static HashSet<int> CollectReachableSkillIds(int entrySkillId)
        {
            var result = new HashSet<int>();
            var toVisit = new Queue<int>();
            toVisit.Enqueue(entrySkillId);

            while (toVisit.Count > 0)
            {
                int id = toVisit.Dequeue();
                if (id <= 0 || result.Contains(id)) continue;
                result.Add(id);

                var skillData = LoadSkillAllEventData(id);
                if (skillData == null || skillData.skillAllEventDatas == null) continue;

                foreach (var subData in skillData.skillAllEventDatas)
                {
                    if (subData.SkillInputEvents?.Events == null) continue;
                    foreach (var inputEvt in subData.SkillInputEvents.Events)
                    {
                        if (inputEvt.InputDataList == null) continue;
                        foreach (var inputData in inputEvt.InputDataList)
                        {
                            if (inputData.SkillId > 0 && !result.Contains(inputData.SkillId))
                                toVisit.Enqueue(inputData.SkillId);
                        }
                    }
                }
            }

            return result;
        }

        private static SkillAllEventData LoadSkillAllEventData(int skillId)
        {
            string path = $"{SkillDataBasePath}/{skillId}.asset";
            var data = AssetDatabase.LoadAssetAtPath<SkillAllEventData>(path);
            if (data != null) return data;
            path = $"{SkillDataEnemyPath}/{skillId}.asset";
            return AssetDatabase.LoadAssetAtPath<SkillAllEventData>(path);
        }
    }
}
#endif
