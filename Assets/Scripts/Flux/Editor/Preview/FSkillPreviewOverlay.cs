using EGamePlay.Combat;
using Flux;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEngine;
using UnityEngine.Rendering;

namespace FluxEditor
{
    /// <summary>
    /// 技能预览 Gizmo：判定盒 / 角色胶囊。Scene 视窗与独立视口共用 DrawGizmos。
    /// </summary>
    [InitializeOnLoad]
    public static class FSkillPreviewOverlay
    {
        const string PrefsPrefix = "Flux.SkillPreview.";
        const string HitboxesKey = PrefsPrefix + "Hitboxes";
        const string AllHitboxesKey = PrefsPrefix + "AllHitboxes";
        const string LabelsKey = PrefsPrefix + "Labels";
        const string SolidKey = PrefsPrefix + "Solid";
        const string XRayKey = PrefsPrefix + "XRay";
        const string CharacterKey = PrefsPrefix + "Character";
        const string HurtboxStateKey = PrefsPrefix + "HurtboxState";
        const string DummyKey = PrefsPrefix + "Dummy";
        const string DummyDistanceKey = PrefsPrefix + "DummyDistance";
        const string OwnerAxesKey = PrefsPrefix + "OwnerAxes";
        const string ColorKey = PrefsPrefix + "HitboxColor";
        const string HandlesKey = PrefsPrefix + "HitboxHandles";

        static readonly Color DefaultHitboxColor = new Color(1f, 0f, 0.91f, 0.62f);
        static readonly Vector3[] FaceVerts = new Vector3[4];
        static readonly List<int> DrawnIds = new List<int>(16);
        static bool _hitboxDetailsFoldout = true;

        static int _lastFrame = int.MinValue;
        static int _lastSequenceId;

        /// <summary>当前帧判定盒（与旧 Test_DrawCube 一致）。</summary>
        public static bool ShowHitboxes { get; private set; } = true;

        /// <summary>画出整条轴上的判定盒，当前帧以外半透明。</summary>
        public static bool ShowAllHitboxes { get; private set; }

        /// <summary>在盒上标段号 / 组。</summary>
        public static bool ShowLabels { get; private set; } = true;

        /// <summary>实心填充；关掉则只画线框。</summary>
        public static bool ShowSolid { get; private set; } = true;

        /// <summary>透视：不被角色模型挡住。</summary>
        public static bool ShowXRay { get; private set; }

        /// <summary>预览角色的受击胶囊（跟 Owner 走，不跟静止的父节点 CC）。</summary>
        public static bool ShowCharacterCollider { get; private set; }

        /// <summary>按当前帧 Tag 给角色胶囊着色（闪避无敌 / 霸体 / 招架窗）。</summary>
        public static bool ShowHurtboxState { get; private set; } = true;

        /// <summary>工作台前方生成木桩，对照判定盒。</summary>
        public static bool ShowDummy { get; private set; }

        /// <summary>木桩与预览角色的水平距离（米）。</summary>
        public static float DummyDistance { get; private set; } = 1.8f;

        /// <summary>Timeline Owner 的本地轴向。</summary>
        public static bool ShowOwnerAxes { get; private set; }

        /// <summary>选中判定盒事件后，在 Scene 里拖中心 / 尺寸 / 旋转。</summary>
        public static bool ShowHitboxHandles { get; private set; } = true;

        /// <summary>判定盒填充色，线框用同色不透明。</summary>
        public static Color HitboxColor { get; private set; } = DefaultHitboxColor;

        /// <summary>判定盒相对 Owner 的局部矩阵，预览绘制与 Scene 手柄共用。</summary>
        public static Matrix4x4 HitboxLocalMatrix(Transform owner, CubeRange range)
        {
            return Matrix4x4.TRS(
                owner.position,
                owner.rotation * Quaternion.Euler(range.rotation),
                owner.lossyScale);
        }

        static FSkillPreviewOverlay()
        {
            LoadPrefs();
            SceneView.duringSceneGui -= OnSceneGUI;
            SceneView.duringSceneGui += OnSceneGUI;
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
        }

        /// <summary>Flux 当前正在编辑的 Sequence；没有则回退工作台会话。</summary>
        public static FSequence GetPreviewSequence()
        {
            if (FSequenceEditorWindow.instance != null)
            {
                FSequenceEditor editor = FSequenceEditorWindow.instance.GetSequenceEditor();
                if (editor != null && editor.Sequence != null)
                    return editor.Sequence;
            }

            return SkillWorkbenchSession.IsEditing ? SkillWorkbenchSession.Sequence : null;
        }

