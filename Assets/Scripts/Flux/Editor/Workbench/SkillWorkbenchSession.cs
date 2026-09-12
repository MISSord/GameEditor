using Flux;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FluxEditor
{
    /// <summary>
    /// 技能工作台会话：打开时进入 SkillEditor 场景并 Unpack 轴；
    /// 关闭时清掉预览物体，再回到进入前的场景。
    /// </summary>
    public static class SkillWorkbenchSession
    {
        public const string SceneName = "SkillEditor";
        public const string SkillEditorScenePath = "Assets/Scenes/SkillEditor.unity";
        public const string PlayerSeqSettingPath = "Assets/Res/SeqSetting/PlayerFSeqSetting.asset";
        public const string EnemyASeqSettingPath = "Assets/Res/SeqSetting/EnmeyAFSeqSetting.asset";
        const string PlayerPreviewPrefabPath = "Assets/Game/Actors/Character/ActPlayer.prefab";
        const string YbotPreviewPrefabPath = "Assets/Game/Actors/Character/ybot.prefab";
        const string LegacyWorkbenchSceneName = "SkillWorkbench";

        static readonly string[] ReusedPreviewNames = { "ActTest" };

        static Scene _workScene;
        static SceneSetup[] _previousSetup;
        static bool _switchedScene;
        static GameObject _sequenceRoot;
        static FSequence _sequence;
        static string _assetPath;
        static Transform _previewRoot;
        static GameObject _spawnedPreviewRoot;
        static GameObject _dummyRoot;
        static bool _spawnedPreview;
        static RuntimeAnimatorController _reusedAnimatorOriginal;
        static AnimatorCullingMode _reusedCullingOriginal;
        static bool _reusedAnimatorEnabledOriginal;
        static readonly List<OwnerSnapshot> _ownerSnapshots = new List<OwnerSnapshot>();
        static readonly List<string> _missingOwners = new List<string>();
        static bool _dirty;
        static bool _hooks;

        struct OwnerSnapshot
        {
            public FTimeline Timeline;
            public string OwnerPath;
        }

        /// <summary>当前是否在工作台里编辑一条技能轴。</summary>
        public static bool IsEditing => _sequence != null;

        /// <summary>正在编辑的 Sequence 预制体路径。</summary>
        public static string AssetPath => _assetPath;

        /// <summary>工作台里的 Sequence 实例。</summary>
        public static FSequence Sequence => _sequence;

        /// <summary>预览角色（通常是带 Animator 的 ActTest）。</summary>
        public static Transform PreviewRoot => _previewRoot;

        /// <summary>木桩 CharacterController，未生成时为 null。</summary>
        public static CharacterController DummyCollider =>
            _dummyRoot != null ? _dummyRoot.GetComponent<CharacterController>() : null;

        /// <summary>当前技能编辑场景（通常是 SkillEditor.unity）。</summary>
        public static Scene WorkScene => _workScene;

        /// <summary>打开后尚未保存的修改。</summary>
        public static bool IsDirty => _dirty;

        /// <summary>Hierarchy / Flux 改了物体但没走 Undo 回调时，用 Unity dirty 标记补上。</summary>
        public static void PollDirty()
        {
            if (_sequenceRoot != null && EditorUtility.IsDirty(_sequenceRoot))
                _dirty = true;
        }

        /// <summary>未能绑上预览体的 Timeline 名称（特效 Owner 常在原编辑场景里）。</summary>
        public static IReadOnlyList<string> MissingOwners => _missingOwners;

        [InitializeOnLoadMethod]
        static void RegisterEditorHooks()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeReload;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeReload;
            EditorApplication.delayCall += CloseOrphanWorkbenchScenes;
        }

        /// <summary>打开指定技能预制体进入工作台。已有会话时先询问是否保存。</summary>
        public static bool Open(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
                return false;

            assetPath = assetPath.Replace('\\', '/');
            if (IsEditing && string.Equals(_assetPath, assetPath, StringComparison.OrdinalIgnoreCase))
            {
                FocusFlux();
                FramePreview();
                return true;
            }

            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                EditorUtility.DisplayDialog("技能工作台", "请先退出 Prefab 模式，再打开技能工作台。", "确定");
                return false;
            }

            if (!PromptSaveIfDirty())
                return false;

            CloseInternal(restorePreviousScene: false);

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefab == null || prefab.GetComponent<FSequence>() == null)
            {
                EditorUtility.DisplayDialog("技能工作台", "不是带 FSequence 的技能预制体：\n" + assetPath, "确定");
                return false;
            }

            if (!EnterSkillEditorScene())
                return false;

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, _workScene);
            if (instance == null)
            {
                RestorePreviousScene();
                EditorUtility.DisplayDialog("技能工作台", "实例化预制体失败：\n" + assetPath, "确定");
                return false;
            }

            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            _sequenceRoot = instance;
            _sequence = instance.GetComponent<FSequence>();
            _assetPath = assetPath;
            _sequence.Rebuild();

            CaptureOwnerSnapshots();
            BindPreviewOwners();
            EnsureEditorHooks();

            FSequenceEditorWindow.Open(_sequence);
            FramePreview();
            _dirty = false;
            return true;
        }

        /// <summary>
        /// 新建技能轴预制体并打开。templateAssetPath 为空则建空白轴，否则复制该预制体。
        /// </summary>
        public static bool CreateSkill(string skillId, FSeqSetting setting, string templateAssetPath)
        {
            if (string.IsNullOrWhiteSpace(skillId))
            {
                EditorUtility.DisplayDialog("新建技能", "请填写 SkillId。", "确定");
                return false;
            }

            skillId = skillId.Trim();
            if (!int.TryParse(skillId, out _))
            {
                EditorUtility.DisplayDialog("新建技能", "SkillId 必须是整数。", "确定");
                return false;
            }

            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("新建技能", "Play 模式下不能新建技能。", "确定");
                return false;
            }

            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                EditorUtility.DisplayDialog("新建技能", "请先退出 Prefab 模式，再新建技能。", "确定");
                return false;
            }

            if (setting == null)
            {
                EditorUtility.DisplayDialog("新建技能", "请指定 FSeqSetting。", "确定");
                return false;
            }

            string destPath = SaveSequenceData.GetCanonicalSequencePrefabPath(skillId);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(destPath) != null)
            {
                EditorUtility.DisplayDialog("新建技能", "预制体已存在：\n" + destPath, "确定");
                return false;
            }

            if (!PromptSaveIfDirty())
                return false;

            FileTool.CheckDirOrCreat(
                Application.dataPath + "/" + SaveSequenceData.SequencePrefabFolder.Substring("Assets/".Length));

            bool created;
            if (string.IsNullOrEmpty(templateAssetPath))
                created = CreateBlankPrefab(destPath, skillId, setting);
            else
                created = CreatePrefabFromTemplate(templateAssetPath, destPath, skillId, setting);

            if (!created)
                return false;

            AssetDatabase.Refresh();
            return Open(destPath);
        }

        static bool CreateBlankPrefab(string destPath, string skillId, FSeqSetting setting)
        {
            GameObject go = new GameObject(skillId);
            FSequence sequence = FSequence.CreateSequence(go);
            sequence.name = skillId;
            sequence.SkillId = skillId;
            sequence.FSeqSetting = setting;
            sequence.FrameRate = FUtility.FrameRate;
            sequence.Length = sequence.FrameRate * FSequence.DEFAULT_LENGTH;

            Scene preview = EditorSceneManager.NewPreviewScene();
            SceneManager.MoveGameObjectToScene(go, preview);
            try
            {
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(go, destPath);
                if (saved == null)
                {
                    EditorUtility.DisplayDialog("新建技能", "保存空白轴失败：\n" + destPath, "确定");
                    return false;
                }

                return true;
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        static bool CreatePrefabFromTemplate(string templatePath, string destPath, string skillId, FSeqSetting setting)
        {
            if (!AssetDatabase.CopyAsset(templatePath, destPath))
            {
                EditorUtility.DisplayDialog("新建技能", "复制模板失败：\n" + templatePath, "确定");
                return false;
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(destPath);
            contents.name = skillId;
            FSequence sequence = contents.GetComponent<FSequence>();
            if (sequence == null)
            {
                PrefabUtility.UnloadPrefabContents(contents);
                AssetDatabase.DeleteAsset(destPath);
                EditorUtility.DisplayDialog("新建技能", "模板没有 FSequence。", "确定");
                return false;
            }

            sequence.SkillId = skillId;
            sequence.FSeqSetting = setting;
            PrefabUtility.SaveAsPrefabAsset(contents, destPath);
            PrefabUtility.UnloadPrefabContents(contents);
            return true;
        }

        /// <summary>导出 SO 并写回 Sequence 预制体。成功后预览 Owner 会重新绑上。</summary>
        public static bool Save()
        {
            if (!IsEditing)
            {
                EditorUtility.DisplayDialog("保存失败", "工作台没有打开的技能。", "确定");
                return false;
            }

            StopFluxPreview();

            if (!SaveSequenceData.TryValidateSequence(_sequence, out string error))
            {
                EditorUtility.DisplayDialog("保存失败", error, "确定");
                return false;
            }

            SaveSequenceData.RestorePreviewAnimators(_sequence);

            if (!SaveSequenceData.ExportOpenedSequence(_sequence))
                return false;

            UnbindOwnersForPrefabWrite();

            PrefabUtility.SaveAsPrefabAsset(_sequenceRoot, _assetPath, out bool saved);
            if (!saved)
            {
                RebindPreviewOwners();
                EditorUtility.DisplayDialog("保存失败", "写回预制体失败：\n" + _assetPath, "确定");
                return false;
            }

            RebindPreviewOwners();
            AssetDatabase.SaveAssets();
            _dirty = false;

            if (FSequenceEditorWindow.instance != null)
            {
                FSequenceEditorWindow.instance.GetSequenceEditor()?.Refresh();
                FSequenceEditorWindow.instance.ShowNotification(new GUIContent("工作台已保存 " + _assetPath));
            }

            Debug.Log("[SkillWorkbench] 已保存 " + _assetPath);
            return true;
        }

        /// <summary>关闭会话。dirty 时询问保存；forceDiscard 用于进 Play / 域重载。</summary>
        public static void Close(bool promptSave)
        {
            if (!IsEditing)
            {
                CloseOrphanWorkbenchScenes();
                return;
            }

            if (promptSave && !PromptSaveIfDirty())
                return;

            CloseInternal(restorePreviousScene: true);
        }

        /// <summary>按 SkillId 号段猜默认 FSeqSetting：12000 段用敌人，其余用玩家。</summary>
        public static FSeqSetting GuessSetting(string skillId)
        {
            string path = PlayerSeqSettingPath;
            if (int.TryParse(skillId, out int id) && id >= 12000 && id < 13000)
                path = EnemyASeqSettingPath;
            return AssetDatabase.LoadAssetAtPath<FSeqSetting>(path);
        }

        /// <summary>是否为工作台按号段自动填的默认 SeqSetting（玩家或敌人 A）。</summary>
        public static bool IsDefaultSeqSetting(FSeqSetting setting)
        {
            if (setting == null)
                return true;
            string path = AssetDatabase.GetAssetPath(setting);
            return string.Equals(path, PlayerSeqSettingPath, StringComparison.OrdinalIgnoreCase)
                || string.Equals(path, EnemyASeqSettingPath, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsWorkbenchSequence(FSequence sequence)
        {
            return sequence != null && _sequence != null && sequence == _sequence;
        }

        /// <summary>按预览开关生成/挪动木桩。非工作台会话不会生成。</summary>
        public static void SyncDummy(bool show, float distance)
        {
            if (!show || !IsEditing || !_workScene.IsValid())
            {
                DestroyDummy();
                return;
            }

            if (_dummyRoot == null)
            {
                GameObject dummy = new GameObject("WorkbenchDummy");
                GameObject mesh = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                mesh.name = "Mesh";
                mesh.transform.SetParent(dummy.transform, false);
                mesh.transform.localPosition = new Vector3(0f, 0.96f, 0f);
                mesh.transform.localScale = new Vector3(0.5f, 0.9f, 0.5f);
                CapsuleCollider builtin = mesh.GetComponent<CapsuleCollider>();
                if (builtin != null)
                    UnityEngine.Object.DestroyImmediate(builtin);

                CharacterController cc = dummy.AddComponent<CharacterController>();
                cc.height = 1.8f;
                cc.radius = 0.25f;
                cc.center = new Vector3(0f, 0.96f, 0f);
                EditorSceneManager.MoveGameObjectToScene(dummy, _workScene);
                MarkDontSave(dummy);
                _dummyRoot = dummy;
            }

            Transform anchor = _previewRoot;
            CharacterController actorCc = anchor != null
                ? anchor.GetComponentInParent<CharacterController>()
                : null;
            Transform root = actorCc != null ? actorCc.transform : anchor;
            if (root == null)
                return;

            Vector3 forward = root.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.forward;
            forward.Normalize();

            _dummyRoot.transform.position = root.position + forward * distance;
            _dummyRoot.transform.rotation = Quaternion.LookRotation(-forward);
        }

        static void DestroyDummy()
        {
            if (_dummyRoot != null)
                UnityEngine.Object.DestroyImmediate(_dummyRoot);
            _dummyRoot = null;
        }

        /// <summary>工作台内容被编辑器改过（Scene 拖盒等），标脏以便保存。</summary>
        public static void NotifyEdited()
        {
            if (!IsEditing)
                return;
            _dirty = true;
            if (_sequenceRoot != null)
                EditorUtility.SetDirty(_sequenceRoot);
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingEditMode)
                return;
            if (!IsEditing)
                return;

            if (_dirty)
            {
                if (EditorUtility.DisplayDialog("技能工作台", "即将进入 Play。保存当前技能轴？", "保存", "不保存"))
                    Save();
            }

            CloseInternal(restorePreviousScene: true);
        }

        static void OnBeforeReload()
        {
            if (!IsEditing)
                return;
            if (_dirty)
                Debug.LogWarning("[SkillWorkbench] 脚本重载，未保存的技能轴修改已丢弃：" + _assetPath);
            CloseInternal(restorePreviousScene: true);
        }

        static bool PromptSaveIfDirty()
        {
            if (!IsEditing || !_dirty)
                return true;

            int choice = EditorUtility.DisplayDialogComplex(
                "技能工作台",
                "技能 " + DisplayName(_assetPath) + " 有未保存的修改。",
                "保存",
                "取消",
                "不保存");

            if (choice == 0)
                return Save();
            return choice == 2;
        }

        static void CloseInternal(bool restorePreviousScene)
        {
            StopFluxPreview();
            RestoreReusedPreviewAnimator();

            if (FSequenceEditorWindow.instance != null
                && FSequenceEditorWindow.instance.GetSequenceEditor() != null
                && IsWorkbenchSequence(FSequenceEditorWindow.instance.GetSequenceEditor().Sequence))
            {
                FSequenceEditorWindow.instance.GetSequenceEditor().OpenSequence(null);
            }

            GameObject seqRoot = _sequenceRoot;
            _sequence = null;
            _sequenceRoot = null;
            _assetPath = null;
            _ownerSnapshots.Clear();
            _missingOwners.Clear();
            _dirty = false;
            RemoveEditorHooks();

            if (seqRoot != null)
                UnityEngine.Object.DestroyImmediate(seqRoot);

            if (_spawnedPreview && _spawnedPreviewRoot != null)
                UnityEngine.Object.DestroyImmediate(_spawnedPreviewRoot);

            DestroyDummy();

            _previewRoot = null;
            _spawnedPreviewRoot = null;
            _spawnedPreview = false;
            _reusedAnimatorOriginal = null;

            if (restorePreviousScene)
                RestorePreviousScene();
            else
                CloseOrphanWorkbenchScenes();
        }

        static bool EnterSkillEditorScene()
        {
            CloseOrphanWorkbenchScenes();

            Scene active = EditorSceneManager.GetActiveScene();
            if (IsSkillEditorScene(active))
            {
                _workScene = active;
                return true;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return false;

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SkillEditorScenePath) == null)
            {
                EditorUtility.DisplayDialog("技能工作台", "找不到技能编辑场景：\n" + SkillEditorScenePath, "确定");
                return false;
            }

            _previousSetup = EditorSceneManager.GetSceneManagerSetup();
            Scene opened = EditorSceneManager.OpenScene(SkillEditorScenePath, OpenSceneMode.Single);
            if (!opened.IsValid() || !opened.isLoaded)
            {
                if (_previousSetup != null && _previousSetup.Length > 0)
                    EditorSceneManager.RestoreSceneManagerSetup(_previousSetup);
                _previousSetup = null;
                EditorUtility.DisplayDialog("技能工作台", "打开技能编辑场景失败：\n" + SkillEditorScenePath, "确定");
                return false;
            }

            _switchedScene = true;
            _workScene = opened;
            return true;
        }

        static void RestorePreviousScene()
        {
            bool switched = _switchedScene;
            SceneSetup[] setup = _previousSetup;
            _switchedScene = false;
            _previousSetup = null;
            _workScene = default;

            if (!switched || setup == null || setup.Length == 0)
                return;

            EditorSceneManager.RestoreSceneManagerSetup(setup);
        }

        static bool IsSkillEditorScene(Scene scene)
        {
            if (!scene.IsValid())
                return false;
            string path = scene.path != null ? scene.path.Replace('\\', '/') : string.Empty;
            return string.Equals(path, SkillEditorScenePath, StringComparison.OrdinalIgnoreCase)
                || string.Equals(scene.name, SceneName, StringComparison.OrdinalIgnoreCase);
        }

        static void CloseOrphanWorkbenchScenes()
        {
            if (IsEditing)
                return;

            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.name == LegacyWorkbenchSceneName && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        static void MarkDontSave(GameObject go)
        {
            if (go == null)
                return;
            go.hideFlags |= HideFlags.DontSave;
            Transform t = go.transform;
            for (int i = 0; i < t.childCount; i++)
                MarkDontSave(t.GetChild(i).gameObject);
        }

        static void CaptureOwnerSnapshots()
        {
            _ownerSnapshots.Clear();
            ForEachTimeline(tl =>
            {
                tl.SuppressOwnerPathWrite = true;
                _ownerSnapshots.Add(new OwnerSnapshot
                {
                    Timeline = tl,
                    OwnerPath = tl.OwnerPath
                });
            });
        }

        static void BindPreviewOwners()
        {
            _missingOwners.Clear();
            Transform actor = ResolveActorPreview();
            _previewRoot = actor;

            for (int i = 0; i < _ownerSnapshots.Count; i++)
            {
                FTimeline timeline = _ownerSnapshots[i].Timeline;
                if (timeline == null)
                    continue;

                string path = _ownerSnapshots[i].OwnerPath;
                Transform owner = IsActorTimeline(timeline, path)
                    ? actor
                    : FindSceneTransform(LeafName(path));

                if (owner == null)
                {
                    _missingOwners.Add(string.IsNullOrEmpty(timeline.name) ? path : timeline.name + "  (" + path + ")");
                    continue;
                }

                timeline.BindOwnerForPreview(owner);
            }
        }

        static void RebindPreviewOwners()
        {
            BindPreviewOwners();
            if (_sequence != null && _sequence.IsInit)
                _sequence.Init();
        }

        static void UnbindOwnersForPrefabWrite()
        {
            for (int i = 0; i < _ownerSnapshots.Count; i++)
            {
                FTimeline timeline = _ownerSnapshots[i].Timeline;
                if (timeline == null)
                    continue;
                timeline.RestoreSerializedOwner(_ownerSnapshots[i].OwnerPath);
            }
        }

        static Transform ResolveActorPreview()
        {
            Transform existing = FindReusedPreview();
            if (existing != null)
            {
                _spawnedPreview = false;
                _spawnedPreviewRoot = null;
                PrepareAnimator(existing, cacheOriginal: true);
                return existing;
            }

            GameObject prefab = LoadPreviewPrefab();
            if (prefab == null)
            {
                Debug.LogWarning("[SkillWorkbench] 找不到预览角色预制体，动画预览会缺 Owner。");
                return null;
            }

            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, _workScene);
            go.name = "WorkbenchPreview";
            StripRuntimeBehaviours(go);
            MarkDontSave(go);
            _spawnedPreview = true;
            _spawnedPreviewRoot = go;

            Transform owner = FindAnimatorOwner(go.transform);
            PrepareAnimator(owner, cacheOriginal: false);
            return owner;
        }

        /// <summary>
        /// Flux 动画轨只用 Owner.GetComponent&lt;Animator&gt;()。
        /// ActPlayer 根节点没有 Animator，真正的蒙皮在子物体 ActTest 上。
        /// </summary>
        static Transform FindAnimatorOwner(Transform root)
        {
            if (root == null)
                return null;

            Transform named = root.Find("ActTest");
            if (named != null && named.GetComponent<Animator>() != null)
                return named;

            Animator onRoot = root.GetComponent<Animator>();
            if (onRoot != null)
                return onRoot.transform;

            Animator child = root.GetComponentInChildren<Animator>(true);
            return child != null ? child.transform : root;
        }

        static void PrepareAnimator(Transform owner, bool cacheOriginal)
        {
            if (owner == null)
                return;

            Animator animator = owner.GetComponent<Animator>();
            if (animator == null)
                animator = owner.GetComponentInChildren<Animator>(true);
            if (animator == null)
                return;

            if (cacheOriginal)
            {
                _reusedAnimatorOriginal = animator.runtimeAnimatorController;
                _reusedCullingOriginal = animator.cullingMode;
                _reusedAnimatorEnabledOriginal = animator.enabled;
            }

            // ActTest.prefab 默认把 Animator 关掉；空场景里还要 AlwaysAnimate，否则 Scene 相机没对着就不采样。
            animator.enabled = true;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            ApplySettingController(animator);
        }

        static Transform FindReusedPreview()
        {
            for (int i = 0; i < ReusedPreviewNames.Length; i++)
            {
                Transform found = FindSceneTransform(ReusedPreviewNames[i]);
                if (found != null && found.GetComponentInChildren<Animator>() != null)
                    return found;
            }

            return null;
        }

        static GameObject LoadPreviewPrefab()
        {
            GameObject player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPreviewPrefabPath);
            if (player != null)
                return player;
            return AssetDatabase.LoadAssetAtPath<GameObject>(YbotPreviewPrefabPath);
        }

        static void RestoreReusedPreviewAnimator()
        {
            if (_spawnedPreview || _previewRoot == null)
                return;

            Animator animator = _previewRoot.GetComponent<Animator>();
            if (animator == null)
                animator = _previewRoot.GetComponentInChildren<Animator>();
            if (animator == null)
                return;

            if (_reusedAnimatorOriginal != null)
                animator.runtimeAnimatorController = _reusedAnimatorOriginal;
            animator.cullingMode = _reusedCullingOriginal;
            animator.enabled = _reusedAnimatorEnabledOriginal;
        }

        static void ApplySettingController(Animator animator)
        {
            if (animator == null || _sequence == null || _sequence.FSeqSetting == null)
                return;
            if (_sequence.FSeqSetting.targetAnimtorController != null)
                animator.runtimeAnimatorController = _sequence.FSeqSetting.targetAnimtorController;
        }

        static void StripRuntimeBehaviours(GameObject go)
        {
            MonoBehaviour[] behaviours = go.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] != null)
                    behaviours[i].enabled = false;
            }
        }

        static bool IsActorTimeline(FTimeline timeline, string ownerPath)
        {
            if (timeline != null && timeline.gameObject.name == "man_editor")
                return true;
            if (string.IsNullOrEmpty(ownerPath))
                return true;
            if (ownerPath.IndexOf("PlayerConstraint", StringComparison.OrdinalIgnoreCase) >= 0)
                return false;
            return ownerPath.IndexOf("ActTest", StringComparison.OrdinalIgnoreCase) >= 0
                || ownerPath.IndexOf("man_editor", StringComparison.OrdinalIgnoreCase) >= 0
                || ownerPath.StartsWith("/Editor/", StringComparison.Ordinal);
        }

        static string LeafName(string path)
        {
            if (string.IsNullOrEmpty(path))
                return string.Empty;
            int slash = path.LastIndexOf('/');
            return slash >= 0 && slash < path.Length - 1 ? path.Substring(slash + 1) : path.TrimStart('/');
        }

        static Transform FindSceneTransform(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;

            GameObject named = GameObject.Find(name);
            if (named != null && named.GetComponent<FObject>() == null)
                return named.transform;

            Transform[] all = Resources.FindObjectsOfTypeAll<Transform>();
            for (int i = 0; i < all.Length; i++)
            {
                Transform t = all[i];
                if (t == null || t.name != name)
                    continue;
                if (!t.gameObject.scene.IsValid() || !t.gameObject.scene.isLoaded)
                    continue;
                if (EditorUtility.IsPersistent(t))
                    continue;
                if (t.GetComponent<FObject>() != null)
                    continue;
                return t;
            }

            return null;
        }

        static void ForEachTimeline(Action<FTimeline> visitor)
        {
            if (_sequence == null || _sequence.Containers == null)
                return;
            for (int c = 0; c < _sequence.Containers.Count; c++)
            {
                FContainer container = _sequence.Containers[c];
                if (container == null || container.Timelines == null)
                    continue;
                for (int t = 0; t < container.Timelines.Count; t++)
                {
                    if (container.Timelines[t] != null)
                        visitor(container.Timelines[t]);
                }
            }
        }

        static void StopFluxPreview()
        {
            if (FSequenceEditorWindow.instance == null)
                return;
            FSequenceEditor editor = FSequenceEditorWindow.instance.GetSequenceEditor();
            if (editor != null && editor.Sequence != null)
                editor.Stop();
        }

        static void FocusFlux()
        {
            if (_sequence != null)
                FSequenceEditorWindow.Open(_sequence);
        }

        static void FramePreview()
        {
            if (_previewRoot == null)
                return;
            Selection.activeTransform = _previewRoot;
            if (SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.FrameSelected();
        }

        static void EnsureEditorHooks()
        {
            if (_hooks)
                return;
            Undo.postprocessModifications += OnUndoModified;
            _hooks = true;
        }

        static void RemoveEditorHooks()
        {
            if (!_hooks)
                return;
            Undo.postprocessModifications -= OnUndoModified;
            _hooks = false;
        }

        static UndoPropertyModification[] OnUndoModified(UndoPropertyModification[] modifications)
        {
            _dirty = true;
            return modifications;
        }

        static string DisplayName(string path)
        {
            return string.IsNullOrEmpty(path) ? "(无)" : System.IO.Path.GetFileNameWithoutExtension(path);
        }
    }
}
