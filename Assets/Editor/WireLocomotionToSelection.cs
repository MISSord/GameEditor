using ACTGameEditor.Locomotion;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ACTGameEditor.Editor
{
    /// <summary>
    /// 给选中角色（如 ShaderTest 的 TestCharacter）挂上独立移动组件。
    /// </summary>
    public static class WireLocomotionToSelection
    {
        const string ConfigPath = "Assets/Res/Locomotion/LocomotionConfig.asset";
        const string InputActionsPath = "Assets/Scripts/Input/RPGFREEInputActions.inputactions";

        [MenuItem("ACTGame/Wire Locomotion To Selected Character")]
        public static void Wire()
        {
            var go = Selection.activeGameObject;
            if (go == null)
            {
                EditorUtility.DisplayDialog("Locomotion", "请先在 Hierarchy 选中角色（例如 TestCharacter）。", "OK");
                return;
            }

            EnsureConfigAsset();
            var config = AssetDatabase.LoadAssetAtPath<LocomotionConfig>(ConfigPath);
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            var cam = Camera.main;

            // CapsuleCollider 与 CharacterController 冲突时移除 Collider
            var capsule = go.GetComponent<CapsuleCollider>();
            var cc = go.GetComponent<CharacterController>();
            if (cc == null)
            {
                if (capsule != null)
                    Object.DestroyImmediate(capsule, true);

                cc = Undo.AddComponent<CharacterController>(go);
                cc.height = 2f;
                cc.radius = 0.5f;
                cc.center = Vector3.zero;
            }

            var inputReader = go.GetComponent<LocomotionInputReader>();
            if (inputReader == null)
                inputReader = Undo.AddComponent<LocomotionInputReader>(go);

            var inputSo = new SerializedObject(inputReader);
            inputSo.FindProperty("actionsAsset").objectReferenceValue = actions;
            inputSo.ApplyModifiedProperties();

            var loco = go.GetComponent<CharacterLocomotion>();
            if (loco == null)
                loco = Undo.AddComponent<CharacterLocomotion>(go);

            var animator = go.GetComponentInChildren<Animator>();
            var locoSo = new SerializedObject(loco);
            locoSo.FindProperty("config").objectReferenceValue = config;
            locoSo.FindProperty("animator").objectReferenceValue = animator;
            locoSo.FindProperty("moveCamera").objectReferenceValue = cam;
            locoSo.FindProperty("locomotionEnabled").boolValue = true;
            locoSo.ApplyModifiedProperties();

            // 场景级 Bootstrap（自带 Physics.Simulate）
            var bootstrap = Object.FindObjectOfType<LocomotionBootstrap>();
            if (bootstrap == null)
            {
                var bootGo = new GameObject("LocomotionBootstrap");
                Undo.RegisterCreatedObjectUndo(bootGo, "Create LocomotionBootstrap");
                bootstrap = bootGo.AddComponent<LocomotionBootstrap>();
            }

            var bootSo = new SerializedObject(bootstrap);
            bootSo.FindProperty("simulatePhysics").boolValue = false;
            bootSo.FindProperty("actionsAsset").objectReferenceValue = actions;
            bootSo.FindProperty("locomotionConfig").objectReferenceValue = config;
            bootSo.FindProperty("playerLocomotion").objectReferenceValue = loco;
            bootSo.FindProperty("inputReader").objectReferenceValue = inputReader;
            bootSo.FindProperty("moveCamera").objectReferenceValue = cam;
            bootSo.ApplyModifiedProperties();

            // 地面 Layer 9（默认 GroundLayers）
            var ground = GameObject.Find("Ground");
            if (ground != null && ground.layer != 9)
            {
                Undo.RecordObject(ground, "Set Ground Layer");
                ground.layer = 9;
            }

            // 可选：主相机简易跟随
            if (cam != null)
            {
                var follow = cam.GetComponent<LocomotionTestCameraFollow>();
                if (follow == null)
                    follow = Undo.AddComponent<LocomotionTestCameraFollow>(cam.gameObject);

                var followSo = new SerializedObject(follow);
                followSo.FindProperty("target").objectReferenceValue = go.transform;
                followSo.ApplyModifiedProperties();
            }

            EditorSceneManager.MarkSceneDirty(go.scene);
            Selection.activeGameObject = go;
            Debug.Log($"[ACTGame] 已为 {go.name} 挂上 Locomotion。Play 后 WASD 移动；确认 Ground 在 Layer 9。");
        }

        [MenuItem("ACTGame/Wire Locomotion To Selected Character", true)]
        static bool WireValidate() => Selection.activeGameObject != null;

        static void EnsureConfigAsset()
        {
            if (AssetDatabase.LoadAssetAtPath<LocomotionConfig>(ConfigPath) != null)
                return;

            if (!AssetDatabase.IsValidFolder("Assets/Res/Locomotion"))
            {
                if (!AssetDatabase.IsValidFolder("Assets/Res"))
                    AssetDatabase.CreateFolder("Assets", "Res");
                AssetDatabase.CreateFolder("Assets/Res", "Locomotion");
            }

            var config = ScriptableObject.CreateInstance<LocomotionConfig>();
            AssetDatabase.CreateAsset(config, ConfigPath);
            AssetDatabase.SaveAssets();
        }
    }
}
