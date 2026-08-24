using Flux;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Random = UnityEngine.Random;

namespace FluxEditor
{
    /// <summary>Sequence 导出时的动画片段收集与 Animator Controller 写入。</summary>
    internal static class SaveSequenceAnimExporter
    {
        internal static readonly List<FPlayAnimationEvent> AnimEvents = new List<FPlayAnimationEvent>();

        public static void Clear() => AnimEvents.Clear();

        public static void CollectAnimEvents(FSequence sequence)
        {
            if (sequence.Containers == null || sequence.Containers.Count == 0)
                return;

            var timeline = sequence.Containers[0].Timelines[0];
            if (timeline?.Tracks == null || timeline.Tracks.Count == 0)
                return;

            var track = timeline.Tracks[0];
            if (track is not FAnimationTrack)
                return;

            foreach (var ev in track.Events)
            {
                if (ev is FPlayAnimationEvent fEvent)
                    AnimEvents.Add(fEvent);
            }
        }

        public static void CheckMoveAnim(FSequence sequence)
        {
            Clear();
            CollectAnimEvents(sequence);

            foreach (var item in AnimEvents)
            {
                string path = AssetDatabase.GetAssetPath(item._animationClip);
                string newPath = "Assets/_Res/Anim/Using/" + Path.GetFileName(path);
                AssetDatabase.MoveAsset(path, newPath);
            }
        }

        public static void CheckAddAnim(FSequence sequence)
        {
            AnimatorController ac = sequence.FSeqSetting.targetAnimtorController as AnimatorController;
            if (ac == null)
            {
                Debug.LogWarning($"FSeqSetting.targetAnimtorController 为空，已跳过写入 Animator。Sequence={sequence.name}");
                return;
            }

            bool ischage = false;
            var otherSpeedDic = new Dictionary<string, float>();
            AnimatorStateMachine sm = ac.layers[0].stateMachine;

            foreach (var item in AnimEvents)
            {
                if (!ac.animationClips.Contains(item._animationClip))
                {
                    AnimatorState state = sm.AddState(item._animationClip.name, sm.exitPosition + Vector3.up * Random.Range(100, 800));
                    state.motion = item._animationClip;
                    state.speed = item._speed;
                    if (state.tag != "NoExit")
                        state.AddExitTransition(true);

                    ischage = true;
                    Debug.Log($"anim add {item._animationClip.name}");
                }
                else
                {
                    otherSpeedDic[item._animationClip.name] = item._speed;
                }
            }

            foreach (var item in sm.states)
            {
                if (otherSpeedDic.TryGetValue(item.state.name, out float speed)
                    && !Mathf.Approximately(item.state.speed, speed))
                {
                    item.state.speed = speed;
                    ischage = true;
                }
            }

            if (ischage)
                EditorUtility.SetDirty(ac);
        }
    }
}
