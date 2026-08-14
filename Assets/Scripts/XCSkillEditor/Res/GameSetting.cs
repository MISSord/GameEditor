using UnityEngine;

namespace XiaoCao
{
    public static class GameSetting
    {
        public static GameMode gameMode;

        public static CameraMode cameraType;

        public static bool isResetCamara = true;

        public static bool HasAIEnable = true;

        public static int Port = 1234;

        public static AgentTag GetEnamyTag(AgentTag agentTag)
        {
            if (agentTag == AgentTag.PlayerB || agentTag == AgentTag.PlayerA)
            {
                return AgentTag.enemy;
            }
            else
            {
                return AgentTag.PlayerA;
            }
        }

        //自身碰撞体layer
        public static int GetColiorLayer(AgentTag agentTag)
        {
            if (agentTag == AgentTag.enemy)
            {
                return LayerMask.NameToLayer("Enemy");
            }
            else
            {
                return LayerMask.NameToLayer("Friend");
            }
        }

        public static int GetAckLayer(AgentTag agentTag)
        {
            if (agentTag == AgentTag.enemy)
            {
                return LayerMask.NameToLayer("EnemyAck");
            }
            else
            {
                //有攻击权限的玩家 一律当友军->本地玩家
                return LayerMask.NameToLayer("FriendAck");
            }
        }
    }

    [EnumLabel("CameraMode")]
    public enum CameraMode
    {
        [EnumLabel("跟随")]
        Follow,
        [EnumLabel("固定")]
        Fix
    }

    public enum GameMode
    {
        PVP,
        PVE
    }

    //被击状态 
    //新框架不用这个，用行为禁止来实现
    //轻重被击可以考虑加入其他内容来完成
    public enum DamageState
    {
        Nor,
        /// <summary>
        /// 重被击
        /// </summary>
        HeavyBreak,
        /// <summary>
        /// 轻被击
        /// </summary>
        LightBreak
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


    //这个未来调整，融入到现有log框架

    //public static class XCDebuger
    //{
    //    public static bool IsLogNor = true;
    //    public static bool IsLogNet = false;
    //    public static bool IsLogSkillEvent = true;

    //    public static void Log(object message, LogTag tag = LogTag.Nor)
    //    {
    //        if(IsLogTag(tag))
    //            Debug.Log(message);
    //    }

    //    private static bool IsLogTag(LogTag tag)
    //    {
    //        switch (tag)
    //        {
    //            case LogTag.Nor:
    //                return IsLogNor;
    //            case LogTag.Net:
    //                return IsLogNet;
    //            case LogTag.SkillEvent:
    //                return IsLogSkillEvent;
    //            default:
    //                break;
    //        }
    //        return true;
    //    }
    //}

    //public enum LogTag
    //{
    //    Nor,
    //    SkillEvent,
    //    Net
    //}
}
