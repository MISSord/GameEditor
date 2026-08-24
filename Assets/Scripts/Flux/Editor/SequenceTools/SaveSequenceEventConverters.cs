using ACTGameEditor;
using Flux;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FluxEditor
{
    /// <summary>Flux 事件与 ACTGameEditor 运行时事件数据之间的转换。</summary>
    internal static class SaveSequenceEventConverters
    {
        public static XCEffectEventData TOXCEffectEvent(FPlayTagEvent fe)
        {
            var xce = new XCEffectEventData();
            xce.Range = new XCRange(fe.Start, fe.End);
            xce.IsLocalTrueOnly = fe.isLocalTrueOnly;
            xce.NormalEffectIds = fe.NormalEffectIds ?? new List<int>();
            xce.SkillEffectIds = fe.SkillEffectIds ?? new List<int>();
            xce.SkillTagList = fe.SkillTagList ?? new List<string>();
            return xce;
        }

        public static XCSkillInputEventData TOXCSkillInputEvent(FSkillInputEvent fe)
        {
            var xce = new XCSkillInputEventData();
            xce.Range = new XCRange(fe.Start, fe.End);
            xce.IsLocalTrueOnly = fe.isLocalTrueOnly;
            fe.InputList.Sort((a, b) => b.SkillSort.CompareTo(a.SkillSort));
            var data = new List<ACTGameEditor.SkillInputData>(fe.InputList.Count);
            for (int i = 0; i < fe.InputList.Count; i++)
            {
                data.Add(new ACTGameEditor.SkillInputData
                {
                    ListernType = fe.InputList[i].ListernType,
                    PressType = fe.InputList[i].PressType,
                    InputCallBackType = fe.InputList[i].InputCallBackType,
                    SkillId = fe.InputList[i].SkillId,
                    SkillSort = (int)fe.InputList[i].SkillSort + fe.InputList[i].Offset,
                    RequiredTags = fe.InputList[i].RequiredTags,
                    BlockedTags = fe.InputList[i].BlockedTags,
                    InputTimeout = fe.InputList[i].InputTimeout,
                });
            }
            xce.InputDataList = data;
            return xce;
        }

        public static Flux.SkillInputData ToFluxSkillInputData(ACTGameEditor.SkillInputData data)
        {
            var skillSorts = (EGamePlay.Combat.SkillSort[])Enum.GetValues(typeof(EGamePlay.Combat.SkillSort));
            int baseVal = (int)EGamePlay.Combat.SkillSort.Normal;
            for (int i = skillSorts.Length - 1; i >= 0; i--)
            {
                int v = (int)skillSorts[i];
                if (data.SkillSort >= v) { baseVal = v; break; }
            }
            return new Flux.SkillInputData
            {
                ListernType = data.ListernType,
                PressType = data.PressType,
                InputCallBackType = data.InputCallBackType,
                SkillId = data.SkillId,
                SkillSort = (EGamePlay.Combat.SkillSort)baseVal,
                Offset = data.SkillSort - baseVal,
                RequiredTags = data.RequiredTags,
                BlockedTags = data.BlockedTags,
                InputTimeout = data.InputTimeout,
            };
        }

        public static FSkillInputEvent TOFSkillInputEvent(XCSkillInputEventData xce)
        {
            var fe = FEvent.Create<FSkillInputEvent>(new FrameRange(xce.Range.Start, xce.Range.End));
            fe.isLocalTrueOnly = xce.IsLocalTrueOnly;
            fe.InputList = new List<Flux.SkillInputData>(xce.InputDataList.Count);
            for (int i = 0; i < xce.InputDataList.Count; i++)
                fe.InputList.Add(ToFluxSkillInputData(xce.InputDataList[i]));
            return fe;
        }

        public static XCTriggerEventData TOXCTriggerEvent(FTriggerRangeEvent fe)
        {
            var xce = new XCTriggerEventData();
            xce.Range = new XCRange(fe.Start, fe.End);
            xce.IsLocalTrueOnly = fe.isLocalTrueOnly;
            xce.CubeRange = CopyCubeRange(fe.cubeRange);
            xce.DamageSegmentIndex = fe.DamageSegmentIndex;
            xce.HitGroupId = fe.HitGroupId;
            xce.EffectIds = fe.EffectIds ?? new List<int>();
            return xce;
        }

        public static FTriggerRangeEvent TOFTriggerEvent(XCTriggerEventData xce)
        {
            var fe = FEvent.Create<FTriggerRangeEvent>(new FrameRange(xce.Range.Start, xce.Range.End));
            fe.isLocalTrueOnly = xce.IsLocalTrueOnly;
            fe.cubeRange = CopyCubeRange(xce.CubeRange);
            fe.DamageSegmentIndex = xce.DamageSegmentIndex;
            fe.HitGroupId = xce.HitGroupId;
            fe.EffectIds = xce.EffectIds != null ? new List<int>(xce.EffectIds) : new List<int>();
            return fe;
        }

        public static FPlayTagEvent TOFEffectEvent(XCEffectEventData xce)
        {
            var fe = FEvent.Create<FPlayTagEvent>(new FrameRange(xce.Range.Start, xce.Range.End));
            fe.isLocalTrueOnly = xce.IsLocalTrueOnly;
            fe.SkillTagList = xce.SkillTagList != null ? new List<string>(xce.SkillTagList) : new List<string>();
            fe.NormalEffectIds = xce.NormalEffectIds != null ? new List<int>(xce.NormalEffectIds) : new List<int>();
            fe.SkillEffectIds = xce.SkillEffectIds != null ? new List<int>(xce.SkillEffectIds) : new List<int>();
            return fe;
        }

        public static XCSwitchEventData TOXCSwitchEvent(FSwitchEvent fe)
        {
            var xce = new XCSwitchEventData();
            xce.Range = new XCRange(fe.Start, fe.End);
            xce.IsLocalTrueOnly = fe.isLocalTrueOnly;
            xce.InputType = fe.InputType;
            return xce;
        }

        public static XCMsgEventData TOXCMsgEvent(FPlayMsgEvent fe)
        {
            var xce = new XCMsgEventData();
            xce.IsLocalTrueOnly = fe.isLocalTrueOnly;
            xce.Range = new XCRange(fe.Start, fe.End);
            xce.MsgEType = fe.msgType;
            xce.MsgName = fe.msgName.ToString();
            xce.BoolMsg = fe.boolMsg;
            xce.SetOppositeOnFinish = fe.setOppositeOnFinish;
            xce.StrMsg = fe.strMsg;
            xce.FloatdMsg = fe.floatMsg;
            return xce;
        }

        public static XCObjEventData TOXCObjEvent(FEvent fe, Transform playerTF)
        {
            var xce = new XCObjEventData();
            xce.Range = new XCRange(fe.Start, fe.End);
            xce.IsLocalTrueOnly = fe.isLocalTrueOnly;
            string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(fe.Owner);
            if (path == "")
            {
                Debug.LogError("yns  path null ");
                path = "SkillEffet/Using/Sword_1";
            }

            xce.BundlePath = PrefabABPathPostprocessor.GenerateBundleNameFromFolder(path) + "_prefab";
            xce.AssetPath = Path.GetFileNameWithoutExtension(Path.GetFileName(path));
            SetObjectEventTF(fe, playerTF, xce);
            return xce;
        }

        public static XCAnimEventData ToXCAnimEvent(FPlayAnimationEvent fe)
        {
            var xce = new XCAnimEventData();
            xce.Range = new XCRange(fe.Start, fe.End);
            xce.IsLocalTrueOnly = fe.isLocalTrueOnly;
            xce.StartOffset = fe._startOffset * XCSetting.FramePerSec;
            xce.BlenderLength = fe._blendLength * XCSetting.FramePerSec;
            xce.IsBackToIdle = fe.isBackToIdle;
            xce.ExitPolicy = fe.ExitPolicy;
            xce.AnimName = fe._animationClip.name;
            return xce;
        }

        public static void SetLineEventTween(XCLineEventData xce, FTweenEvent<FTweenVector3> fe)
        {
            xce.Range = new XCRange(fe.Start, fe.End);
            xce.StartVec = fe.Tween.From;
            xce.IsLocalTrueOnly = fe.isLocalTrueOnly;
            xce.EndVec = fe.Tween.To;
            xce.EaseType = fe.Tween.EasingType.ToDotweenEase();
        }

        public static void SetLineEventTween_Ex(XCMoveEventData xce, FTweenEvent<FTweenVector3_Ex> fe)
        {
            xce.Range = new XCRange(fe.Start, fe.End);
            xce.StartVec = fe.Tween.From;
            xce.IsLocalTrueOnly = fe.isLocalTrueOnly;
            xce.EndVec = fe.Tween.To;
            xce.EaseType = fe.Tween.EasingType.ToDotweenEase();
            xce.IsBezier = fe.Tween.isBezier;
            xce.HandlePoint = fe.Tween.HandlePoint;
            xce.LookForward = fe.Tween.lookForward;
        }

        static EGamePlay.Combat.CubeRange CopyCubeRange(EGamePlay.Combat.CubeRange src)
        {
            var range = new EGamePlay.Combat.CubeRange();
            if (src == null)
                return range;
            range.pos = src.pos;
            range.rotation = src.rotation;
            range.size = src.size;
            range.radius = src.radius;
            range.height = src.height;
            range.colliderType = src.colliderType;
            return range;
        }

        static void SetObjectEventTF(FEvent fe, Transform playerTF, XCObjEventData xce)
        {
            xce.TransfromType = fe.Track.Timeline.transfromType;
            xce.StartPos = fe.Owner.transform.position - playerTF.transform.position;
            xce.StartRotation = fe.Owner.transform.localEulerAngles;
            xce.StartScale = fe.Owner.transform.localScale;
            if (fe.Owner.transform.parent == playerTF)
                xce.StartScale /= playerTF.localScale.x;
        }
    }
}
