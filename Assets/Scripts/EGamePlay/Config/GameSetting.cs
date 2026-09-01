namespace EGamePlay
{

    [EnumLabel("CameraMode")]
    public enum CameraMode
    {
        [EnumLabel("跟随")]
        Follow,
        [EnumLabel("固定")]
        Fix
    }

    //判断阵营
    public enum AgentTag
    {
        PlayerA,
        PlayerB,
        enemy,
        other
    }

    //模型 这个未来和上面的阵营进行统一！！！
    public enum AgentModelType
    {
        Player = 0,
        EnemyA = 1,
        EnemyB = 2,
    }

    public enum ClientEventType
    {
        Start,
        Stop,
        Change,
        ValueChange,
    }

    //移动状态，这里更多是指外部表现，与下面的PlayerStateEnum无关
    //玩家在滑动，走路，跑步等非技能导致的运动时，都是处于PlayerStateEnum.Moving状态
    public enum MoveTypeEnum
    {
        Idle,
        Falling,
        Walk,
        Run,
        Jump,
    }

    //当前状态
    public enum PlayerStateEnum
    {
        Idle,
        Moving, //这里是指角色处于如跑步，飞行等移动状态中
        PlayerSkill, //闪避属于释放技能，闪避技能
        Hit,
        Dead
    }
}
