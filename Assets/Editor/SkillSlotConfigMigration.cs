#if UNITY_EDITOR
using EGamePlay.Combat;
using UnityEditor;
using UnityEngine;

namespace ACTGameEditor
{
    public static class SkillSlotConfigMigration
    {
        [MenuItem("ACTGame/从 IdleSkillMapping 创建 SkillSlotConfig")]
        public static void CreateSkillSlotConfigFromIdleSkillMapping()
        {
            var selection = Selection.activeObject as IdleSkillMapping;
            if (selection == null || selection.Mappings == null || selection.Mappings.Count == 0)
            {
                EditorUtility.DisplayDialog("迁移", "请先选中一个 IdleSkillMapping 资源。", "确定");
                return;
            }

            var config = ScriptableObject.CreateInstance<SkillSlotConfig>();
            config.Slots.Clear();

            var slotOrder = new[] { SkillSlotId.NormalAttack, SkillSlotId.Skill1, SkillSlotId.Skill2, SkillSlotId.Skill3, SkillSlotId.Ultimate, SkillSlotId.Dodge };
            int slotIndex = 0;

            foreach (var m in selection.Mappings)
            {
                if (m.SkillId <= 0) continue;

                var (sortBase, sortOffset) = IntToSkillSortAndOffset(m.Sort);
                var entry = new SkillSlotConfig.SlotEntry
                {
                    SlotId = slotIndex < slotOrder.Length ? slotOrder[slotIndex] : SkillSlotId.NormalAttack,
                    InputType = m.InputType,
                    PressType = m.PressType,
                    InputCallBackType = m.InputCallBackType,
                    DefaultSkillId = m.SkillId,
                    SortBase = sortBase,
                    SortOffset = sortOffset,
                    InputTimeout = m.InputTimeout,
                };
                config.Slots.Add(entry);
                slotIndex++;
            }

            string path = AssetDatabase.GetAssetPath(selection);
            if (string.IsNullOrEmpty(path)) path = "Assets/Game/Config";
            path = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(path), "SkillSlotConfig.asset");
            path = AssetDatabase.GenerateUniqueAssetPath(path);

            AssetDatabase.CreateAsset(config, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = config;
            EditorUtility.DisplayDialog("迁移完成", $"已创建 {path}\n请将其赋值给 NormalActPlayer.SlotConfig。\n释放条件（RequireRollTag 等）需在技能配置 AbilityConfigObject 中设置。", "确定");
        }

        static (SkillSort sortBase, int sortOffset) IntToSkillSortAndOffset(int sortValue)
        {
            var values = (SkillSort[])System.Enum.GetValues(typeof(SkillSort));
            int baseVal = (int)SkillSort.Normal;
            foreach (var v in values)
            {
                int iv = (int)v;
                if (sortValue >= iv) baseVal = iv;
            }
            return ((SkillSort)baseVal, sortValue - baseVal);
        }
    }
}
#endif
