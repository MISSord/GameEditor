using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ACTGameEditor.Editor
{
    /// <summary>
    /// 球形 / 圆锥显现接线与样例生成。
    /// </summary>
    public static class WireRevealVisionMenu
    {
        const string ScanSphereMatPath = "Assets/Res/Materials/Character/ScanSphere.mat";
        const string RevealMaskedMatPath = "Assets/Res/Materials/Character/RevealMasked.mat";

        [MenuItem("ACTGame/Wire Reveal Vision To Selected Character")]
        public static void Wire()
        {
            var go = Selection.activeGameObject;
            if (go == null)
            {
                EditorUtility.DisplayDialog("Reveal Vision", "请先选中角色（如 TestCharacter）。", "OK");
                return;
            }

            EnsureRevealMaskedMaterial();

            var controller = go.GetComponent<RevealVisionController>();
            if (controller == null)
                controller = Undo.AddComponent<RevealVisionController>(go);

            var sphereMat = AssetDatabase.LoadAssetAtPath<Material>(ScanSphereMatPath);
            if (sphereMat != null)
            {
                var so = new SerializedObject(controller);
                so.FindProperty("revealSphereMaterial").objectReferenceValue = sphereMat;
                so.FindProperty("revealOrigin").objectReferenceValue = go.transform;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            var cone = go.GetComponent<RevealConeController>();
            if (cone == null)
                cone = Undo.AddComponent<RevealConeController>(go);

            var coneSo = new SerializedObject(cone);
            coneSo.FindProperty("coneOrigin").objectReferenceValue = go.transform;
            coneSo.FindProperty("showScreenEdgeRing").boolValue = false;
            coneSo.ApplyModifiedPropertiesWithoutUndo();

            if (Object.FindObjectOfType<RevealVisionService>() == null)
            {
                var svcGo = new GameObject("RevealVisionService");
                Undo.RegisterCreatedObjectUndo(svcGo, "Create RevealVisionService");
                svcGo.AddComponent<RevealVisionService>();
            }

            // 强制重建样例以切到 ShaderMask 材质
            var old = GameObject.Find("RevealSamples");
            if (old != null)
                Undo.DestroyObjectImmediate(old);

            CreateSampleSubjects();
            // 球形染色后处理
            DepthVisionFeatureInstaller.InstallScreenTint();
            EditorSceneManager.MarkSceneDirty(go.scene);
            Debug.Log("[ACTGame] 显现已接入：5=球形(+浅蓝屏罩)，7=圆锥（原点=角色朝向）。请确认 URP Renderer 含 ScreenTint。");
        }

        [MenuItem("ACTGame/Wire Reveal Vision To Selected Character", true)]
        static bool WireValidate() => Selection.activeGameObject != null;

        static Material EnsureRevealMaskedMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(RevealMaskedMatPath);
            if (mat != null)
                return mat;

            var shader = Shader.Find("ACT/RevealMasked");
            if (shader == null)
            {
                Debug.LogError("[ACTGame] 找不到 Shader ACT/RevealMasked，请先编译。");
                return null;
            }

            mat = new Material(shader)
            {
                name = "RevealMasked",
                color = new Color(0.2f, 0.95f, 1f, 0.92f)
            };

            string dir = "Assets/Res/Materials/Character";
            if (!AssetDatabase.IsValidFolder(dir))
            {
                if (!AssetDatabase.IsValidFolder("Assets/Res/Materials"))
                    AssetDatabase.CreateFolder("Assets/Res", "Materials");
                AssetDatabase.CreateFolder("Assets/Res/Materials", "Character");
            }

            AssetDatabase.CreateAsset(mat, RevealMaskedMatPath);
            AssetDatabase.SaveAssets();
            return mat;
        }

        static void CreateSampleSubjects()
        {
            if (GameObject.Find("RevealSamples") != null)
                return;

            var maskedMat = EnsureRevealMaskedMaterial();
            var root = new GameObject("RevealSamples");
            Undo.RegisterCreatedObjectUndo(root, "Create RevealSamples");

            CreateMaskedQuad(root.transform, "Footprint_A", new Vector3(1.2f, 0.02f, 1.5f),
                Quaternion.Euler(90f, 25f, 0f), new Vector3(0.35f, 0.55f, 1f),
                RevealChannel.Footprints, new Color(0.15f, 0.85f, 1f, 0.9f), maskedMat);
            CreateMaskedQuad(root.transform, "Footprint_B", new Vector3(1.8f, 0.02f, 2.2f),
                Quaternion.Euler(90f, 25f, 0f), new Vector3(0.35f, 0.55f, 1f),
                RevealChannel.Footprints, new Color(0.15f, 0.85f, 1f, 0.9f), maskedMat);
            CreateMaskedQuad(root.transform, "Footprint_C", new Vector3(2.5f, 0.02f, 2.8f),
                Quaternion.Euler(90f, 25f, 0f), new Vector3(0.35f, 0.55f, 1f),
                RevealChannel.Footprints, new Color(0.15f, 0.85f, 1f, 0.9f), maskedMat);

            CreateMaskedQuad(root.transform, "HiddenImage_Sign", new Vector3(-2f, 1.2f, 1f),
                Quaternion.Euler(0f, 35f, 0f), new Vector3(1.2f, 0.8f, 1f),
                RevealChannel.HiddenImages, new Color(1f, 0.45f, 0.2f, 1f), maskedMat);

            // 样例 Cube：验证圆锥/球均可扫出
            CreateMaskedCube(root.transform, "HiddenCube", new Vector3(0f, 0.5f, 3.5f),
                Vector3.one, RevealChannel.Default, new Color(0.3f, 1f, 0.55f, 0.95f), maskedMat);
        }

        static void CreateMaskedCube(Transform parent, string name, Vector3 pos, Vector3 scale,
            RevealChannel channel, Color color, Material sharedMat)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            Undo.RegisterCreatedObjectUndo(cube, "Create Reveal Cube");
            cube.transform.SetParent(parent, false);
            cube.transform.position = pos;
            cube.transform.localScale = scale;
            Object.DestroyImmediate(cube.GetComponent<Collider>());

            var renderer = cube.GetComponent<Renderer>();
            if (sharedMat != null)
            {
                var inst = new Material(sharedMat) { name = name + "_Mat" };
                if (inst.HasProperty("_Color"))
                    inst.SetColor("_Color", color);
                else
                    inst.color = color;
                renderer.sharedMaterial = inst;
            }

            var subject = Undo.AddComponent<RevealVisionSubject>(cube);
            var so = new SerializedObject(subject);
            so.FindProperty("channel").intValue = (int)channel;
            so.FindProperty("visibilityDrive").enumValueIndex = (int)RevealVisibilityDrive.ShaderMask;
            so.FindProperty("mode").enumValueIndex = (int)RevealSubjectMode.RendererEnable;
            so.FindProperty("startHidden").boolValue = true;
            so.FindProperty("autoApplyRevealMaskedMaterial").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void CreateMaskedQuad(Transform parent, string name, Vector3 pos, Quaternion rot, Vector3 scale,
            RevealChannel channel, Color color, Material sharedMat)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            Undo.RegisterCreatedObjectUndo(quad, "Create Reveal Sample");
            quad.transform.SetParent(parent, false);
            quad.transform.SetPositionAndRotation(pos, rot);
            quad.transform.localScale = scale;
            Object.DestroyImmediate(quad.GetComponent<MeshCollider>());

            var renderer = quad.GetComponent<Renderer>();
            if (sharedMat != null)
            {
                var inst = new Material(sharedMat) { name = name + "_Mat" };
                if (inst.HasProperty("_Color"))
                    inst.SetColor("_Color", color);
                else
                    inst.color = color;
                renderer.sharedMaterial = inst;
            }

            var subject = Undo.AddComponent<RevealVisionSubject>(quad);
            var so = new SerializedObject(subject);
            so.FindProperty("channel").intValue = (int)channel;
            so.FindProperty("visibilityDrive").enumValueIndex = (int)RevealVisibilityDrive.ShaderMask;
            so.FindProperty("mode").enumValueIndex = (int)RevealSubjectMode.RendererEnable;
            so.FindProperty("startHidden").boolValue = true;
            so.FindProperty("autoApplyRevealMaskedMaterial").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        [MenuItem("ACTGame/Convert Selection To Reveal Subject")]
        public static void ConvertSelection()
        {
            var selected = Selection.gameObjects;
            if (selected == null || selected.Length == 0)
            {
                EditorUtility.DisplayDialog("Reveal Vision", "请先选中要显现的物体（如 Cube）。", "OK");
                return;
            }

            var maskedMat = EnsureRevealMaskedMaterial();
            int count = 0;
            for (int i = 0; i < selected.Length; i++)
            {
                GameObject go = selected[i];
                if (go == null)
                    continue;

                var subject = go.GetComponent<RevealVisionSubject>();
                if (subject == null)
                    subject = Undo.AddComponent<RevealVisionSubject>(go);

                var so = new SerializedObject(subject);
                so.FindProperty("visibilityDrive").enumValueIndex = (int)RevealVisibilityDrive.ShaderMask;
                so.FindProperty("autoApplyRevealMaskedMaterial").boolValue = true;
                so.FindProperty("startHidden").boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();

                var renderer = go.GetComponentInChildren<Renderer>();
                if (renderer != null && maskedMat != null)
                {
                    Color keep = Color.cyan;
                    var src = renderer.sharedMaterial;
                    if (src != null)
                    {
                        if (src.HasProperty("_BaseColor"))
                            keep = src.GetColor("_BaseColor");
                        else if (src.HasProperty("_Color"))
                            keep = src.color;
                    }

                    var inst = new Material(maskedMat) { name = go.name + "_RevealMasked" };
                    if (inst.HasProperty("_Color"))
                        inst.SetColor("_Color", keep);
                    Undo.RecordObject(renderer, "Assign RevealMasked");
                    renderer.sharedMaterial = inst;
                    EditorUtility.SetDirty(renderer);
                }

                EditorUtility.SetDirty(go);
                count++;
            }

            Debug.Log($"[ACTGame] 已把 {count} 个物体转为显现物（ACT/RevealMasked）。Play 后按 5/7 扫描即可看到。");
        }

        [MenuItem("ACTGame/Convert Selection To Reveal Subject", true)]
        static bool ConvertSelectionValidate() => Selection.gameObjects != null && Selection.gameObjects.Length > 0;
    }
}
