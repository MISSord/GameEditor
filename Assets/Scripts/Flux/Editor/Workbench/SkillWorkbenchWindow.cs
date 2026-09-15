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
        readonly HashSet<string> _expandedFolders = new HashSet<string>();
        double _nextRefreshAt;
        string _newSkillId = "";
        FSeqSetting _newSetting;
        int _templateIndex;
        bool _createFoldout;
        bool _helpFoldout;

        /// <summary>SkillSequences 根目录下的预制体（新建默认落这里）。</summary>
        const string RootFolder = "根目录";
        const string LegacyUncategorizedFolder = "未分类";
        const string ExpandedPrefsKey = "Flux.SkillWorkbench.ExpandedFolders";

        struct SkillEntry
        {
            public string Path;
            public string SkillId;
            public string FileName;
            public string Folder;
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
            LoadExpandedFolders();
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

            EditorGUILayout.HelpBox(
                "会保存到 " + SaveSequenceData.SequencePrefabFolder + "/{SkillId}.prefab（根目录）。已整理的轴仍在子文件夹。",
                MessageType.None);

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
                    _expandedFolders.Add(RootFolder);
                    SaveExpandedFolders();
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
            bool filtering = filter.Length > 0;

            DrawFolderSection(RootFolder, filter, filtering, alwaysShow: !filtering);

            int i = 0;
            while (i < _entries.Count)
            {
                string folder = _entries[i].Folder;
                int start = i;
                int matchCount = 0;
                while (i < _entries.Count && string.Equals(_entries[i].Folder, folder, StringComparison.Ordinal))
                {
                    if (MatchesFilter(_entries[i], filter))
                        matchCount++;
                    i++;
                }

                if (folder == RootFolder || matchCount == 0)
                    continue;

                DrawFolderBody(folder, start, i, matchCount, filter, filtering);
            }

            EditorGUILayout.EndScrollView();
        }

        void DrawFolderSection(string folder, string filter, bool filtering, bool alwaysShow)
        {
            int matchCount = 0;
            int start = -1;
            int end = _entries.Count;
            for (int k = 0; k < _entries.Count; k++)
            {
                if (_entries[k].Folder != folder)
                {
                    if (start >= 0)
                    {
                        end = k;
                        break;
                    }
                    continue;
                }

                if (start < 0)
                    start = k;
                if (MatchesFilter(_entries[k], filter))
                    matchCount++;
            }

            if (matchCount == 0 && !alwaysShow)
                return;

            DrawFolderBody(folder, start < 0 ? 0 : start, start < 0 ? 0 : end, matchCount, filter, filtering);
        }

        void DrawFolderBody(string folder, int start, int end, int matchCount, string filter, bool filtering)
        {
            bool expanded = filtering || _expandedFolders.Contains(folder);
            string label = folder == RootFolder ? RootFolder + "（新建默认）" : folder;
            if (DrawFolderRow(label, matchCount, expanded) && !filtering)
            {
                if (expanded)
                    _expandedFolders.Remove(folder);
                else
                    _expandedFolders.Add(folder);
                SaveExpandedFolders();
            }

            if (!expanded)
                return;

            if (matchCount == 0)
            {
                EditorGUILayout.LabelField("    空，新建技能会出现在这里", EditorStyles.miniLabel);
                return;
            }

            for (int k = start; k < end; k++)
            {
                SkillEntry entry = _entries[k];
                if (entry.Folder != folder || !MatchesFilter(entry, filter))
                    continue;
                DrawSkillRow(entry);
            }
        }

        static bool DrawFolderRow(string label, int count, bool expanded)
        {
            Rect rect = EditorGUILayout.GetControlRect(false, 22f);
            GUIContent content = new GUIContent(
                (expanded ? "▼  " : "▶  ") + label + "  (" + count + ")",
                EditorGUIUtility.IconContent("Folder Icon").image);
            return GUI.Button(rect, content, EditorStyles.miniButton);
        }

        void DrawSkillRow(SkillEntry entry)
        {
            bool current = SkillWorkbenchSession.IsEditing
                && string.Equals(SkillWorkbenchSession.AssetPath, entry.Path, StringComparison.OrdinalIgnoreCase);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(16f);
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

        void DrawHelp()
        {
            EditorGUILayout.Space(4);
            _helpFoldout = EditorGUILayout.Foldout(_helpFoldout, "说明", true);
            if (!_helpFoldout)
                return;

            EditorGUILayout.HelpBox(
                "列表先显示「根目录」（新建默认落这里），再按子文件夹分组。点文件夹展开 / 收回。\n"
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
                    FileName = Path.GetFileNameWithoutExtension(path),
                    Folder = FolderOf(path)
                });
            }

            _entries.Sort(CompareEntries);
            _templateIndex = Mathf.Clamp(_templateIndex, 0, _entries.Count);
            Repaint();
        }

        static int CompareEntries(SkillEntry a, SkillEntry b)
        {
            int folder = CompareFolder(a.Folder, b.Folder);
            if (folder != 0)
                return folder;
            return string.CompareOrdinal(a.FileName, b.FileName);
        }

        static int CompareFolder(string a, string b)
        {
            bool aRoot = a == RootFolder;
            bool bRoot = b == RootFolder;
            if (aRoot != bRoot)
                return aRoot ? -1 : 1;
            return string.CompareOrdinal(a, b);
        }

        static string FolderOf(string assetPath)
        {
            string root = SaveSequenceData.SequencePrefabFolder.Replace('\\', '/').TrimEnd('/');
            string path = assetPath.Replace('\\', '/');
            if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                return RootFolder;

            string relative = path.Substring(root.Length).TrimStart('/');
            int slash = relative.LastIndexOf('/');
            if (slash < 0)
                return RootFolder;
            return relative.Substring(0, slash);
        }

        void LoadExpandedFolders()
        {
            _expandedFolders.Clear();
            string raw = EditorPrefs.GetString(ExpandedPrefsKey, string.Empty);
            if (string.IsNullOrEmpty(raw))
            {
                _expandedFolders.Add(RootFolder);
                return;
            }

            string[] parts = raw.Split('|');
            for (int i = 0; i < parts.Length; i++)
            {
                if (string.IsNullOrEmpty(parts[i]))
                    continue;
                if (parts[i] == LegacyUncategorizedFolder)
                    _expandedFolders.Add(RootFolder);
                else
                    _expandedFolders.Add(parts[i]);
            }
        }

        void SaveExpandedFolders()
        {
            if (_expandedFolders.Count == 0)
            {
                EditorPrefs.DeleteKey(ExpandedPrefsKey);
                return;
            }

            var names = new string[_expandedFolders.Count];
            _expandedFolders.CopyTo(names);
            Array.Sort(names, StringComparer.Ordinal);
            EditorPrefs.SetString(ExpandedPrefsKey, string.Join("|", names));
        }

        string[] BuildTemplateOptions()
        {
            string[] names = new string[_entries.Count + 1];
            names[0] = "空白轴";
            for (int i = 0; i < _entries.Count; i++)
                names[i + 1] = _entries[i].Folder + "/" + _entries[i].FileName;
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
            if (entry.Folder != null && entry.Folder.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
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
