using Flux;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using static PlasticPipe.Server.MonitorStats;
using static UnityEditor.VersionControl.Asset;

namespace FluxEditor
{
    public class FSequenceWindowHeader
    {
        // padding on top, bottom, left and right
        public const float PADDING = 5;

        // space between labels and the fields
        public const float LABEL_SPACE = 5;

        // space between elements (i.e. label+field pairs)
        public const float ELEMENT_SPACE = 20;

        // height of the header
        public const float HEIGHT_HEADER = HEIGHT_ONE * 2;

        public const float HEIGHT_ONE = HEIGHT_ONEINE + PADDING + PADDING;

        public const float HEIGHT_ONEINE = 20;

        //public const float HEIGHT_Field = HEIGHT_ONEINE;

        private const float MAX_SEQUENCE_POPUP_WIDTH = 220;
        private const float UPDATE_MODE_FIELD_WIDTH = 100;
        private const float FRAMERATE_FIELD_WIDTH = 40;
        private const float LENGTH_FIELD_WIDTH = 100;

        // window this header belongs to
        private FSequenceEditorWindow _sequenceWindow;

        private SerializedObject _sequenceSO;

        private SerializedProperty _sequenceUpdateMode;
        private SerializedProperty _sequenceLength;
        private SerializedProperty _sequenceSkillId;
        //private SerializedProperty _sequenceSpeed;

        private SerializedProperty _sequenceSetting;

        // sequence selection popup variables
        private GUIContent _sequenceLabel = new GUIContent("Sequence", "Select Sequence...");
        private GUIContent _seqSettigLabel = new GUIContent("SeqSetting", "SeqSetting");

        // rect of the sequence label
        private Rect _sequenceLabelRect;

        // rect of the sequence name
        private Rect _sequencePopupRect;

        // rect of the sequence label
        private Rect _seqSettingLabelRect;

        // rect of the sequence name
        private Rect _seqSettingRect;

        // rect for the button to create a new sequence
        private Rect _sequenceAddButtonRect;

        private FSequence[] _sequences;

        private GUIContent[] _sequenceNames;

        private int _selectedSequenceIndex;

        // update mode UI variables
        private GUIContent _updateModeLabel = new GUIContent("Update Mode", "How does the sequence update:\n\tNormal: uses Time.time in Update()\n\tAnimatePhysics: uses Time.fixedTime in FixedUpdate()\n\tUnscaledTime: uses Time.unscaledTime in Update()");
        private Rect _updateModeLabelRect;
        private Rect _updateModeFieldRect;
        private bool _showUpdadeMode;

        // framerate UI variables
        private GUIContent _framerateLabel = new GUIContent("Frame Rate", "How many frames does the sequence have per second");
        private Rect _framerateLabelRect;
        private Rect _framerateFieldRect;
        private bool _showFramerate;

        // length UI variables
        private GUIContent _lengthLabel = new GUIContent("Length", "What's the length of the sequence");
        private Rect _lengthLabelRect;
        private Rect _lengthFieldRect;
        private bool _showLength;

        private GUIContent _skillIdLabel = new GUIContent("SkillId", "SkillId");
        private Rect _skillIdLabelRect;
        private Rect _skillIdFieldRect;


        private GUIContent _addContainerLabel = new GUIContent(string.Empty, "Add Container To Sequence");
        private Rect _addContainerRect;
        private bool _showAddContainer;

        private GUIContent _openInspectorLabel = new GUIContent(string.Empty, "Open Flux Inspector");
        private Rect _openInspectorRect;

        private GUIContent _savaDataLabel = new GUIContent(string.Empty, "_savaData");
        private Rect _savaDataRect;

        // cached number field style, since we want numbers centered
        private GUIStyle _numberFieldStyle;

        public FSequenceWindowHeader(FSequenceEditorWindow sequenceWindow)
        {
            _sequenceWindow = sequenceWindow;

            RebuildSequenceList();

            EditorApplication.hierarchyWindowChanged += OnHierarchyChanged;

            _addContainerLabel.image = FUtility.GetFluxTexture("AddFolder.png");
            _openInspectorLabel.image = FUtility.GetFluxTexture("Inspector.png");
            _savaDataLabel.image = FUtility.GetFluxTexture("Save.png");

        }

        private void OnHierarchyChanged()
        {
            RebuildSequenceList();
        }

