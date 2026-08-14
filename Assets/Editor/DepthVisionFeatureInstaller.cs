#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ACTGameEditor.Editor
{
    /// <summary>
    /// 将 DepthVisionFeature 挂到当前 URP Renderer（若尚未挂载）。
    /// </summary>
    public static class DepthVisionFeatureInstaller
    {
        const string UrpRendererPath = "Assets/Res/New Universal Render Pipeline Asset_Renderer.asset";
        const string ShaderPath = "Assets/Shaders/ACTDepthVision.shader";
        [MenuItem("ACTGame/Add Player Fog To URP Renderer")]
        public static void InstallPlayerFog()
        {
            InstallFeature<PlayerFogFeature>("PlayerFog", "Assets/Shaders/ACTPlayerFog.shader");
        }

        [MenuItem("ACTGame/Add Screen Tint To URP Renderer")]
        public static void InstallScreenTint()
        {
            InstallFeature<ScreenTintFeature>("ScreenTint", "Assets/Shaders/ACTScreenTint.shader");
        }

        [MenuItem("ACTGame/Add Depth Vision To URP Renderer")]
        public static void Install()
        {
            InstallFeature<DepthVisionFeature>("DepthVision", "Assets/Shaders/ACTDepthVision.shader");
        }

        static void InstallFeature<T>(string featureName, string shaderPath) where T : ScriptableRendererFeature
        {
            var rendererData = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(UrpRendererPath);
            if (rendererData == null)
            {
                EditorUtility.DisplayDialog(featureName, $"找不到 Renderer：{UrpRendererPath}", "OK");
                return;
            }

            var so = new SerializedObject(rendererData);
            SerializedProperty featuresProp = so.FindProperty("m_RendererFeatures");
            if (featuresProp == null)
            {
                EditorUtility.DisplayDialog(featureName, "无法读取 m_RendererFeatures。", "OK");
                return;
            }

            for (int i = 0; i < featuresProp.arraySize; i++)
            {
                var existing = featuresProp.GetArrayElementAtIndex(i).objectReferenceValue;
                if (existing is T)
                {
                    Debug.Log($"[ACTGame] {featureName} 已存在。");
                    Selection.activeObject = rendererData;
                    return;
                }
            }

            var feature = ScriptableObject.CreateInstance<T>();
            feature.name = featureName;

            if (feature is DepthVisionFeature depth)
                depth.settings.shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
            else if (feature is ScreenTintFeature tint)
                tint.settings.shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
            else if (feature is PlayerFogFeature fog)
                fog.settings.shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);

            feature.SetActive(true);
            AssetDatabase.AddObjectToAsset(feature, rendererData);

            featuresProp.arraySize++;
            featuresProp.GetArrayElementAtIndex(featuresProp.arraySize - 1).objectReferenceValue = feature;
            so.ApplyModifiedProperties();

            SerializedProperty mapProp = so.FindProperty("m_RendererFeatureMap");
            if (mapProp != null && mapProp.isArray)
            {
                mapProp.arraySize = featuresProp.arraySize;
                so.ApplyModifiedProperties();
            }

            EditorUtility.SetDirty(rendererData);
            EditorUtility.SetDirty(feature);
            AssetDatabase.SaveAssets();
            Selection.activeObject = rendererData;
            Debug.Log($"[ACTGame] 已添加 {featureName} 到 URP Renderer。");
        }

        [MenuItem("ACTGame/Wire Player Fog To Selected Character")]
        public static void WirePlayerFog()
        {
            InstallPlayerFog();

            var go = Selection.activeGameObject;
            if (go == null)
            {
                EditorUtility.DisplayDialog("Player Fog", "请先选中角色（如 TestCharacter）。", "OK");
                return;
            }

            if (go.GetComponent<PlayerFogController>() == null)
                Undo.AddComponent<PlayerFogController>(go);

            EditorUtility.SetDirty(go);
            Debug.Log($"[ACTGame] 已为 {go.name} 挂上 PlayerFogController（热键 8：立方体迷雾）。");
        }

        [MenuItem("ACTGame/Wire Player Fog To Selected Character", true)]
        static bool WirePlayerFogValidate() => Selection.activeGameObject != null;

        [MenuItem("ACTGame/Wire Depth Vision To Selected Character")]
        public static void WireToCharacter()
        {
            Install();

            var go = Selection.activeGameObject;
            if (go == null)
            {
                EditorUtility.DisplayDialog("Depth Vision", "请先选中角色（如 TestCharacter）。", "OK");
                return;
            }

            if (go.GetComponent<DepthVisionController>() == null)
                Undo.AddComponent<DepthVisionController>(go);

            if (go.GetComponent<DepthVisionParticipant>() == null)
                Undo.AddComponent<DepthVisionParticipant>(go);

            EditorUtility.SetDirty(go);
            Debug.Log($"[ACTGame] 已为 {go.name} 挂上 DepthVisionController + Participant（热键 6；单对象可关 Include）。");
        }

        [MenuItem("ACTGame/Wire Depth Vision To Selected Character", true)]
        static bool WireValidate() => Selection.activeGameObject != null;

        [MenuItem("ACTGame/Add Depth Vision Participant To Selection Or ScanTargets")]
        public static void AddParticipants()
        {
            int count = 0;
            var selected = Selection.gameObjects;
            if (selected != null && selected.Length > 0)
            {
                for (int i = 0; i < selected.Length; i++)
                {
                    if (EnsureParticipant(selected[i]))
                        count++;
                }
            }
            else
            {
                var targets = Object.FindObjectsOfType<ScanTarget>();
                for (int i = 0; i < targets.Length; i++)
                {
                    if (EnsureParticipant(targets[i].gameObject))
                        count++;
                }

                var test = GameObject.Find("TestCharacter");
                if (test != null && EnsureParticipant(test))
                    count++;
            }

            if (count == 0)
                Debug.LogWarning("[ACTGame] 未添加任何 DepthVisionParticipant（可先选中对象，或场景中需有 ScanTarget）。");
            else
                Debug.Log($"[ACTGame] 已为 {count} 个对象添加/确认 DepthVisionParticipant。在 Inspector 取消 Include In Depth Vision 即可单独排除。");
        }

        static bool EnsureParticipant(GameObject go)
        {
            if (go == null)
                return false;
            if (go.GetComponent<DepthVisionParticipant>() != null)
                return false;
            Undo.AddComponent<DepthVisionParticipant>(go);
            EditorUtility.SetDirty(go);
            return true;
        }
    }
}
#endif
