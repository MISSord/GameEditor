using UnityEngine;
using UnityEditor;
using Flux;
using EGamePlay.Combat;

namespace FluxEditor
{
    [CustomEditor(typeof(FSwitchEvent))]
    public class FSwitchEventInspector : FEventInspector
    {
        FSwitchEvent targetEvent;

        private SerializedProperty _toFrame = null;
        private SerializedProperty _swFrame = null;
        private SerializedProperty _unMoveFrames = null;

        protected override void OnEnable()
        {
            base.OnEnable();
            targetEvent = target as FSwitchEvent;

            _toFrame = serializedObject.FindProperty("_toFrame");
            _swFrame = serializedObject.FindProperty("switchFrame");
            _unMoveFrames = serializedObject.FindProperty("unMoveFrames");
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            serializedObject.Update();
            if (targetEvent)
            {
                switch (targetEvent.InputType)
                {
                    //case InputEventType.Switch:
                    //    EditorGUILayout.PropertyField(_toFrame,new GUIContent("目的切换帧"));
                    //    EditorGUILayout.PropertyField(_swFrame,new GUIContent("触发帧"));
                    //    break;
                    case EventTriggerType.Exit:
                        break;
                    case EventTriggerType.Finish:
                        EditorGUILayout.PropertyField(_unMoveFrames, new GUIContent("禁止移动帧数"));
                        break;
                    default:
                        break;
                }
            }
            //Repaint();
            serializedObject.ApplyModifiedProperties();
        }
    }
}