        private void RebuildSequenceList()
        {
            FSequence[] found = GameObject.FindObjectsOfType<FSequence>();
            System.Array.Sort<FSequence>(found, delegate (FSequence x, FSequence y) { return x.name.CompareTo(y.name); });

            List<FSequence> sceneSequences = new List<FSequence>(found.Length);
            for (int i = 0; i != found.Length; ++i)
            {
                if (found[i] != null && found[i].gameObject.scene.IsValid())
                    sceneSequences.Add(found[i]);
            }
            _sequences = sceneSequences.ToArray();

            _sequenceNames = new GUIContent[_sequences.Length + 2];
            for (int i = 0; i != _sequences.Length; ++i)
            {
                _sequenceNames[i] = new GUIContent(_sequences[i].name);
            }
            _sequenceNames[_sequenceNames.Length - 2] = new GUIContent("null");
            _sequenceNames[_sequenceNames.Length - 1] = new GUIContent("[Create New Sequence]");


            _selectedSequenceIndex = -1;
        }

        float FieldHigh = 0;


        public void RebuildLayout(Rect rect)
        {
            rect.xMin += PADDING;
            rect.yMin += PADDING;
            rect.xMax -= PADDING;
            rect.yMax -= PADDING;

            float width = rect.width;

            SetRectY(0, ref rect);

            _openInspectorRect = rect;
            _savaDataRect = rect;

            _updateModeLabelRect = _updateModeFieldRect = rect;
            _framerateLabelRect = _framerateFieldRect = rect;
            _lengthLabelRect = _lengthFieldRect = rect;

            _updateModeLabelRect.width = EditorStyles.label.CalcSize(_updateModeLabel).x + LABEL_SPACE;
            _updateModeFieldRect.width = UPDATE_MODE_FIELD_WIDTH ;

            _framerateLabelRect.width = EditorStyles.label.CalcSize(_framerateLabel).x + LABEL_SPACE;
            _framerateFieldRect.width = FRAMERATE_FIELD_WIDTH;

            _lengthLabelRect.width = EditorStyles.label.CalcSize(_lengthLabel).x + LABEL_SPACE;
            _lengthFieldRect.width = LENGTH_FIELD_WIDTH*0.6f ;


            _sequenceLabelRect = rect;
            _sequenceLabelRect.width = EditorStyles.label.CalcSize(_sequenceLabel).x + LABEL_SPACE;
            //SetRectY(0, ref _sequenceLabelRect);


            _sequencePopupRect = rect;
            //SetRectY(0, ref _sequencePopupRect);
            _sequencePopupRect.xMin = _sequenceLabelRect.xMax;
            _sequencePopupRect.width = Mathf.Min(width - _sequenceLabelRect.width, MAX_SEQUENCE_POPUP_WIDTH);
            //			Debug.Log( _sequenceNameRect.width );

            _sequenceAddButtonRect = rect;
            _sequenceAddButtonRect.xMin = _sequencePopupRect.xMax + LABEL_SPACE;
            _sequenceAddButtonRect.width = 16;

            float reminderWidth = width - _sequenceAddButtonRect.xMax;

            _addContainerRect = new Rect(0, 3, 22, 22);
            _addContainerRect.x = _sequencePopupRect.xMax + LABEL_SPACE;

            reminderWidth -= (ELEMENT_SPACE + _addContainerRect.width);

            _showAddContainer = reminderWidth >= 0;

            _openInspectorRect.xMin = _openInspectorRect.xMax - 22;
            _savaDataRect.xMax = _openInspectorRect.xMin - 4;
            _savaDataRect.xMin = _savaDataRect.xMax - 24;


            _lengthFieldRect.x = rect.xMax - 50 - PADDING - _lengthFieldRect.width -10;
            _lengthLabelRect.x = _lengthFieldRect.xMin - _lengthLabelRect.width;
            _lengthFieldRect.height = HEIGHT_ONEINE;

            reminderWidth -= (ELEMENT_SPACE + _lengthLabelRect.width + _lengthFieldRect.width);

            _showLength = reminderWidth >= 0;

            _framerateFieldRect.x = _lengthLabelRect.xMin - ELEMENT_SPACE - _framerateFieldRect.width;
            _framerateLabelRect.x = _framerateFieldRect.xMin - _framerateLabelRect.width;

            reminderWidth -= (_framerateLabelRect.width + _framerateFieldRect.width + ELEMENT_SPACE);

            _showFramerate = reminderWidth >= 0;
            //位: updateMode
            _updateModeFieldRect.x = _framerateLabelRect.xMin - ELEMENT_SPACE - _updateModeFieldRect.width;
            _updateModeLabelRect.x = _updateModeFieldRect.xMin - _updateModeLabelRect.width;


            reminderWidth -= (_updateModeLabelRect.width + _updateModeFieldRect.width + ELEMENT_SPACE);

            _showUpdadeMode = reminderWidth >= 0;

            SetRectY(1, ref rect);
            FieldHigh = rect.height - PADDING;

            _seqSettingLabelRect = rect;
            _seqSettingLabelRect.width = EditorStyles.label.CalcSize(_seqSettigLabel).x + LABEL_SPACE;

            _seqSettingRect = rect;
            _seqSettingRect.xMin = _seqSettingLabelRect.xMax;
            _seqSettingRect.width = Mathf.Min(width - _seqSettingLabelRect.width, MAX_SEQUENCE_POPUP_WIDTH*0.8f);
            _seqSettingRect.height = HEIGHT_ONEINE;

            _skillIdLabelRect  = rect;
            _skillIdLabelRect.xMin = _seqSettingLabelRect.xMax + _seqSettingRect.width + LABEL_SPACE + LABEL_SPACE + LABEL_SPACE;
            _skillIdLabelRect.width = EditorStyles.label.CalcSize(_skillIdLabel).x + LABEL_SPACE;


            _skillIdFieldRect = _skillIdLabelRect;
            _skillIdFieldRect.xMin = _skillIdLabelRect.xMax;
            _skillIdFieldRect.width = LENGTH_FIELD_WIDTH *1.5f;
            _skillIdFieldRect.height = HEIGHT_ONEINE;


            _numberFieldStyle = new GUIStyle(EditorStyles.numberField);
            _numberFieldStyle.alignment = TextAnchor.MiddleCenter;
        }

