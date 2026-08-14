#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ACTGameEditor.Editor
{
    /// <summary>
    /// 近距镂空渐隐接线。
    /// </summary>
    public static class WireProximityDitherMenu
    {
        [MenuItem("ACTGame/Add Proximity Dither Fade To Selection")]
        public static void AddToSelection()
        {
            var selected = Selection.gameObjects;
            if (selected == null || selected.Length == 0)
            {
                EditorUtility.DisplayDialog("Proximity Dither", "请先选中物体（需使用 ACT/Character 材质）。", "OK");
                return;
            }

            int count = 0;
            for (int i = 0; i < selected.Length; i++)
            {
                GameObject go = selected[i];
                if (go == null)
                    continue;

                if (go.GetComponent<CharacterRenderFX>() == null)
                    Undo.AddComponent<CharacterRenderFX>(go);

                if (go.GetComponent<ProximityDitherFade>() == null)
                {
                    Undo.AddComponent<ProximityDitherFade>(go);
                    count++;
                }

                EditorUtility.SetDirty(go);
            }

            Debug.Log($"[ACTGame] 已为选中物体添加近距镂空（新增 {count}）。相机靠近时细密镂空透出后方，非透明混合。");
        }

        [MenuItem("ACTGame/Add Proximity Dither Fade To Selection", true)]
        static bool Validate() => Selection.gameObjects != null && Selection.gameObjects.Length > 0;
    }
}
#endif
