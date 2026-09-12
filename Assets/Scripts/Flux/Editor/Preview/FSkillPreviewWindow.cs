using UnityEditor;
using UnityEngine;

namespace FluxEditor
{
    /// <summary>技能 Scene 预览开关。与工作台分开，避免挤占技能列表。</summary>
    public class FSkillPreviewWindow : EditorWindow
    {
        Vector2 _scroll;

        /// <summary>打开技能预览开关窗口。</summary>
        [MenuItem(FSequenceEditorWindow.MENU_PATH + FSequenceEditorWindow.PRODUCT_NAME + "/技能预览", false, 2)]
        public static void Open()
        {
            FSkillPreviewWindow window = GetWindow<FSkillPreviewWindow>();
            window.titleContent = new GUIContent("技能预览");
            window.minSize = new Vector2(240, 200);
            window.Show();
        }

        void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.HelpBox(
                "只影响 Scene 视图里的编辑器预览，不会写进技能预制体。\n"
                + "打开技能会进入 SkillEditor 场景；关闭工作台回到之前的场景。\n"
                + "选中判定盒事件后，Scene 里可拖中心 / 尺寸；旋转工具可改盒朝向。",
                MessageType.Info);
            FSkillPreviewOverlay.DrawSettingsGUI(showTitle: false);
            EditorGUILayout.EndScrollView();
        }
    }
}