        private void SetRectY(int i, ref Rect rect)
        {

            rect.yMin = PADDING + (HEIGHT_ONEINE + PADDING) * i;
            rect.yMax = PADDING + (HEIGHT_ONEINE + PADDING) * (i + 1);

        }

        public void OnGUI()
        {
            FSequenceEditor sequenceEditor = _sequenceWindow.GetSequenceEditor();
            if (sequenceEditor == null)
                return;

            FSequence sequence = sequenceEditor.Sequence;

            if ((_selectedSequenceIndex < 0 && sequence != null) || (_selectedSequenceIndex >= 0 && _selectedSequenceIndex < _sequences.Length && _sequences[_selectedSequenceIndex] != sequence))
            {
                _selectedSequenceIndex = -1;
                for (int i = 0; i != _sequences.Length; ++i)
                {
                    if (_sequences[i] == sequence)
                    {
                        _selectedSequenceIndex = i;
                        break;
                    }
                }
            }

            bool currentNotInList = sequence != null && _selectedSequenceIndex < 0;
            GUIContent[] popupNames = _sequenceNames;
            int popupIndex = _selectedSequenceIndex;
            if (currentNotInList)
            {
                popupNames = new GUIContent[_sequenceNames.Length + 1];
                popupNames[0] = new GUIContent(sequence.name);
                for (int i = 0; i < _sequenceNames.Length; ++i)
                    popupNames[i + 1] = _sequenceNames[i];
                popupIndex = 0;
            }

            if (Event.current.type == EventType.MouseDown && Event.current.alt && _sequencePopupRect.Contains(Event.current.mousePosition))
            {
                Selection.activeObject = sequence;
                Event.current.Use();
            }

            EditorGUI.BeginChangeCheck();
            EditorGUI.PrefixLabel(_sequenceLabelRect, _sequenceLabel);
            int newSequenceIndex = EditorGUI.Popup(_sequencePopupRect, popupIndex, popupNames);
            if (EditorGUI.EndChangeCheck())
            {
                if (currentNotInList)
                {
                    if (newSequenceIndex == 0)
                    {
                        EditorGUIUtility.keyboardControl = 0;
                        EditorGUIUtility.ExitGUI();
                        return;
                    }
                    newSequenceIndex -= 1;
                }

                if (newSequenceIndex == _sequenceNames.Length - 1)
                {
                    FSequence newSequence = FSequenceEditorWindow.CreateSequence();
                    Selection.activeTransform = newSequence.transform;
                    sequenceEditor.OpenSequence(newSequence);
                }
                else if (newSequenceIndex == _sequenceNames.Length - 2)
                {
                    sequenceEditor.OpenSequence(null);
                }
                else
                {
                    _selectedSequenceIndex = newSequenceIndex;
                    FSequence fSequence = _sequences[_selectedSequenceIndex];
                    sequenceEditor.OpenSequence(fSequence);
                    _sequenceWindow.RemoveNotification();
                }
                EditorGUIUtility.keyboardControl = 0; // deselect it
                EditorGUIUtility.ExitGUI();
            }

            // if we're in play mode, can't change anything
            if (Application.isPlaying)
                GUI.enabled = false;

            if (sequence == null)
                return;


      

            if (_sequenceSO == null || _sequenceSO.targetObject != sequence)
            {
                _sequenceSO = new SerializedObject(sequence);
            }
            _sequenceUpdateMode = _sequenceSO.FindProperty("_updateMode");
            _sequenceLength = _sequenceSO.FindProperty("_length");
            _sequenceSkillId = _sequenceSO.FindProperty("_skillId");
            _sequenceSetting = _sequenceSO.FindProperty("_fSeqSetting");

            //_sequenceSO.Update();

            if (_showUpdadeMode)
            {
                EditorGUI.PrefixLabel(_updateModeLabelRect, _updateModeLabel);
                EditorGUI.PropertyField(_updateModeFieldRect, _sequenceUpdateMode, GUIContent.none);
            }

            if (_showFramerate)
            {
                EditorGUI.PrefixLabel(_framerateLabelRect, _framerateLabel);
                EditorGUI.BeginChangeCheck();
                int newFrameRate = FGUI.FrameRatePopup(_framerateFieldRect, sequence.FrameRate);
                if (EditorGUI.EndChangeCheck())
                {
                    if (newFrameRate == -1)
                    {
                        FChangeFrameRateWindow.Show(new Vector2(_framerateLabelRect.xMin, _framerateLabelRect.yMax), sequence, FSequenceInspector.Rescale);
                    }
                    else
                    {
                        FSequenceInspector.Rescale(sequence, newFrameRate, true);
                    }
                }
            }

            if (_showLength)
            {
                EditorGUI.PrefixLabel(_lengthLabelRect, _lengthLabel);
                _sequenceLength.intValue = Mathf.Clamp(EditorGUI.IntField(_lengthFieldRect, _sequenceLength.intValue, _numberFieldStyle), 1, int.MaxValue);
            }
            EditorGUI.PrefixLabel(_skillIdLabelRect, _skillIdLabel);
            _sequenceSkillId.stringValue = EditorGUI.TextField(_skillIdFieldRect, _sequenceSkillId.stringValue);


            GUIStyle s = new GUIStyle(EditorStyles.miniButton);
            s.padding = new RectOffset(1, 1, 1, 1);

            if (_showAddContainer)
            {
                if (FGUI.Button(_addContainerRect, _addContainerLabel))
                {
                    AddContainer();
                }
            }

            if (FGUI.Button(_savaDataRect, _savaDataLabel))
            {
                SaveSequenceData.TrySaveCurrentSequence();
            }

            if (FGUI.Button(_openInspectorRect, _openInspectorLabel))
            {
                FInspectorWindow.Open();
            }

            EditorGUI.PrefixLabel(_seqSettingLabelRect, _seqSettigLabel);

            EditorGUI.PropertyField(_seqSettingRect, _sequenceSetting, GUIContent.none);

            _sequenceSO.ApplyModifiedProperties();

            GUI.enabled = true;
        }

        private void AddContainer()
        {
            GenericMenu menu = new GenericMenu();

            bool hasDefaultContainers = false;

            List<FColorSetting> defaultContainers = FUtility.GetSettings().DefaultContainers;
            foreach (FColorSetting colorSetting in defaultContainers)
            {
                if (string.IsNullOrEmpty(colorSetting._str))
                    continue;

                menu.AddItem(new GUIContent(colorSetting._str), false, CreateContainer, colorSetting);
                hasDefaultContainers = true;
            }

            if (!hasDefaultContainers)
            {
                _sequenceWindow.GetSequenceEditor().CreateContainer(new FColorSetting("Default", FGUI.DefaultContainerColor()));
                return;
            }

            menu.AddSeparator(null);

            menu.AddItem(new GUIContent("[Default New Container]"), false, CreateContainer, new FColorSetting("Default", FGUI.DefaultContainerColor()));

            menu.ShowAsContext();
        }

        private void CreateContainer(object data)
        {
            _sequenceWindow.GetSequenceEditor().CreateContainer((FColorSetting)data);
        }
    }
}
