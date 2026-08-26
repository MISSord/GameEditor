using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

//using UnityEditorInternal;

using System.Collections.Generic;

using Flux;
//using FluxEditor;

namespace FluxEditor
{
    [InitializeOnLoad]
    internal static class FAnimationPreviewBridgeRegistration
    {
        static FAnimationPreviewBridgeRegistration()
        {
            FAnimationPreviewBridge.ResolvePreviewController = source =>
            {
                AnimatorController controller = source as AnimatorController;
                return controller != null ? FAnimationTrackInspector.GetMutablePreviewController(controller) : source;
            };
        }
    }

    [CustomEditor(typeof(FAnimationTrack))]
    public class FAnimationTrackInspector : FTrackInspector
    {

        private const string FLUX_STATE_MACHINE_NAME = "FluxStateMachine";
        private const string PreviewControllerPath = "Assets/Res/Animator/_FluxPreview.controller";
        private const string PreviewSourcePrefKey = "Flux.PreviewControllerSource";

        public FAnimationTrack _animTrack = null;

        private SerializedProperty _animatorController = null;

        private SerializedProperty _layerName = null;

        private SerializedProperty _layerId = null;

        public override void OnEnable()
        {
            base.OnEnable();

            if (target == null)
            {
                DestroyImmediate(this);
                return;
            }
            _animTrack = (FAnimationTrack)target;
            _animatorController = serializedObject.FindProperty("_animatorController");
            _layerName = serializedObject.FindProperty("_layerName");
            _layerId = serializedObject.FindProperty("_layerId");
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            serializedObject.Update();

            if (_animatorController.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox("There's no Animator Controller", MessageType.Warning);
                Rect helpBoxRect = GUILayoutUtility.GetLastRect();

                float yCenter = helpBoxRect.center.y;

                helpBoxRect.xMax -= 3;
                helpBoxRect.xMin = helpBoxRect.xMax - 50;
                helpBoxRect.yMin = yCenter - 12.5f;
                helpBoxRect.height = 25f;

                if (GUI.Button(helpBoxRect, "Create"))
                {
                    AnimatorController newController = CreateAnimatorController(_animTrack);
                    if (newController)
                        _animatorController.objectReferenceValue = newController;

                    UpdateLayer(newController == null ? null : newController.layers[0]);
                }
            }

            AnimatorController prevAnimatorController = (AnimatorController)_animatorController.objectReferenceValue;

            EditorGUILayout.PropertyField(_animatorController);

            AnimatorController controller = (AnimatorController)_animatorController.objectReferenceValue;

            if (controller != prevAnimatorController)
            {
                AnimatorControllerLayer layer = controller == null ? null : controller.layers[0];
                if (layer != null && (layer.stateMachine.states.Length > 0 || layer.stateMachine.stateMachines.Length > 0))
                    layer = null;
                UpdateLayer(layer);
            }
            #region 卡性能 去掉
            //if (controller != null)
            //{
            //    int len = controller.layers.Length;
            //    string[] layers = new string[len];
            //    int layerIndex = -1;
            //    for (int i = 0; i != len; ++i)
            //    {
            //        layers[i] = controller.layers[i].name;
            //        if (layers[i] == _layerName.stringValue)
            //            layerIndex = i;
            //    }

            //    if (layerIndex != _layerId.intValue) // has it, but it got moved
            //    {
            //        if (layerIndex == -1 && controller.layers.Length > 0)
            //            layerIndex = 0;

            //        UpdateLayer(controller.layers[layerIndex]);
            //    }

            //    EditorGUI.BeginChangeCheck();
            //    layerIndex = EditorGUILayout.Popup("State Machine Layer", layerIndex, layers);
            //    if (EditorGUI.EndChangeCheck())
            //    {
            //        // choosing existing one
            //        if (layerIndex > -1)
            //        {
            //            if (VerifyUseAnimatorControllerLayer(controller.layers[layerIndex]))
            //            {
            //                UpdateLayer(controller.layers[layerIndex]);
            //            }
            //        }
            //    }
            //}
            #endregion

            if (GUILayout.Button("ReBuild Anim StateMachine"))
            {
                FAnimationTrackInspector.RebuildStateMachine((FAnimationTrack)_animTrack);
            }

            serializedObject.ApplyModifiedProperties();
        }



