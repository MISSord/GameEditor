#if !SERVER
using Sirenix.OdinInspector;
#endif

[LabelText("移动类型")]
public enum CollisionMoveType
{
    [LabelText("可选位置碰撞体")]
    SelectedPosition,
    [LabelText("可选朝向间距碰撞体")]
    SelectedDirection,
    [LabelText("围绕自身旋转碰撞体")]
    RotationPosition,

    [LabelText("目标飞行碰撞体")]
    TargetFly,
    [LabelText("朝向飞行碰撞体")]
    ForwardFly,
    [LabelText("固定路径飞行碰撞体")]
    PathFly,
    [LabelText("可选朝向路径飞行碰撞体")]
    SelectedDirectionPathFly,
}

[LabelText("自身移动类型")]
public enum SelfMoveType
{
    [LabelText("闪现/传输飞行")]
    Flash,
    [LabelText("可选位置飞行")]
    SelectedPosition,
    [LabelText("可选朝向飞行")]
    ForwardFly,
    [LabelText("路径飞行")]
    PathFly,
    [LabelText("可选朝向路径飞行")]
    SelectedDirectionPathFly,
}


[LabelText("路径中轴点")]
public enum PathExecutePoint
{
    /// <summary>
    /// 以执行体为起始
    /// </summary>
    [LabelText("以执行体坐标加偏移为中轴点")]
    EntityOffset = 10,

    /// <summary>
    /// 以输入位置为起始
    /// </summary>
    [LabelText("以输入坐标加偏移为中轴点")]
    InputPoint = 20,

    /// <summary>
    /// 以固定世界位置为起始
    /// </summary>
    [LabelText("以固定世界坐标加偏移为中轴点")]
    WorldInputPoint = 30
}

public enum EffectApplyType
{
    [LabelText("全部效果")]
    AllEffects,
    [LabelText("效果1")]
    Effect1,
    [LabelText("效果2")]
    Effect2,
    [LabelText("效果3")]
    Effect3,
    [LabelText("效果4")]
    Effect4,
    [LabelText("效果5")]
    Effect5,

    //[LabelText("其他")]
    //Other = 100,
}