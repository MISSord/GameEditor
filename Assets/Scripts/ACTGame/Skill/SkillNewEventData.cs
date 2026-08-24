using System;
using Sirenix.OdinInspector;
using System.Collections.Generic;
using ACTGameEditor;

[Serializable]
public class SkillNewEventData
{
    //BaseSkillID 主技能id
    public int SkillId = 0;
    //SubSkillID 子技能id，同一个子技能在伤害计算与效果触发上有相同逻辑
    public string SkillName = "";
    //技能顺序排序
    public int SkillSort = 0;
    //运行速度 影响下面的事件执行时的时间累加速度
    public float Speed = 1f;

    //ObjEvent 是生成物体,一般是子技能的载体
    [ShowIf(nameof(HasObjEvent))]
    public XCObjEventData ObjEvent;

    //动画事件
    [ShowIf(nameof(ShowAnimEvents))]
    public XCEventOwnerData<XCAnimEventData> AnimEvents = new XCEventOwnerData<XCAnimEventData>();

    //位移 旋转 缩放
    [ShowIf(nameof(ShowMoveEvents))]
    public XCEventOwnerData<XCMoveEventData> MoveEvents = new XCEventOwnerData<XCMoveEventData>();

    [ShowIf(nameof(ShowRotateEvents))]
    public XCEventOwnerData<XCRotateEventData> RotateEvents = new XCEventOwnerData<XCRotateEventData>();

    [ShowIf(nameof(ShowScale))]
    public XCEventOwnerData<XCScaleEventData> ScaleEvents = new XCEventOwnerData<XCScaleEventData>();

    //SwitchEvents 用来控制触发技能进度，如前摇后摇
    [ShowIf(nameof(ShowSwitchEvents))]
    public XCEventOwnerData<XCSwitchEventData> SwitchEvents = new XCEventOwnerData<XCSwitchEventData>();

    ///MsgEvents 用于发送消息,如重力开关 <see cref="EGamePlay.Combat.PlayEventMsg"/> 
    [ShowIf(nameof(ShowMsgEvents))]
    public XCEventOwnerData<XCMsgEventData> MsgEvents = new XCEventOwnerData<XCMsgEventData>();

    //伤害范围
    [ShowIf(nameof(ShowTriggerEvents))]
    public XCEventOwnerData<XCTriggerEventData> TriggerEvents = new XCEventOwnerData<XCTriggerEventData>();

    //操作输入监听
    [ShowIf(nameof(ShowSkillInputEvents))]
    public XCEventOwnerData<XCSkillInputEventData> SkillInputEvents = new XCEventOwnerData<XCSkillInputEventData>();

    //效果列表
    [ShowIf(nameof(ShowEffectEvents))]
    public XCEventOwnerData<XCEffectEventData> EffectEvents = new XCEventOwnerData<XCEffectEventData>();

    public bool HasObjEvent
    {
        get
        {
            return ObjEvent != null && ObjEvent.BundlePath != null && ObjEvent.BundlePath != string.Empty;
        }
    }

    public bool ShowAnimEvents => IsHasLen(AnimEvents);
    public bool ShowMoveEvents => IsHasLen(MoveEvents);
    public bool ShowRotateEvents => IsHasLen(RotateEvents);
    public bool ShowScale => IsHasLen(ScaleEvents);
    public bool ShowSwitchEvents => IsHasLen(SwitchEvents);
    public bool ShowMsgEvents => IsHasLen(MsgEvents);
    public bool ShowTriggerEvents => IsHasLen(TriggerEvents);
    public bool ShowSkillInputEvents => IsHasLen(SkillInputEvents);
    public bool ShowEffectEvents => IsHasLen(EffectEvents);

    public bool IsHasLen<T>(XCEventOwnerData<T> data) where T : XCEventData
    {
        return data.Events.Count > 0;
    }

    [Serializable]
    public class XCEventOwnerData<T> where T : XCEventData
    {
        public List<T> Events = new List<T>();

        //运行时调用
        [NonSerialized]
        private List<XCEventData> _events;

        public List<XCEventData> ToXCEventList()
        {
            if (_events == null)
            {
                _events = new List<XCEventData>();
                foreach (var item in Events)
                {
                    _events.Add(item);
                }
            }
            return _events;
        }
    }
}