        public void UpdateLayer(AnimatorControllerLayer layer)
        {
            if (layer == null)
            {
                _layerName.stringValue = null;
                _layerId.intValue = -1;
            }
            else
            {
                AnimatorController controller = (AnimatorController)_animatorController.objectReferenceValue;
                for (int i = 0; i != controller.layers.Length; ++i)
                {
                    if (controller.layers[i].name == layer.name)
                    {
                        _layerName.stringValue = layer.name;
                        _layerId.intValue = i;
                        return;
                    }
                }
                // shouldn't get here..
                Debug.LogError("Trying to set a layer that doesn't belong to the controller");
                UpdateLayer(null);
            }
        }

        /// <summary>
        /// Rebuild Flux preview states on a cloned controller so the project AnimatorController is not modified.
        /// </summary>
        public static void RebuildStateMachine(FAnimationTrack track)
        {
            if (track == null || track.Owner == null || track.AnimatorController == null || track.LayerId == -1)
                return;

            bool isPreviewing = track.HasCache;

            if (isPreviewing)
                track.ClearCache();

            Animator animator = track.Owner.GetComponent<Animator>();
            if (animator == null)
                return;

            animator.runtimeAnimatorController = null;

            AnimatorController controller = GetMutablePreviewController((AnimatorController)track.AnimatorController);
            if (controller == null)
                return;

            track.UpdateEventIds();

            AnimatorControllerLayer layer = FindLayer(controller, track.LayerName);

            if (layer == null)
            {
                layer = controller.layers[0];
            }

            AnimatorStateMachine fluxSM = FindStateMachine(layer.stateMachine, FLUX_STATE_MACHINE_NAME);

            if (fluxSM == null)
            {
                fluxSM = layer.stateMachine.AddStateMachine(FLUX_STATE_MACHINE_NAME);

                ChildAnimatorStateMachine[] stateMachines = layer.stateMachine.stateMachines;

                for (int i = 0; i != stateMachines.Length; ++i)
                {
                    if (stateMachines[i].stateMachine == fluxSM)
                    {
                        stateMachines[i].position = layer.stateMachine.entryPosition + new Vector3(300, 0, 0);
                        break;
                    }
                }
                layer.stateMachine.stateMachines = stateMachines;
            }

            List<FEvent> events = track.Events;

            if (fluxSM.states.Length > events.Count)
            {
                for (int i = fluxSM.states.Length - 1; i >= events.Count; --i)
                {
                    AnimatorState extra = fluxSM.states[i].state;
                    if (extra == null)
                        continue;
                    AnimatorStateTransition[] extraTransitions = extra.transitions;
                    for (int t = extraTransitions.Length - 1; t >= 0; --t)
                        extra.RemoveTransition(extraTransitions[t]);
                    fluxSM.RemoveState(extra);
                }
            }
            else if (fluxSM.states.Length < events.Count)
            {
                for (int i = fluxSM.states.Length; i < events.Count; ++i)
                    fluxSM.AddState(i.ToString());
            }

            AnimatorState lastState = null;
            Vector3 pos = fluxSM.entryPosition + new Vector3(300, 0, 0);

            Vector3 statePosDelta = new Vector3(0, 80, 0);

            ChildAnimatorState[] states = fluxSM.states;

            for (int i = 0; i != events.Count; ++i)
            {
                FPlayAnimationEvent animEvent = (FPlayAnimationEvent)events[i];

                if (animEvent._animationClip == null) // dump events without animations
                    continue;

                states[i].position = pos;

                AnimatorState state = states[i].state;

                state.name = i.ToString();

                pos += statePosDelta;

                state.motion = animEvent._animationClip;

                state.speed = animEvent._speed;

                animEvent._stateHash = Animator.StringToHash(state.name);

                if (lastState)
                {
                    AnimatorStateTransition[] lastStateTransitions = lastState.transitions;

                    if (animEvent.IsBlending())
                    {
                        AnimatorStateTransition transition = null;

                        for (int j = lastStateTransitions.Length - 1; j > 0; --j)
                        {
                            lastState.RemoveTransition(lastStateTransitions[j]);
                        }

                        transition = lastStateTransitions.Length == 1 ? lastStateTransitions[0] : lastState.AddTransition(state);

                        transition.offset = (animEvent._startOffset / animEvent._animationClip.frameRate) / animEvent._animationClip.length;

                        FPlayAnimationEvent prevAnimEvent = (FPlayAnimationEvent)events[i - 1];

                        animEvent._startOffset = Mathf.Clamp(Mathf.RoundToInt(transition.offset * animEvent._animationClip.length * animEvent._animationClip.frameRate), 0, animEvent._animationClip.isLooping ? animEvent.Length : Mathf.RoundToInt(animEvent._animationClip.length * animEvent._animationClip.frameRate) - animEvent.Length);
                        transition.offset = (animEvent._startOffset / animEvent._animationClip.frameRate) / animEvent._animationClip.length;
                        EditorUtility.SetDirty(animEvent);

                        transition.duration = (animEvent._blendLength / prevAnimEvent._animationClip.frameRate) / prevAnimEvent._animationClip.length;

                        transition.hasExitTime = true;
                        transition.exitTime = (prevAnimEvent.Length + prevAnimEvent._startOffset) / (prevAnimEvent._animationClip.length * prevAnimEvent._animationClip.frameRate);

                        for (int j = transition.conditions.Length - 1; j >= 0; --j)
                        {
                            transition.RemoveCondition(transition.conditions[j]);
                        }

                        //						AnimatorCondition condition = transition.conditions[0];
                        //						condition.threshold = 0;
                    }
                    else // animations not blending, needs hack for animation previewing
                    {
                        for (int j = lastStateTransitions.Length - 1; j >= 0; --j)
                        {
                            lastState.RemoveTransition(lastStateTransitions[j]);
                        }
                    }
                }

                lastState = state;
            }

            fluxSM.states = states;

            if (fluxSM.states.Length > 0)
                layer.stateMachine.defaultState = fluxSM.states[0].state;

            animator.runtimeAnimatorController = controller;

            if (isPreviewing)
                track.CreateCache();
        }

