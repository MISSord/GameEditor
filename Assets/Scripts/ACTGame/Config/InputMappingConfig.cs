
//操作输入回调类型
public enum InputCallBackType
{
    Started,    // 按键按下瞬间
    Performed,  // Action 执行（例如短按释放、长按时间到）
    Canceled    // 按键抬起或被系统取消
}

//需要监听的点击类型 //新加之前务必先确认在InputActionAsset里是否有，没有就新加
public enum InputListernType
{
    Move,
    ButtonX,
    ButtonY,
    ButtonA,
    ButtonB,
    LongButtonX,
    LongButtonY,
    LongButtonA,
    LongButtonB,
    Jump,
    Targeting,
    //Weapon,
}

public enum PressType { Click, LongPress }

//记录操作信息
public struct InputRecord
{
    public InputListernType Command;
    public PressType PressType;
    public InputCallBackType InputCallBackType;
    public float timestamp;
}
