#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ACTGameEditor.Editor
{
    /// <summary>
    /// 角色 / 场景物效果预设接线。
    /// </summary>
    public static class WireObjectFxPresetMenu
    {
        const string CharacterMatPath = "Assets/Res/Materials/Character/ActCharacter.mat";
        const string PropMatPath = "Assets/Res/Materials/Character/ActProp.mat";
        const string GhostMatPath = "Assets/Res/Materials/Character/ActGhost.mat";

        [MenuItem("ACTGame/Fx Preset/Setup Selection As Character")]
        public static void SetupCharacter()
        {
            EnsurePropMaterial(); // no-op create if missing elsewhere
            int n = ApplyPreset(ObjectFxController.FxPreset.Character, preferPropMat: false, addProximity: true, addRenderFx: true);
            Debug.Log($"[ACTGame] 已按「角色」预设配置 {n} 个对象（描边+镂空+战斗 FX，ACT/Character）。");
        }

        [MenuItem("ACTGame/Fx Preset/Setup Selection As Prop (Dither Only)")]
        public static void SetupPropDither()
        {
            var propMat = EnsurePropMaterial();
            int n = ApplyPreset(ObjectFxController.FxPreset.PropDitherOnly, preferPropMat: true, addProximity: true, addRenderFx: false, propMat: propMat);
            Debug.Log($"[ACTGame] 已按「场景物-仅镂空」配置 {n} 个对象（ACT/Prop + ProximityDither）。");
        }

        [MenuItem("ACTGame/Fx Preset/Setup Selection As Prop (Outline Only)")]
        public static void SetupPropOutline()
        {
            EnsurePropMaterial();
            int n = ApplyPreset(ObjectFxController.FxPreset.PropOutlineOnly, preferPropMat: false, addProximity: false, addRenderFx: true);
            Debug.Log($"[ACTGame] 已按「场景物-仅高亮/描边」配置 {n} 个对象（保留 Character 材质以支持描边 Pass）。");
        }

        [MenuItem("ACTGame/Fx Preset/Setup Selection As Character", true)]
        [MenuItem("ACTGame/Fx Preset/Setup Selection As Prop (Dither Only)", true)]
        [MenuItem("ACTGame/Fx Preset/Setup Selection As Prop (Outline Only)", true)]
        static bool Validate() => Selection.gameObjects != null && Selection.gameObjects.Length > 0;

        static int ApplyPreset(
            ObjectFxController.FxPreset preset,
            bool preferPropMat,
            bool addProximity,
            bool addRenderFx,
            Material propMat = null)
        {
            var selected = Selection.gameObjects;
            int count = 0;
            for (int i = 0; i < selected.Length; i++)
            {
                GameObject go = selected[i];
                if (go == null)
                    continue;

                var fx = go.GetComponent<ObjectFxController>();
                if (fx == null)
                    fx = Undo.AddComponent<ObjectFxController>(go);

                fx.ApplyPreset(preset);
                EditorUtility.SetDirty(fx);

                if (addRenderFx && go.GetComponent<CharacterRenderFX>() == null)
                    Undo.AddComponent<CharacterRenderFX>(go);

                if (addProximity && go.GetComponent<ProximityDitherFade>() == null)
                    Undo.AddComponent<ProximityDitherFade>(go);

                if (addRenderFx && preset == ObjectFxController.FxPreset.Character)
                {
                    var afterimage = go.GetComponent<AfterimageController>();
                    if (afterimage == null)
                        afterimage = Undo.AddComponent<AfterimageController>(go);

                    var ghostMat = AssetDatabase.LoadAssetAtPath<Material>(GhostMatPath);
                    if (ghostMat != null)
                    {
                        var afterSo = new SerializedObject(afterimage);
                        afterSo.FindProperty("ghostMaterial").objectReferenceValue = ghostMat;
                        afterSo.ApplyModifiedPropertiesWithoutUndo();
                    }
                }

                if (preferPropMat && propMat != null)
                {
                    AssignMaterialsToRenderers(go, propMat, "Assign ActProp", makeInstance: true, enableDitherKeyword: true);
                }
                else if (preset == ObjectFxController.FxPreset.Character
                         || preset == ObjectFxController.FxPreset.PropOutlineOnly)
                {
                    // 角色/描边预设必须落到 ACT/Character，否则 Frame Debugger 里只有 URP/Lit，无 OcclusionOutline
                    var charMat = AssetDatabase.LoadAssetAtPath<Material>(CharacterMatPath);
                    if (charMat != null)
                        AssignMaterialsToRenderers(go, charMat, "Assign ActCharacter", makeInstance: false, enableDitherKeyword: false);
                    else
                        Debug.LogError($"[ACTGame] 找不到角色材质：{CharacterMatPath}");
                }

                // 再刷一次同步 Keyword/Pass
                fx.RefreshDependents();
                count++;
            }

            return count;
        }

        static void AssignMaterialsToRenderers(
            GameObject go,
            Material sourceMat,
            string undoName,
            bool makeInstance,
            bool enableDitherKeyword)
        {
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            for (int r = 0; r < renderers.Length; r++)
            {
                Renderer renderer = renderers[r];
                if (renderer == null)
                    continue;

                // 武器等独立 MeshRenderer 保留原材质；身体多为 SkinnedMeshRenderer
                if (renderer is not SkinnedMeshRenderer)
                    continue;

                Undo.RecordObject(renderer, undoName);
                Color keep = Color.white;
                var old = renderer.sharedMaterial;
                if (old != null)
                {
                    if (old.HasProperty("_BaseColor"))
                        keep = old.GetColor("_BaseColor");
                    else if (old.HasProperty("_Color"))
                        keep = old.color;
                }

                Material mat = sourceMat;
                if (makeInstance)
                {
                    mat = new Material(sourceMat) { name = go.name + "_Prop" };
                    if (enableDitherKeyword)
                        mat.EnableKeyword(MaterialFxSync.KwProximityDither);
                    if (mat.HasProperty("_BaseColor"))
                        mat.SetColor("_BaseColor", keep);
                }

                renderer.sharedMaterial = mat;
                EditorUtility.SetDirty(renderer);
            }
        }

        static Material EnsurePropMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(PropMatPath);
            if (mat != null)
                return mat;

            var shader = Shader.Find("ACT/Prop");
            if (shader == null)
            {
                Debug.LogError("[ACTGame] 找不到 ACT/Prop，请先编译 Shader。");
                return null;
            }

            mat = new Material(shader)
            {
                name = "ActProp",
                color = new Color(0.55f, 0.62f, 0.4f, 1f)
            };
            mat.EnableKeyword(MaterialFxSync.KwProximityDither);

            string dir = "Assets/Res/Materials/Character";
            if (!AssetDatabase.IsValidFolder(dir))
            {
                if (!AssetDatabase.IsValidFolder("Assets/Res/Materials"))
                    AssetDatabase.CreateFolder("Assets/Res", "Materials");
                AssetDatabase.CreateFolder("Assets/Res/Materials", "Character");
            }

            AssetDatabase.CreateAsset(mat, PropMatPath);
            AssetDatabase.SaveAssets();
            return mat;
        }

        [MenuItem("ACTGame/Fx Preset/Create ActProp Material")]
        public static void CreatePropMatOnly()
        {
            var mat = EnsurePropMaterial();
            if (mat != null)
            {
                Selection.activeObject = mat;
                Debug.Log($"[ACTGame] ActProp 材质：{PropMatPath}");
            }
        }
    }
}
#endif