        public static AnimatorStateTransition GetTransitionTo(FPlayAnimationEvent animEvt)
        {
            FAnimationTrack animTrack = (FAnimationTrack)animEvt.Track;

            if (animTrack.AnimatorController == null)
                return null;

            AnimatorController controller = GetMutablePreviewController((AnimatorController)animTrack.AnimatorController);

            AnimatorState animState = null;

            AnimatorControllerLayer layer = FindLayer(controller, ((FAnimationTrack)animEvt.Track).LayerName);

            if (layer == null)
                return null;

            if (layer.stateMachine.stateMachines.Length == 0)
                return null;

            ChildAnimatorStateMachine fluxSM = layer.stateMachine.stateMachines[0];

            AnimatorStateMachine stateMachine = fluxSM.stateMachine;

            for (int i = 0; i != stateMachine.states.Length; ++i)
            {
                if (stateMachine.states[i].state.nameHash == animEvt._stateHash)
                {
                    animState = stateMachine.states[i].state;
                    break;
                }
            }

            if (animState == null)
            {
                //				Debug.LogError("Couldn't find state " + animEvt._animationClip );
                return null;
            }

            for (int i = 0; i != stateMachine.states.Length; ++i)
            {
                AnimatorState state = stateMachine.states[i].state;

                AnimatorStateTransition[] transitions = state.transitions;
                for (int j = 0; j != transitions.Length; ++j)
                {
                    if (transitions[j].destinationState == animState)
                    {
                        return transitions[j];
                    }
                }
            }

            return null;
        }

        public static AnimatorController CreateAnimatorController(FAnimationTrack animTrack)
        {
            string defaultFolder = "Assets/";
            string defaultFileName = string.Format("{0}_{1}.controller", animTrack.Timeline.Sequence.name, animTrack.Owner.name);
            string defaultFilePath = AssetDatabase.GenerateUniqueAssetPath(defaultFolder + defaultFileName);
            defaultFileName = System.IO.Path.GetFileNameWithoutExtension(defaultFilePath);

            string filePath = EditorUtility.SaveFilePanel("Create new AnimatorController...", defaultFolder, defaultFileName, "controller");

            if (string.IsNullOrEmpty(filePath))
                return null;

            // transform the path into a local path
            if (!filePath.StartsWith(Application.dataPath))
            {
                Debug.LogError("Cannot save controller outside of the project");
                return null;
            }

            string fileLocalPath = "Assets" + filePath.Substring(Application.dataPath.Length, filePath.Length - Application.dataPath.Length);

            //			AnimatorController animatorAtPath = (AnimatorController)AssetDatabase.LoadAssetAtPath( fileLocalPath, typeof(AnimatorController) );

            return AnimatorController.CreateAnimatorControllerAtPath(fileLocalPath);
        }

