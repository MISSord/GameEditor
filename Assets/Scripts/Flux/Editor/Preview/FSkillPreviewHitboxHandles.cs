using EGamePlay.Combat;
using Flux;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace FluxEditor
{
    /// <summary>选中 TriggerRange 事件时，在 Scene 里拖判定盒的中心、尺寸和旋转。</summary>
    public static class FSkillPreviewHitboxHandles
    {
        const float MinExtent = 0.01f;

        static readonly BoxBoundsHandle BoxHandle = new BoxBoundsHandle();
        static readonly SphereBoundsHandle SphereHandle = new SphereBoundsHandle();
        static readonly CapsuleBoundsHandle CapsuleHandle = new CapsuleBoundsHandle();

        static int _undoHotControl;

        /// <summary>按 Flux 当前选中的判定盒事件画手柄。必须在 SceneView 的全部 GUI 事件里调用，不能只在 Repaint。</summary>
        public static void Draw(FSequence sequence)
        {
            if (!FSkillPreviewOverlay.ShowHitboxHandles || sequence == null)
                return;
            if (FSequenceEditorWindow.instance == null)
                return;

            FSequenceEditor editor = FSequenceEditorWindow.instance.GetSequenceEditor();
            if (editor == null)
                return;

            List<FEventEditor> editors = editor.EventSelection.Editors;
            for (int i = 0; i < editors.Count; i++)
            {
                FTriggerRangeEvent ev = editors[i] != null ? editors[i].Evt as FTriggerRangeEvent : null;
                if (ev == null || ev.cubeRange == null || ev.Owner == null)
                    continue;
                if (ev.Sequence != sequence)
                    continue;
                DrawOne(ev);
            }

            if (Event.current.type == EventType.MouseUp || GUIUtility.hotControl == 0)
                _undoHotControl = 0;
        }

        static void DrawOne(FTriggerRangeEvent ev)
        {
            Transform owner = ev.Owner;
            CubeRange range = ev.cubeRange;
            Matrix4x4 local = FSkillPreviewOverlay.HitboxLocalMatrix(owner, range);
            Color handleColor = FSkillPreviewOverlay.HitboxColor;
            handleColor.a = 1f;

            using (new Handles.DrawingScope(handleColor, local))
            {
                Tool tool = Tools.current;
                bool showMove = tool != Tool.Rotate;
                bool showRotate = tool == Tool.Rotate || tool == Tool.Transform;

                if (showMove)
                    DrawCenterHandle(ev, range);

                DrawShapeHandle(ev, range);

                if (showRotate)
                    DrawRotationHandle(ev, owner, range, local);
            }
        }

        static void DrawCenterHandle(FTriggerRangeEvent ev, CubeRange range)
        {
            EditorGUI.BeginChangeCheck();
            Vector3 newPos = Handles.PositionHandle(range.pos, Quaternion.identity);
            if (!EditorGUI.EndChangeCheck())
                return;
            Commit(ev, () => range.pos = newPos);
        }

        static void DrawShapeHandle(FTriggerRangeEvent ev, CubeRange range)
        {
            Color wire = FSkillPreviewOverlay.HitboxColor;
            wire.a = 1f;

            EditorGUI.BeginChangeCheck();
            switch (range.colliderType)
            {
                case ColliderType.Sphere:
                    SphereHandle.SetColor(wire);
                    SphereHandle.center = range.pos;
                    SphereHandle.radius = Mathf.Max(MinExtent, range.radius);
                    SphereHandle.DrawHandle();
                    if (EditorGUI.EndChangeCheck())
                    {
                        Vector3 center = SphereHandle.center;
                        float radius = Mathf.Max(MinExtent, SphereHandle.radius);
                        Commit(ev, () =>
                        {
                            range.pos = center;
                            range.radius = radius;
                        });
                    }
                    break;

                case ColliderType.Capsule:
                    CapsuleHandle.SetColor(wire);
                    CapsuleHandle.heightAxis = CapsuleBoundsHandle.HeightAxis.Y;
                    CapsuleHandle.center = range.pos;
                    CapsuleHandle.radius = Mathf.Max(MinExtent, range.radius);
                    CapsuleHandle.height = Mathf.Max(CapsuleHandle.radius * 2f, range.height);
                    CapsuleHandle.DrawHandle();
                    if (EditorGUI.EndChangeCheck())
                    {
                        Vector3 center = CapsuleHandle.center;
                        float radius = Mathf.Max(MinExtent, CapsuleHandle.radius);
                        float height = Mathf.Max(radius * 2f, CapsuleHandle.height);
                        Commit(ev, () =>
                        {
                            range.pos = center;
                            range.radius = radius;
                            range.height = height;
                        });
                    }
                    break;

                default:
                    BoxHandle.SetColor(wire);
                    BoxHandle.center = range.pos;
                    BoxHandle.size = MinSize(range.size);
                    BoxHandle.DrawHandle();
                    if (EditorGUI.EndChangeCheck())
                    {
                        Vector3 center = BoxHandle.center;
                        Vector3 size = MinSize(BoxHandle.size);
                        Commit(ev, () =>
                        {
                            range.pos = center;
                            range.size = size;
                        });
                    }
                    break;
            }
        }

        static void DrawRotationHandle(FTriggerRangeEvent ev, Transform owner, CubeRange range, Matrix4x4 local)
        {
            Vector3 worldCenter = local.MultiplyPoint3x4(range.pos);
            Quaternion worldRot = owner.rotation * Quaternion.Euler(range.rotation);

            Matrix4x4 old = Handles.matrix;
            Handles.matrix = Matrix4x4.identity;
            EditorGUI.BeginChangeCheck();
            Quaternion newWorldRot = Handles.RotationHandle(worldRot, worldCenter);
            Handles.matrix = old;
            if (!EditorGUI.EndChangeCheck())
                return;

            Vector3 euler = (Quaternion.Inverse(owner.rotation) * newWorldRot).eulerAngles;
            Commit(ev, () => range.rotation = euler);
        }

        static void Commit(FTriggerRangeEvent ev, Action apply)
        {
            int hot = GUIUtility.hotControl;
            if (hot != _undoHotControl)
            {
                Undo.RegisterCompleteObjectUndo(ev, "Edit Hitbox");
                _undoHotControl = hot;
            }

            apply();
            EditorUtility.SetDirty(ev);
            SkillWorkbenchSession.NotifyEdited();

            if (FSequenceEditorWindow.instance != null)
                FSequenceEditorWindow.instance.Repaint();
        }

        static Vector3 MinSize(Vector3 size)
        {
            return new Vector3(
                Mathf.Max(MinExtent, size.x),
                Mathf.Max(MinExtent, size.y),
                Mathf.Max(MinExtent, size.z));
        }
    }
}