        /// <summary>开关面板。技能预览窗口与 Scene Overlay 共用。</summary>
        public static void DrawSettingsGUI(bool showTitle)
        {
            if (showTitle)
                EditorGUILayout.LabelField("场景预览", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();

            ShowHitboxes = EditorGUILayout.Toggle("判定盒", ShowHitboxes);
            using (new EditorGUI.DisabledScope(!ShowHitboxes))
            {
                _hitboxDetailsFoldout = EditorGUILayout.Foldout(_hitboxDetailsFoldout, "判定盒选项", true);
                if (_hitboxDetailsFoldout)
                {
                    EditorGUI.indentLevel++;
                    ShowAllHitboxes = EditorGUILayout.Toggle("整条轴", ShowAllHitboxes);
                    ShowLabels = EditorGUILayout.Toggle("段号标签", ShowLabels);
                    ShowSolid = EditorGUILayout.Toggle("实心填充", ShowSolid);
                    ShowXRay = EditorGUILayout.Toggle("透视", ShowXRay);
                    HitboxColor = EditorGUILayout.ColorField("颜色", HitboxColor);
                    EditorGUI.indentLevel--;
                }
            }

            ShowHitboxHandles = EditorGUILayout.Toggle("拖拽判定盒", ShowHitboxHandles);

            ShowCharacterCollider = EditorGUILayout.Toggle("角色胶囊", ShowCharacterCollider);
            using (new EditorGUI.DisabledScope(!ShowCharacterCollider))
                ShowHurtboxState = EditorGUILayout.Toggle("受击/无敌着色", ShowHurtboxState);

            ShowDummy = EditorGUILayout.Toggle("木桩", ShowDummy);
            using (new EditorGUI.DisabledScope(!ShowDummy))
            {
                DummyDistance = EditorGUILayout.Slider("木桩距离", DummyDistance, 0.8f, 6f);
                if (ShowDummy && !SkillWorkbenchSession.IsEditing)
                    EditorGUILayout.HelpBox("打开技能工作台后才会在预览场景生成木桩。", MessageType.Info);
            }

            ShowOwnerAxes = EditorGUILayout.Toggle("Owner 轴向", ShowOwnerAxes);

            if (EditorGUI.EndChangeCheck())
            {
                SavePrefs();
                SkillWorkbenchSession.SyncDummy(ShowDummy, DummyDistance);
                SceneView.RepaintAll();
            }
        }

        static void OnEditorUpdate()
        {
            SkillWorkbenchSession.SyncDummy(ShowDummy, DummyDistance);

            FSequence sequence = GetPreviewSequence();
            int seqId = sequence != null ? sequence.GetInstanceID() : 0;
            int frame = sequence != null ? sequence.CurrentFrame : int.MinValue;
            if (frame != _lastFrame || seqId != _lastSequenceId)
            {
                _lastFrame = frame;
                _lastSequenceId = seqId;
                if (AnyVisible())
                    SceneView.RepaintAll();
            }
        }

        static void OnSceneGUI(SceneView sceneView)
        {
            FSequence sequence = GetPreviewSequence();
            if (sequence == null)
                return;

            FSkillPreviewHitboxHandles.Draw(sequence);
            DrawGizmos(sequence);
        }

        /// <summary>
        /// 在当前 Handles 相机下画判定盒 / 胶囊。Scene 与独立视口共用；拖盒仍只走 Scene。
        /// </summary>
        /// <param name="hasSceneDepth">
        /// Scene 视窗有深度缓冲，可以做遮挡。独立视口是先贴图再画线，没有深度，必须关 ZTest 否则盒会被裁掉。
        /// </param>
        public static void DrawGizmos(FSequence sequence, bool hasSceneDepth = true)
        {
            if (sequence == null || Event.current.type != EventType.Repaint)
                return;
            if (!AnyVisible())
                return;

            CompareFunction oldZTest = Handles.zTest;
            Handles.zTest = ShowXRay || !hasSceneDepth ? CompareFunction.Always : CompareFunction.LessEqual;
            Matrix4x4 oldMatrix = Handles.matrix;
            Color oldColor = Handles.color;

            DrawnIds.Clear();
            int frame = sequence.CurrentFrame;
            if (sequence.Containers != null)
            {
                for (int c = 0; c < sequence.Containers.Count; c++)
                {
                    FContainer container = sequence.Containers[c];
                    if (container == null || container.Timelines == null)
                        continue;
                    for (int t = 0; t < container.Timelines.Count; t++)
                    {
                        FTimeline timeline = container.Timelines[t];
                        if (timeline == null || !timeline.enabled)
                            continue;
                        DrawTimelineOverlays(timeline, sequence, frame);
                    }
                }
            }

            if (ShowDummy)
                DrawDummy();

            Handles.matrix = oldMatrix;
            Handles.color = oldColor;
            Handles.zTest = oldZTest;
        }

        static void DrawTimelineOverlays(FTimeline timeline, FSequence sequence, int frame)
        {
            Transform owner = timeline.Owner;
            if (owner == null)
                return;

            int ownerId = owner.GetInstanceID();
            if (ShowOwnerAxes && !DrawnIds.Contains(ownerId))
            {
                DrawnIds.Add(ownerId);
                DrawOwnerAxes(owner);
            }

            if (ShowCharacterCollider)
                DrawCharacterCollider(owner, sequence, frame);

            if (!ShowHitboxes || timeline.Tracks == null)
                return;

            for (int i = 0; i < timeline.Tracks.Count; i++)
            {
                FTriggerRangeTrack track = timeline.Tracks[i] as FTriggerRangeTrack;
                if (track == null || !track.enabled || track.Events == null)
                    continue;
                for (int e = 0; e < track.Events.Count; e++)
                {
                    FTriggerRangeEvent ev = track.Events[e] as FTriggerRangeEvent;
                    if (ev == null || ev.cubeRange == null)
                        continue;
                    DrawHitbox(ev, owner, frame);
                }
            }
        }

        static void DrawHitbox(FTriggerRangeEvent ev, Transform owner, int frame)
        {
            bool onFrame = frame >= 0 && ev.FrameRange.Contains(frame);
            bool selected = IsSelected(ev);
            if (!onFrame && !selected && !ShowAllHitboxes)
                return;

            CubeRange range = ev.cubeRange;
            Color fill = HitboxColor;
            Color wire = new Color(HitboxColor.r, HitboxColor.g, HitboxColor.b, 1f);
            if (selected)
            {
                wire = new Color(1f, 0.92f, 0.2f, 1f);
                fill = new Color(1f, 0.85f, 0.15f, fill.a);
            }
            else if (!onFrame)
            {
                fill.a *= 0.28f;
                wire.a = 0.35f;
            }

            Handles.matrix = HitboxLocalMatrix(owner, range);

            switch (range.colliderType)
            {
                case ColliderType.Sphere:
                    DrawSphere(range.pos, range.radius, fill, wire);
                    break;
                case ColliderType.Capsule:
                    DrawCapsuleY(range.pos, range.radius, range.height, fill, wire);
                    break;
                default:
                    DrawBox(range.pos, range.size, fill, wire);
                    break;
            }

            if (ShowLabels && (onFrame || selected))
            {
                Vector3 labelPos = range.pos + Vector3.up * (Mathf.Max(range.size.y, range.height, range.radius * 2f) * 0.5f + 0.05f);
                string label = "段" + ev.DamageSegmentIndex;
                if (ev.HitGroupId != 0)
                    label += " G" + ev.HitGroupId;
                Handles.Label(labelPos, label, EditorStyles.whiteMiniLabel);
            }
        }

        static void DrawCharacterCollider(Transform owner, FSequence sequence, int frame)
        {
            CharacterController cc = owner.GetComponent<CharacterController>();
            if (cc == null)
                cc = owner.GetComponentInParent<CharacterController>();
            if (cc == null)
                return;

            int id = cc.GetInstanceID();
            if (DrawnIds.Contains(id))
                return;
            DrawnIds.Add(id);

            Color wire = new Color(0.2f, 0.85f, 1f, 0.9f);
            string stateLabel = "受击";
            if (ShowHurtboxState)
                ResolveHurtboxColor(sequence, frame, owner, ref wire, ref stateLabel);

            Matrix4x4 hurtbox = HurtboxMatrix(owner, cc);
            Handles.matrix = hurtbox;
            DrawCapsuleY(cc.center, cc.radius, cc.height, new Color(wire.r, wire.g, wire.b, 0.12f), wire);
            if (ShowLabels)
            {
                Handles.matrix = Matrix4x4.identity;
                Vector3 top = hurtbox.MultiplyPoint3x4(cc.center + Vector3.up * (cc.height * 0.5f));
                Handles.Label(top + Vector3.up * 0.05f, stateLabel, EditorStyles.whiteMiniLabel);
            }
        }

        /// <summary>
        /// 运行时 CC 在 ActPlayer 根上，Flux Owner 是子物体 ActTest。
        /// 编辑器里位移轨 / RM 推 Owner，Mixamo 动画还会推 Hips；父节点 CC 都不动。
        /// </summary>
        static Matrix4x4 HurtboxMatrix(Transform owner, CharacterController cc)
        {
            Transform hips = FindPreviewHips(owner);
            bool ownerMoved = cc.transform != owner;
            if (hips == null && !ownerMoved)
                return owner.localToWorldMatrix;

            Vector3 pos = owner.position;
            if (hips != null)
            {
                pos.x = hips.position.x;
                pos.z = hips.position.z;
            }

            return Matrix4x4.TRS(pos, owner.rotation, cc.transform.lossyScale);
        }

        static Transform FindPreviewHips(Transform owner)
        {
            Animator animator = owner.GetComponent<Animator>();
            if (animator != null && animator.isHuman)
            {
                Transform bone = animator.GetBoneTransform(HumanBodyBones.Hips);
                if (bone != null)
                    return bone;
            }

            Transform named = owner.Find("mixamorig:Hips");
            if (named != null)
                return named;
            return owner.Find("Hips");
        }

        static void DrawDummy()
        {
            CharacterController cc = SkillWorkbenchSession.DummyCollider;
            if (cc == null)
                return;

            int id = cc.GetInstanceID();
            if (DrawnIds.Contains(id))
                return;
            DrawnIds.Add(id);

            Color wire = new Color(0.25f, 0.9f, 0.35f, 0.95f);
            Handles.matrix = cc.transform.localToWorldMatrix;
            DrawCapsuleY(cc.center, cc.radius, cc.height, new Color(wire.r, wire.g, wire.b, 0.12f), wire);
            if (ShowLabels)
            {
                Handles.matrix = Matrix4x4.identity;
                Vector3 top = cc.transform.TransformPoint(cc.center + Vector3.up * (cc.height * 0.5f));
                Handles.Label(top + Vector3.up * 0.05f, "木桩", EditorStyles.whiteMiniLabel);
            }
        }

        static void ResolveHurtboxColor(FSequence sequence, int frame, Transform owner, ref Color wire, ref string label)
        {
            bool roll = false;
            bool unstopped = false;
            bool parry = false;
            bool dazeRecover = false;
            CollectOwnerTags(sequence, frame, owner, ref roll, ref unstopped, ref parry, ref dazeRecover);

            if (roll)
            {
                wire = new Color(0.35f, 0.55f, 1f, 0.95f);
                label = "闪避无敌";
            }
            else if (dazeRecover)
            {
                wire = new Color(0.75f, 0.4f, 1f, 0.95f);
                label = "起身无敌";
            }
            else if (parry)
            {
                wire = new Color(1f, 0.92f, 0.35f, 0.95f);
                label = "招架窗";
            }
            else if (unstopped)
            {
                wire = new Color(1f, 0.7f, 0.15f, 0.95f);
                label = "霸体";
            }
        }

        static void CollectOwnerTags(
            FSequence sequence,
            int frame,
            Transform owner,
            ref bool roll,
            ref bool unstopped,
            ref bool parry,
            ref bool dazeRecover)
        {
            if (sequence == null || frame < 0 || sequence.Containers == null)
                return;

            for (int c = 0; c < sequence.Containers.Count; c++)
            {
                FContainer container = sequence.Containers[c];
                if (container == null || container.Timelines == null)
                    continue;
                for (int t = 0; t < container.Timelines.Count; t++)
                {
                    FTimeline timeline = container.Timelines[t];
                    if (timeline == null || timeline.Tracks == null || !IsSameOwner(timeline.Owner, owner))
                        continue;
                    for (int i = 0; i < timeline.Tracks.Count; i++)
                    {
                        FTrack track = timeline.Tracks[i];
                        if (track == null || !track.enabled || track.Events == null)
                            continue;
                        for (int e = 0; e < track.Events.Count; e++)
                        {
                            FPlayTagEvent tagEvent = track.Events[e] as FPlayTagEvent;
                            if (tagEvent == null || tagEvent.SkillTagList == null || !tagEvent.FrameRange.Contains(frame))
                                continue;
                            for (int s = 0; s < tagEvent.SkillTagList.Count; s++)
                            {
                                string tag = tagEvent.SkillTagList[s];
                                if (tag == CombatTags.BuffRoll)
                                    roll = true;
                                else if (tag == CombatTags.BuffUnStopped)
                                    unstopped = true;
                                else if (tag == CombatTags.CombatParryWindow)
                                    parry = true;
                                else if (tag == CombatTags.CombatDazeRecover)
                                    dazeRecover = true;
                            }
                        }
                    }
                }
            }
        }

        static bool IsSameOwner(Transform a, Transform b)
        {
            if (a == null || b == null)
                return false;
            if (a == b)
                return true;
            return a.root == b.root;
        }

        static void DrawOwnerAxes(Transform owner)
        {
            Handles.matrix = Matrix4x4.identity;
            float size = HandleUtility.GetHandleSize(owner.position) * 0.35f;
            Handles.color = Color.red;
            Handles.DrawLine(owner.position, owner.position + owner.right * size);
            Handles.color = Color.green;
            Handles.DrawLine(owner.position, owner.position + owner.up * size);
            Handles.color = Color.blue;
            Handles.DrawLine(owner.position, owner.position + owner.forward * size);
        }

        static void DrawBox(Vector3 center, Vector3 size, Color fill, Color wire)
        {
            if (ShowSolid)
            {
                Vector3 h = size * 0.5f;
                DrawFace(center, h, 0, 1, fill);
                DrawFace(center, h, 0, -1, fill);
                DrawFace(center, h, 1, 1, fill);
                DrawFace(center, h, 1, -1, fill);
                DrawFace(center, h, 2, 1, fill);
                DrawFace(center, h, 2, -1, fill);
            }

            Handles.color = wire;
            Handles.DrawWireCube(center, size);
        }

        static void DrawFace(Vector3 center, Vector3 h, int axis, int sign, Color fill)
        {
            Vector3 a;
            Vector3 b;
            Vector3 c;
            Vector3 d;
            if (axis == 0)
            {
                float x = center.x + h.x * sign;
                a = new Vector3(x, center.y - h.y, center.z - h.z);
                b = new Vector3(x, center.y - h.y, center.z + h.z);
                c = new Vector3(x, center.y + h.y, center.z + h.z);
                d = new Vector3(x, center.y + h.y, center.z - h.z);
            }
            else if (axis == 1)
            {
                float y = center.y + h.y * sign;
                a = new Vector3(center.x - h.x, y, center.z - h.z);
                b = new Vector3(center.x + h.x, y, center.z - h.z);
                c = new Vector3(center.x + h.x, y, center.z + h.z);
                d = new Vector3(center.x - h.x, y, center.z + h.z);
            }
            else
            {
                float z = center.z + h.z * sign;
                a = new Vector3(center.x - h.x, center.y - h.y, z);
                b = new Vector3(center.x + h.x, center.y - h.y, z);
                c = new Vector3(center.x + h.x, center.y + h.y, z);
                d = new Vector3(center.x - h.x, center.y + h.y, z);
            }

            FaceVerts[0] = a;
            FaceVerts[1] = b;
            FaceVerts[2] = c;
            FaceVerts[3] = d;
            Handles.DrawSolidRectangleWithOutline(FaceVerts, fill, Color.clear);
        }

        static void DrawSphere(Vector3 center, float radius, Color fill, Color wire)
        {
            if (radius <= 0f)
                return;
            if (ShowSolid)
            {
                Handles.color = fill;
                Handles.SphereHandleCap(0, center, Quaternion.identity, radius, EventType.Repaint);
            }

            Handles.color = wire;
            Handles.DrawWireDisc(center, Vector3.up, radius);
            Handles.DrawWireDisc(center, Vector3.right, radius);
            Handles.DrawWireDisc(center, Vector3.forward, radius);
        }

        static void DrawCapsuleY(Vector3 center, float radius, float height, Color fill, Color wire)
        {
            if (radius <= 0f)
                return;
            float cylinderHalf = Mathf.Max(0f, height * 0.5f - radius);
            Vector3 top = center + Vector3.up * cylinderHalf;
            Vector3 bottom = center - Vector3.up * cylinderHalf;

            if (ShowSolid)
            {
                Handles.color = fill;
                Handles.SphereHandleCap(0, top, Quaternion.identity, radius, EventType.Repaint);
                Handles.SphereHandleCap(0, bottom, Quaternion.identity, radius, EventType.Repaint);
            }

            Handles.color = wire;
            Handles.DrawWireDisc(top, Vector3.up, radius);
            Handles.DrawWireDisc(bottom, Vector3.up, radius);
            Handles.DrawLine(top + Vector3.right * radius, bottom + Vector3.right * radius);
            Handles.DrawLine(top - Vector3.right * radius, bottom - Vector3.right * radius);
            Handles.DrawLine(top + Vector3.forward * radius, bottom + Vector3.forward * radius);
            Handles.DrawLine(top - Vector3.forward * radius, bottom - Vector3.forward * radius);
            Handles.DrawWireArc(top, Vector3.forward, Vector3.right, 180f, radius);
            Handles.DrawWireArc(top, Vector3.right, Vector3.back, 180f, radius);
            Handles.DrawWireArc(bottom, Vector3.forward, Vector3.left, 180f, radius);
            Handles.DrawWireArc(bottom, Vector3.right, Vector3.forward, 180f, radius);
        }

        static bool IsSelected(FEvent ev)
        {
            if (FSequenceEditorWindow.instance == null)
                return false;
            FSequenceEditor editor = FSequenceEditorWindow.instance.GetSequenceEditor();
            if (editor == null)
                return false;
            List<FEventEditor> editors = editor.EventSelection.Editors;
            for (int i = 0; i < editors.Count; i++)
            {
                if (editors[i] != null && editors[i].Evt == ev)
                    return true;
            }

            return false;
        }

        static bool AnyVisible()
        {
            return ShowHitboxes || ShowCharacterCollider || ShowOwnerAxes || ShowDummy;
        }

        static void LoadPrefs()
        {
            ShowHitboxes = EditorPrefs.GetBool(HitboxesKey, true);
            ShowAllHitboxes = EditorPrefs.GetBool(AllHitboxesKey, false);
            ShowLabels = EditorPrefs.GetBool(LabelsKey, true);
            ShowSolid = EditorPrefs.GetBool(SolidKey, true);
            ShowXRay = EditorPrefs.GetBool(XRayKey, false);
            ShowCharacterCollider = EditorPrefs.GetBool(CharacterKey, false);
            ShowHurtboxState = EditorPrefs.GetBool(HurtboxStateKey, true);
            ShowDummy = EditorPrefs.GetBool(DummyKey, false);
            DummyDistance = EditorPrefs.GetFloat(DummyDistanceKey, 1.8f);
            ShowOwnerAxes = EditorPrefs.GetBool(OwnerAxesKey, false);
            ShowHitboxHandles = EditorPrefs.GetBool(HandlesKey, true);
            HitboxColor = LoadColor(ColorKey, DefaultHitboxColor);
        }

        static void SavePrefs()
        {
            EditorPrefs.SetBool(HitboxesKey, ShowHitboxes);
            EditorPrefs.SetBool(AllHitboxesKey, ShowAllHitboxes);
            EditorPrefs.SetBool(LabelsKey, ShowLabels);
            EditorPrefs.SetBool(SolidKey, ShowSolid);
            EditorPrefs.SetBool(XRayKey, ShowXRay);
            EditorPrefs.SetBool(CharacterKey, ShowCharacterCollider);
            EditorPrefs.SetBool(HurtboxStateKey, ShowHurtboxState);
            EditorPrefs.SetBool(DummyKey, ShowDummy);
            EditorPrefs.SetFloat(DummyDistanceKey, DummyDistance);
            EditorPrefs.SetBool(OwnerAxesKey, ShowOwnerAxes);
            EditorPrefs.SetBool(HandlesKey, ShowHitboxHandles);
            EditorPrefs.SetString(ColorKey, ColorUtility.ToHtmlStringRGBA(HitboxColor));
        }

        static Color LoadColor(string key, Color fallback)
        {
            string html = EditorPrefs.GetString(key, string.Empty);
            if (string.IsNullOrEmpty(html))
                return fallback;
            Color color;
            return ColorUtility.TryParseHtmlString("#" + html, out color) ? color : fallback;
        }
    }

    /// <summary>Scene 视图里的技能预览开关。打开 Flux 技能轴时自动出现。</summary>
    [Overlay(typeof(SceneView), "flux-skill-preview", "技能预览", true)]
    public sealed class FSkillPreviewSceneOverlay : IMGUIOverlay, ITransientOverlay
    {
        /// <inheritdoc />
        public bool visible => FSkillPreviewOverlay.GetPreviewSequence() != null;

        /// <inheritdoc />
        public override void OnGUI()
        {
            FSkillPreviewOverlay.DrawSettingsGUI(showTitle: false);
        }
    }
}
