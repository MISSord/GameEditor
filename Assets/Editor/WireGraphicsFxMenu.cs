using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ACTGameEditor.Editor
{
    /// <summary>
    /// 将 GraphicsFx Service / DebugPanel 挂入当前打开场景。
    /// </summary>
    public static class WireGraphicsFxMenu
    {
        const string GfxConfigPath = "Assets/Res/Rendering/GraphicsFxConfig.asset";

        [MenuItem("ACTGame/Wire Graphics Fx Into Open Scene")]
        public static void WireIntoOpenScene()
        {
            if (Object.FindObjectOfType<GraphicsFxService>() != null)
            {
                Debug.Log("[ACTGame] 场景中已存在 GraphicsFxService，跳过创建。");
                return;
            }

            var gfxConfig = AssetDatabase.LoadAssetAtPath<GraphicsFxConfig>(GfxConfigPath);
            var go = new GameObject("GraphicsFx");
            var service = go.AddComponent<GraphicsFxService>();
            var serviceSo = new SerializedObject(service);
            serviceSo.FindProperty("config").objectReferenceValue = gfxConfig;
            serviceSo.FindProperty("autoApplyOnAwake").boolValue = true;
            serviceSo.ApplyModifiedPropertiesWithoutUndo();

            var panel = go.AddComponent<GraphicsFxDebugPanel>();
            var panelSo = new SerializedObject(panel);
            panelSo.FindProperty("showPanel").boolValue = true;
            panelSo.FindProperty("config").objectReferenceValue = gfxConfig;
            panelSo.ApplyModifiedPropertiesWithoutUndo();

            Undo.RegisterCreatedObjectUndo(go, "Wire Graphics Fx");
            EditorSceneManager.MarkSceneDirty(go.scene);
            Selection.activeGameObject = go;
            Debug.Log("[ACTGame] 已创建 GraphicsFx（Service + DebugPanel）。角色对象可另挂 ObjectFxController。");
        }
    }
}
