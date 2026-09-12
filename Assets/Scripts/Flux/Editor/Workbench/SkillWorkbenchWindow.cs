using Flux;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace FluxEditor
{
    /// <summary>技能轴工作台：从 SkillSequences 列表打开，Flux 编辑 Unpack 副本，保存写回预制体并导出 SO。</summary>
    public class SkillWorkbenchWindow : EditorWindow
    {
        Vector2 _listScroll;
        string _filter = "";
        List<SkillEntry> _entries = new List<SkillEntry>();
        double _nextRefreshAt;
        string _newSkillId = "";
        FSeqSetting _newSetting;
        int _templateIndex;
        bool _createFoldout;
        bool _helpFoldout;

        struct SkillEntry
        {
            public string Path;
            public string SkillId;
            public string FileName;
        }

        [MenuItem(FSequenceEditorWindow.MENU_PATH + FSequenceEditorWindow.PRODUCT_NAME + "/技能工作台", false, 1)]
        public static void Open()
        {
            SkillWorkbenchWindow window = GetWindow<SkillWorkbenchWindow>();
            window.titleContent = new GUIContent("技能工作台");
            window.minSize = new Vector2(280, 360);
            window.Show();
            window.RefreshEntries();
        }

        /// <summary>打开工作台窗口并进入指定技能预制体的编辑会话。</summary>
        public static void OpenAndEdit(string assetPath)
        {
            Open();
            SkillWorkbenchSession.Open(assetPath);
        }

        [MenuItem("Assets/Flux/在技能工作台打开", false, 40)]
        static void OpenSelectedPrefab()
        {
            string path = AssetPathOfSelectedSequence();
            if (!string.IsNullOrEmpty(path))
                OpenAndEdit(path);
        }

        [MenuItem("Assets/Flux/在技能工作台打开", true)]
        static bool OpenSelectedPrefabValidate()
        {
            return !string.IsNullOrEmpty(AssetPathOfSelectedSequence());
        }

        /// <summary>Project 双击 SkillSequences 下的 Sequence 预制体时进工作台；按住 Alt 走 Unity 默认 Prefab 模式。</summary>
        [OnOpenAsset(0)]
        static bool OnOpenSkillSequence(int instanceId, int line)
        {
            if (Event.current != null && Event.current.alt)
                return false;

            GameObject go = EditorUtility.InstanceIDToObject(instanceId) as GameObject;
            if (go == null || go.GetComponent<FSequence>() == null)
                return false;

            string path = AssetDatabase.GetAssetPath(instanceId);
            if (string.IsNullOrEmpty(path))
                return false;

            path = path.Replace('\\', '/');
            if (!path.StartsWith(SaveSequenceData.SequencePrefabFolder, StringComparison.OrdinalIgnoreCase))
                return false;

            OpenAndEdit(path);
            return true;
        }

        void OnEnable()
        {
            RefreshEntries();
            if (_newSetting == null)
                _newSetting = SkillWorkbenchSession.GuessSetting(_newSkillId);
        }

        void OnFocus()
        {
            RefreshEntries();
        }

        void OnGUI()
        {
            if (SkillWorkbenchSession.IsEditing)
                SkillWorkbenchSession.PollDirty();

            DrawSessionBar();
            EditorGUILayout.Space(4);
            DrawCreateSkill();
            EditorGUILayout.Space(4);
            DrawFilter();
            DrawSkillList();
            DrawHelp();
        }

        void DrawSessionBar()
        {
            EditorGUILayout.LabelField("当前编辑", EditorStyles.boldLabel);

            if (!SkillWorkbenchSession.IsEditing)
            {
                EditorGUILayout.HelpBox("未打开技能。从下方列表点选，或用「新建技能」创建轴。", MessageType.Info);
                return;
            }

            string skillId = SkillWorkbenchSession.Sequence != null
                ? SkillWorkbenchSession.Sequence.SkillId
                : Path.GetFileNameWithoutExtension(SkillWorkbenchSession.AssetPath);

            EditorGUILayout.LabelField("SkillId", string.IsNullOrEmpty(skillId) ? "(空)" : skillId);
            EditorGUILayout.LabelField("路径", SkillWorkbenchSession.AssetPath);
            EditorGUILayout.LabelField("状态", SkillWorkbenchSession.IsDirty ? "未保存" : "已保存");

            if (SkillWorkbenchSession.MissingOwners.Count > 0)
            {
                EditorGUILayout.HelpBox(
                    "这些 Timeline 没找到 Owner（特效通常还在编辑场景的 PlayerConstraint 下）：\n- "
                    + string.Join("\n- ", SkillWorkbenchSession.MissingOwners),
                    MessageType.Warning);
            }

            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = SkillWorkbenchSession.IsDirty ? new Color(0.6f, 1f, 0.6f) : Color.white;
            if (GUILayout.Button("保存并导出", GUILayout.Height(24)))
                SkillWorkbenchSession.Save();
            GUI.backgroundColor = Color.white;
            if (GUILayout.Button("关闭", GUILayout.Height(24)))
                SkillWorkbenchSession.Close(promptSave: true);
            EditorGUILayout.EndHorizontal();
        }

        void DrawCreateSkill()
        {
            _createFoldout = EditorGUILayout.Foldout(_createFoldout, "新建技能", true);
            if (!_createFoldout)
                return;

            if (EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("Play 模式下不能新建技能。", MessageType.Warning);
                return;
            }

            EditorGUI.BeginChangeCheck();
            _newSkillId = EditorGUILayout.TextField("SkillId", _newSkillId ?? string.Empty);
            if (EditorGUI.EndChangeCheck())
                ApplyGuessedSettingIfDefault();

            _newSetting = (FSeqSetting)EditorGUILayout.ObjectField("SeqSetting", _newSetting, typeof(FSeqSetting), false);

            string[] templateOptions = BuildTemplateOptions();
            _templateIndex = EditorGUILayout.Popup("模板", Mathf.Clamp(_templateIndex, 0, templateOptions.Length - 1), templateOptions);

            if (GUILayout.Button("新建并打开", GUILayout.Height(24)))
            {
                string templatePath = null;
                if (_templateIndex > 0)
                {
                    int entryIndex = _templateIndex - 1;
                    if (entryIndex >= 0 && entryIndex < _entries.Count)
                        templatePath = _entries[entryIndex].Path;
                }

                if (SkillWorkbenchSession.CreateSkill(_newSkillId, _newSetting, templatePath))
                {
                    _newSkillId = string.Empty;
                    _templateIndex = 0;
                    ApplyGuessedSettingIfDefault();
                    RefreshEntries();
                }
            }
        }

        void DrawFilter()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("技能列表", EditorStyles.boldLabel);
            if (GUILayout.Button("预览", GUILayout.Width(48)))
                FSkillPreviewWindow.Open();
            if (GUILayout.Button("刷新", GUILayout.Width(48)))
                RefreshEntries();
            EditorGUILayout.EndHorizontal();

            _filter = EditorGUILayout.TextField("筛选", _filter);
        }

        void DrawSkillList()
        {
            if (EditorApplication.timeSinceStartup > _nextRefreshAt)
            {
                RefreshEntries();
                _nextRefreshAt = EditorApplication.timeSinceStartup + 3;
            }

            _listScroll = EditorGUILayout.BeginScrollView(_listScroll, GUILayout.ExpandHeight(true));
            string filter = _filter != null ? _filter.Trim() : string.Empty;

            for (int i = 0; i < _entries.Count; i++)
            {
                SkillEntry entry = _entries[i];
                if (!MatchesFilter(entry, filter))
                    continue;

                bool current = SkillWorkbenchSession.IsEditing
                    && string.Equals(SkillWorkbenchSession.AssetPath, entry.Path, StringComparison.OrdinalIgnoreCase);

                EditorGUILayout.BeginHorizontal();
                GUI.backgroundColor = current ? new Color(0.55f, 0.8f, 1f) : Color.white;
                string label = string.IsNullOrEmpty(entry.SkillId) || entry.SkillId == entry.FileName
                    ? entry.FileName
                    : entry.SkillId + "  (" + entry.FileName + ")";
                if (GUILayout.Button(label, EditorStyles.miniButton))
                    SkillWorkbenchSession.Open(entry.Path);
                GUI.backgroundColor = Color.white;
                if (GUILayout.Button("定位", EditorStyles.miniButton, GUILayout.Width(40)))
                {
                    UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<GameObject>(entry.Path);
                    EditorGUIUtility.PingObject(asset);
                    Selection.activeObject = asset;
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
        }

        void DrawHelp()
        {
            EditorGUILayout.Space(4);
            _helpFoldout = EditorGUILayout.Foldout(_helpFoldout, "说明", true);
            if (!_helpFoldout)
                return;

            EditorGUILayout.HelpBox(
                "新建技能、打开技能都在本窗口完成。\n"
                + "打开技能会进入 Assets/Scenes/SkillEditor.unity，在 Scene 里预览和拖判定盒。\n"
                + "关闭工作台会回到进入前的场景（不保存 SkillEditor 里的预览物体）。\n"
                + "加轨 / 删轨在 Flux。保存会导出 SkillDataScriptable 并写回预制体。\n"
                + "Project 双击打开工作台；按住 Alt 再双击进入 Unity Prefab 模式。",
                MessageType.None);
        }

        void RefreshEntries()
        {
            _entries.Clear();
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { SaveSequenceData.SequencePrefabFolder });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                    continue;
                FSequence sequence = prefab.GetComponent<FSequence>();
                if (sequence == null)
                    continue;

                _entries.Add(new SkillEntry
                {
                    Path = path.Replace('\\', '/'),
                    SkillId = sequence.SkillId,
                    FileName = Path.GetFileNameWithoutExtension(path)
                });
            }

            _entries.Sort((a, b) => string.CompareOrdinal(a.FileName, b.FileName));
            _templateIndex = Mathf.Clamp(_templateIndex, 0, _entries.Count);
            Repaint();
        }

        string[] BuildTemplateOptions()
        {
            string[] names = new string[_entries.Count + 1];
            names[0] = "空白轴";
            for (int i = 0; i < _entries.Count; i++)
                names[i + 1] = _entries[i].FileName;
            return names;
        }

        void ApplyGuessedSettingIfDefault()
        {
            if (_newSetting != null && !SkillWorkbenchSession.IsDefaultSeqSetting(_newSetting))
                return;
            _newSetting = SkillWorkbenchSession.GuessSetting(_newSkillId);
        }

        static bool MatchesFilter(SkillEntry entry, string filter)
        {
            if (string.IsNullOrEmpty(filter))
                return true;
            return (entry.FileName != null && entry.FileName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                || (entry.SkillId != null && entry.SkillId.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        static string AssetPathOfSelectedSequence()
        {
            GameObject go = Selection.activeObject as GameObject;
            if (go == null || go.GetComponent<FSequence>() == null)
                return null;
            string path = AssetDatabase.GetAssetPath(go);
            if (string.IsNullOrEmpty(path))
                return null;
            path = path.Replace('\\', '/');
            if (!path.StartsWith(SaveSequenceData.SequencePrefabFolder, StringComparison.OrdinalIgnoreCase))
                return null;
            return path;
        }
    }
}
