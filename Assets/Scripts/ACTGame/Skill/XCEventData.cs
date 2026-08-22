using DG.Tweening;
using EGamePlay.Combat;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ACTGameEditor
{
    [Serializable]
    public class XCRange
    {
        // start frame
        [SerializeField]
        private int _start;

        // end frame
        [SerializeField]
        private int _end;

        /// @brief Returns the start frame.
        public int Start
        {
            get { return _start; }
            //暂时屏蔽
            //set
            //{
            //    _start = value;
            //}
        }

        /// @brief Returns the end frame.
        public int End
        {
            get { return _end; }
            //暂时屏蔽
            //set
            //{
            //    _end = value;
            //}
        }

        /// @brief Sets / Gets the length.
        /// @note It doesn't cache the value.
        public int Length
        {
            /*set { End = _start + value; } */
            get { return _end - _start; }
        }

        /**
		 * @brief Create a frame Range
		 * @param start Start frame
		 * @param end End frame
		 * @note It is up to you to make sure start is smaller than end.
		 */
        public XCRange(int start, int end)
        {
            this._start = start;
            this._end = end;
        }

        ///// @brief Returns \e i clamped to the Range.
        //public int Cull(int i)
        //{
        //    return Mathf.Clamp(i, _start, _end);
        //}

        ///// @brief Returns if \e i is inside [start, end], i.e. including borders
        //public bool Contains(int i)
        //{
        //    return i >= _start && i <= _end;
        //}

        ///// @brief Returns if \e i is inside ]start, end[, i.e. excluding borders
        //public bool ContainsExclusive(int i)
        //{
        //    return i > _start && i < _end;
        //}

        ///// @brief Returns if the ranges intersect, i.e. touching returns false
        ///// @note Assumes They are both valid
        //public bool Collides(XCRange Range)
        //{
        //    return _start < Range._end && _end > Range._start;
        //}

        ///// @brief Returns if the ranges overlap, i.e. touching return true
        ///// @note Assumes They are both valid
        //public bool Overlaps(XCRange Range)
        //{
        //    return Range.End >= _start && Range.Start <= _end;
        //}

        //public override string ToString()
        //{
        //    return string.Format("[{0}; {1}]", _start, _end);
        //}
    }

    public abstract class XCEventData
    {
        public XCRange Range;
        //是否只有本地才有
        public bool IsLocalTrueOnly = false;
    }

    [Serializable]
    public class XCAnimEventData : XCEventData
    {
        public string AnimName;
        /// <summary>混合时长（秒），走 CrossFadeInFixedTime。</summary>
        public float BlenderLength = 0;
        /// <summary>从 clip 内偏移起播（秒）。</summary>
        public float StartOffset = 0;
        /// <summary>轴自然结束时是否交回 Locomotion；Hold 则保持末帧。</summary>
        public AnimExitPolicy ExitPolicy = AnimExitPolicy.Locomotion;
        /// <summary>本段是否把动画 Root Motion 写入 CharacterController（跟 Token；无曲线则无位移）。</summary>
        public bool UseRootMotion = true;
        /// <summary>本段技能全控位移时压制重力（浮空/动画带 Y 时勾选）。</summary>
        public bool SuppressGravity;
        /// <summary>
        /// 已废弃：请用 ExitPolicy。保留仅兼容旧资源；运行时若 ExitPolicy 为默认且本字段为 false，仍按 Locomotion。
        /// </summary>
        public bool IsBackToIdle;
    }

    [Serializable]
    public class XCObjEventData : XCEventData 
    {
        public bool IsEffect;
        public TransfromType TransfromType;
        public Vector3 PlayerOffset;
        public string BundlePath;
        public string AssetPath;
        //初始状态
        public Vector3 StartPos = Vector3.zero;
        public Vector3 StartRotation = Vector3.zero; //其实是eulerAngles
        public Vector3 StartScale = Vector3.one;
    }

    public abstract class XCLineEventData : XCEventData
    {
        public Vector3 StartVec;
        public Vector3 EndVec;
        public Ease EaseType = Ease.Linear;
    }

    [Serializable]
    public class XCMoveEventData : XCLineEventData
    {
        public Vector3 StartDetal = Vector3.zero; //Move事件之间可能存在空隙,需要补全
        public bool IsBezier;
        public bool LookForward;
        [NaughtyAttributes.ShowIf(nameof(IsBezier))]
        public Vector3 HandlePoint;
    }

    [Serializable]
    public class XCRotateEventData : XCLineEventData { }

    [Serializable]
    public class XCScaleEventData : XCLineEventData { }

    [Serializable]
    public class XCTriggerEventData : XCEventData
    {
        public CubeRange CubeRange;
        /// <summary>该触发对应技能的第几段伤害（从 1 开始），0 表示未指定。</summary>
        public int DamageSegmentIndex = 0;
        /// <summary>同一击多盒共用的组。0 表示按该触发事件实例去重。</summary>
        public int HitGroupId = 0;
        /// <summary>触发时执行的效果 ID；为空时触发该技能下全部效果。</summary>
        public List<int> EffectIds = new List<int>();
    }

    [Serializable]
    public class XCSwitchEventData : XCEventData
    {
        public EventTriggerType InputType;
    }

    [Serializable]
    public class XCMsgEventData : XCEventData
    {
        public MsgType MsgEType;
        public string MsgName;
        public string StrMsg;
        public float FloatdMsg;
        public bool BoolMsg;
        public bool SetOppositeOnFinish;
    }

    [Serializable]
    public class XCEffectEventData : XCEventData
    {
        /// <summary>只触发一次的效果 ID 列表；为空时触发全部。</summary>
        public List<int> NormalEffectIds = new List<int>();
        /// <summary>跟随轨道生命周期的效果 ID 列表；为空时触发全部。</summary>
        public List<int> SkillEffectIds = new List<int>();
        /// <summary>跟随轨道生命周期的标签列表。</summary>
        public List<string> SkillTagList = new List<string>();
    }

    [Serializable]
    //输入信息
    public class SkillInputData 
    {
        //监听操作类型
        public InputListernType ListernType;
        public PressType PressType;
        public InputCallBackType InputCallBackType;
        //技能ID
        public int SkillId;
        //技能优先级
        public int SkillSort;
        //技能释放Buff判断
        public List<string> RequiredTags;
        public List<string> BlockedTags;
        /// <summary>窗边预输入寿命（秒）。≤0 时用 NormalActPlayer.ComboBufferTimeout。</summary>
        public float InputTimeout;
    }

    [Serializable]
    public class XCSkillInputEventData : XCEventData
    {
        public List<SkillInputData> InputDataList;
    }
}