        /// <summary>
        /// Returns a writable preview clone. Project AnimatorController assets are copied once.
        /// </summary>
        public static AnimatorController GetMutablePreviewController(AnimatorController source)
        {
            if (source == null)
                return null;

            string srcPath = AssetDatabase.GetAssetPath(source);
            if (string.IsNullOrEmpty(srcPath) || srcPath == PreviewControllerPath)
                return source;

            AnimatorController existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(PreviewControllerPath);
            string lastSrc = EditorPrefs.GetString(PreviewSourcePrefKey, string.Empty);
            if (existing == null || lastSrc != srcPath)
            {
                if (!string.IsNullOrEmpty(AssetDatabase.GetAssetPath(existing)))
                    AssetDatabase.DeleteAsset(PreviewControllerPath);

                if (!AssetDatabase.CopyAsset(srcPath, PreviewControllerPath))
                    return null;

                EditorPrefs.SetString(PreviewSourcePrefKey, srcPath);
                existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(PreviewControllerPath);
            }

            return existing != null ? existing : source;
        }

        /// <summary>
        /// Ensure preview controller and cache are ready before editor playback.
        /// </summary>
        public static void EnsurePreviewReady(FAnimationTrack track)
        {
            if (track == null || track.Owner == null || !track.CanCreateCache())
                return;

            RebuildStateMachine(track);

            if (!track.HasCache)
                track.CreateCache();
        }

        /// <summary>
        /// Restore scene Animators from the preview clone back to the track's project controller.
        /// </summary>
        public static void RestorePreviewAnimators(FSequence sequence)
        {
            if (sequence == null)
                return;

            FAnimationTrack.DeleteAnimationPreviews(sequence);
            for (int c = 0; c < sequence.Containers.Count; ++c)
            {
                List<FTimeline> timelines = sequence.Containers[c].Timelines;
                for (int t = 0; t < timelines.Count; ++t)
                {
                    FTimeline timeline = timelines[t];
                    if (timeline == null || timeline.Owner == null)
                        continue;

                    Animator animator = timeline.Owner.GetComponent<Animator>();
                    if (animator == null)
                        animator = timeline.Owner.GetComponentInChildren<Animator>();
                    if (animator == null)
                        continue;

                    RuntimeAnimatorController original = null;
                    List<FTrack> tracks = timeline.Tracks;
                    for (int k = 0; k < tracks.Count; ++k)
                    {
                        FAnimationTrack animTrack = tracks[k] as FAnimationTrack;
                        if (animTrack != null && animTrack.AnimatorController != null)
                        {
                            original = animTrack.AnimatorController;
                            break;
                        }
                    }

                    if (original != null)
                        animator.runtimeAnimatorController = original;
                }
            }
        }

        private static bool VerifyUseAnimatorControllerLayer(AnimatorControllerLayer layer)
        {
            if (layer == null || (layer.stateMachine.states.Length == 0 && layer.stateMachine.stateMachines.Length == 0))
                return true;

            return EditorUtility.DisplayDialog("Animator Controller's Layer isn't empty!",
                                               "Layer '" + layer.name + "' is not empty, and Flux will change it.\n"
                                               + "Are you sure you want to use this layer?", "Use", "Cancel");
        }

        private static AnimatorControllerParameter FindParameter(AnimatorController controller, string paramName)
        {
            foreach (AnimatorControllerParameter p in controller.parameters)
            {
                if (p.name == paramName)
                    return p;
            }

            return null;
        }

        private static AnimatorControllerLayer FindLayer(AnimatorController controller, string layerName)
        {
            foreach (AnimatorControllerLayer layer in controller.layers)
            {
                if (layer.name == layerName)
                    return layer;
            }

            return null;
        }

        private static AnimatorStateMachine FindStateMachine(AnimatorStateMachine stateMachine, string stateMachineName)
        {
            foreach (ChildAnimatorStateMachine sm in stateMachine.stateMachines)
            {
                if (sm.stateMachine.name == stateMachineName)
                    return sm.stateMachine;
            }

            return null;
        }
    }
}
