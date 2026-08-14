using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ACTGameEditor.Editor
{
    /// <summary>
    /// 生成 / 打开角色 Shader 测试场景。
    /// </summary>
    public static class CreateShaderTestScene
    {
        const string ScenePath = "Assets/Scenes/ShaderTest.unity";
        const string MaterialPath = "Assets/Res/Materials/Character/ActCharacter.mat";
        const string ScanMatPath = "Assets/Res/Materials/Character/ScanSphere.mat";
        const string EdgeMatPath = "Assets/Res/Materials/Character/ScanEdgeHighlight.mat";
        const string GfxConfigPath = "Assets/Res/Rendering/GraphicsFxConfig.asset";
        const string StarfieldSkyMatPath = "Assets/Res/Materials/ActStarfieldSky.mat";
        const string GhostMatPath = "Assets/Res/Materials/Character/ActGhost.mat";

        [MenuItem("ACTGame/Create Shader Test Scene")]
        public static void Create()
        {
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
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.fieldOfView = 45f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
            camGo.AddComponent<AudioListener>();
            var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
            camGo.transform.position = new Vector3(0f, 1.6f, -4.5f);
            camGo.transform.rotation = Quaternion.Euler(8f, 0f, 0f);

            CreatePrimitive("Ground", PrimitiveType.Plane, new Vector3(0f, 0f, 0f), new Vector3(2f, 1f, 2f),
                new Color(0.35f, 0.35f, 0.38f));

            CreatePrimitive("OccluderPillar", PrimitiveType.Cube, new Vector3(0f, 1.2f, 1.2f),
                new Vector3(0.6f, 2.4f, 0.6f), new Color(0.25f, 0.25f, 0.28f));

            CreateStarfieldSky();

            var characterMat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            var scanMat = AssetDatabase.LoadAssetAtPath<Material>(ScanMatPath);
            var edgeMat = AssetDatabase.LoadAssetAtPath<Material>(EdgeMatPath);
            var character = CreatePrimitive("TestCharacter", PrimitiveType.Capsule, new Vector3(0f, 1f, 0f),
                Vector3.one, Color.white, characterMat);

            var fx = character.AddComponent<CharacterRenderFX>();
            fx.BindModel(character.transform);
            character.AddComponent<ObjectFxController>();

            var ghostMat = AssetDatabase.LoadAssetAtPath<Material>(GhostMatPath);
            var afterimage = character.AddComponent<AfterimageController>();
            var afterSo = new SerializedObject(afterimage);
            afterSo.FindProperty("modelRoot").objectReferenceValue = character.transform;
            afterSo.FindProperty("ghostMaterial").objectReferenceValue = ghostMat;
            afterSo.ApplyModifiedPropertiesWithoutUndo();

            var scan = character.AddComponent<ScanPulseController>();
            var scanSo = new SerializedObject(scan);
            scanSo.FindProperty("scanOrigin").objectReferenceValue = character.transform;
            scanSo.FindProperty("scanSphereMaterial").objectReferenceValue = scanMat;
            scanSo.FindProperty("maxRadius").floatValue = 8f;
            scanSo.ApplyModifiedPropertiesWithoutUndo();

            var tester = character.AddComponent<CharacterShaderTester>();
            var so = new SerializedObject(tester);
            so.FindProperty("renderFX").objectReferenceValue = fx;
            so.FindProperty("scanPulse").objectReferenceValue = scan;
            so.FindProperty("characterMaterial").objectReferenceValue = characterMat;
            so.ApplyModifiedPropertiesWithoutUndo();

            // 可扫描目标（挂 ScanTarget + ActCharacter 材质）
            CreateScanTarget("ScanTarget_A", new Vector3(3.5f, 1f, 2f), characterMat, edgeMat);
            CreateScanTarget("ScanTarget_B", new Vector3(-3.5f, 1f, 3f), characterMat, edgeMat);
            CreateScanTarget("ScanTarget_C", new Vector3(2f, 1f, -2.5f), characterMat, edgeMat);

            var volumeGo = new GameObject("GlobalVolume");
            var volume = volumeGo.AddComponent<UnityEngine.Rendering.Volume>();
            volume.isGlobal = true;
            var profile = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>(
                "Assets/Res/Rendering/CombatVolumeProfile.asset");
            if (profile != null)
                volume.sharedProfile = profile;

            CreateGraphicsFxBootstrap();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Debug.Log($"[ACTGame] ShaderTest scene saved: {ScenePath}");
        }

        static void CreateStarfieldSky()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(StarfieldSkyMatPath);
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "StarfieldSky";
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.position = Vector3.zero;
            go.transform.localScale = Vector3.one * 80f;
            if (mat != null)
                go.GetComponent<Renderer>().sharedMaterial = mat;
            else
                Debug.LogWarning($"[ACTGame] 找不到星空材质：{StarfieldSkyMatPath}");
        }

        static void CreateGraphicsFxBootstrap()
        {
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
        }

        static void CreateScanTarget(string name, Vector3 pos, Material bodyMat, Material edgeMat)
        {
            var go = CreatePrimitive(name, PrimitiveType.Capsule, pos, Vector3.one, Color.white, bodyMat);
            go.AddComponent<ObjectFxController>();
            var visual = go.AddComponent<ScanRevealVisual>();
            visual.Bind(go.transform, edgeMat);
            var target = go.AddComponent<ScanTarget>();
            var so = new SerializedObject(target);
            so.FindProperty("revealVisual").objectReferenceValue = visual;
            so.FindProperty("edgeHighlightMaterial").objectReferenceValue = edgeMat;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static GameObject CreatePrimitive(string name, PrimitiveType type, Vector3 pos, Vector3 scale,
            Color color, Material overrideMat = null)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.position = pos;
            go.transform.localScale = scale;

            var renderer = go.GetComponent<Renderer>();
            if (overrideMat != null)
            {
                renderer.sharedMaterial = overrideMat;
            }
            else
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                var mat = new Material(shader != null ? shader : Shader.Find("Standard"));
                mat.color = color;
                mat.name = name + "_Mat";
                renderer.sharedMaterial = mat;
            }

            return go;
        }
    }
}
