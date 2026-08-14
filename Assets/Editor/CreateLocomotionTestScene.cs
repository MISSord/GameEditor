using ACTGameEditor.Locomotion;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

namespace ACTGameEditor.Editor
{
    /// <summary>
    /// 生成不依赖 Scene.prefab 的独立移动测试场景。
    /// </summary>
    public static class CreateLocomotionTestScene
    {
        const string ScenePath = "Assets/Scenes/LocomotionTest.unity";
        const string ConfigPath = "Assets/Res/Locomotion/LocomotionConfig.asset";
        const string InputActionsPath = "Assets/Scripts/Input/RPGFREEInputActions.inputactions";

        [MenuItem("ACTGame/Create Locomotion Test Scene")]
        public static void Create()
        {
            EnsureConfigAsset();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.96f, 0.9f);
            light.intensity = 1.1f;
            light.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.fieldOfView = 45f;
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<UniversalAdditionalCameraData>();
            camGo.transform.position = new Vector3(0f, 3.2f, -6f);
            camGo.transform.rotation = Quaternion.Euler(18f, 0f, 0f);

            // 地面：Layer 9，匹配默认 GroundLayers
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(3f, 1f, 3f);
            ground.layer = 9;
            var groundRenderer = ground.GetComponent<Renderer>();
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit != null)
            {
                var mat = new Material(lit) { color = new Color(0.32f, 0.34f, 0.36f) };
                groundRenderer.sharedMaterial = mat;
            }

            var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "LocomotionPlayer";
            player.transform.position = new Vector3(0f, 1f, 0f);
            Object.DestroyImmediate(player.GetComponent<CapsuleCollider>());

            var cc = player.AddComponent<CharacterController>();
            cc.height = 2f;
            cc.radius = 0.4f;
            cc.center = new Vector3(0f, 0f, 0f);

            var animatorGo = new GameObject("Model");
            animatorGo.transform.SetParent(player.transform, false);
            var animator = animatorGo.AddComponent<Animator>();

            var config = AssetDatabase.LoadAssetAtPath<LocomotionConfig>(ConfigPath);
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);

            var inputReader = player.AddComponent<LocomotionInputReader>();
            var inputSo = new SerializedObject(inputReader);
            inputSo.FindProperty("actionsAsset").objectReferenceValue = actions;
            inputSo.ApplyModifiedPropertiesWithoutUndo();

            var loco = player.AddComponent<CharacterLocomotion>();
            var locoSo = new SerializedObject(loco);
            locoSo.FindProperty("config").objectReferenceValue = config;
            locoSo.FindProperty("animator").objectReferenceValue = animator;
            locoSo.FindProperty("moveCamera").objectReferenceValue = cam;
            locoSo.ApplyModifiedPropertiesWithoutUndo();

            var bootstrapGo = new GameObject("LocomotionBootstrap");
            var bootstrap = bootstrapGo.AddComponent<LocomotionBootstrap>();
            var bootSo = new SerializedObject(bootstrap);
            bootSo.FindProperty("simulatePhysics").boolValue = false;
            bootSo.FindProperty("actionsAsset").objectReferenceValue = actions;
            bootSo.FindProperty("locomotionConfig").objectReferenceValue = config;
            bootSo.FindProperty("playerLocomotion").objectReferenceValue = loco;
            bootSo.FindProperty("inputReader").objectReferenceValue = inputReader;
            bootSo.FindProperty("moveCamera").objectReferenceValue = cam;
            bootSo.ApplyModifiedPropertiesWithoutUndo();

            // 简易跟随相机
            var follow = camGo.AddComponent<LocomotionTestCameraFollow>();
            var followSo = new SerializedObject(follow);
            followSo.FindProperty("target").objectReferenceValue = player.transform;
            followSo.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Debug.Log($"[ACTGame] LocomotionTest 已生成: {ScenePath}（WASD/左摇杆移动，无需 Scene.prefab）");
        }

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
