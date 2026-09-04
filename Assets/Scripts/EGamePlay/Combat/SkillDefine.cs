using Sirenix.OdinInspector;
using UnityEngine;

namespace EGamePlay.Combat
{
    //[LabelText("技能类型")]
    //public enum SkillSpellType
    //{
    //    [LabelText("主动技能")]
    //    Initiative,
    //    [LabelText("被动技能")]
    //    Passive,
    //}

    //[LabelText("目标选取类型")]
    //public enum SkillTargetSelectType
    //{
    //    [LabelText("手动指定")]
    //    PlayerSelect,
    //    [LabelText("碰撞检测")]
    //    CollisionSelect,
    //    [LabelText("固定区域场检测")]
    //    AreaSelect,
    //    [LabelText("条件指定")]
    //    ConditionSelect,
    //}

    //[LabelText("区域场类型")]
    //public enum SkillAffectAreaType
    //{
    //    [LabelText("圆形")]
    //    Circle = 0,
    //    [LabelText("矩形")]
    //    Rect = 1,
    //    [LabelText("组合")]
    //    Compose = 2,
    //}

    [LabelText("能力类型")]
    public enum AbilityType
    {
        /// <summary>
        /// 主动技能
        /// </summary>
        ActiveSkill,
        /// <summary>
        /// 被动技能
        /// </summary>
        PassiveSkill,
        /// <summary>
        /// Buff
        /// </summary>
        Buff,
    }

    /// <summary>
    /// 坐标轴缩放类型
    /// </summary>
    public enum ScaleAxisType
    {
        X,
        Y,
        Z,
        ALL,
    }

    [LabelText("目标传入类型")]
    public enum ExecutionTargetInputType
    {
        [LabelText("None")]
        None = 0,
        [LabelText("传入目标实体")]
        Target = 1,
        [LabelText("传入目标点")]
        Point = 2,

        TargetOrNull= 3,
    }

    [LabelText("作用对象")]
    public enum AddSkillEffetTargetType
    {
        [LabelText("技能目标")]
        SkillTarget = 0,
        [LabelText("附身对象")]
        AttachTarget = 1,
        [LabelText("自身")]
        Self = 2,
        [LabelText("击中的对象")]
        Tirgger = 3,
        [LabelText("其他")]
        Other = 4,
    }

    //"属性类型"
    public enum AttributeType
    {
        None = 0,

        //"生命值上限"
        HealthPointMax = 999,
        //"生命值"
        HealthPoint = 1000,
        //"攻击力"
        Attack = 1001,
        //"护甲值"
        Defense = 1002,
        //"能量值上限"
        ManaMax = 1006,
        //"能量值"
        Mana = 1007,
        //"暴击概率"
        CriticalProbability = 1008,
        //"暴击伤害"
        CriticalValue = 1009,
        //"移动速度"
        MoveSpeed = 1010,
        //"攻击速度"
        AttackSpeed = 1011,

        // 防御区属性 1100 -- 1299
        /// <summary>物理防御（参与物理伤害减免）</summary>
        PhysicalDefense = 1100,

        // 抗性区属性 1300 -- 1399
        /// <summary>物理抗性（0~1，如 0.2 表示 20% 减伤）</summary>
        PhysicResist = 1300,
        /// <summary>火抗</summary>
        FireResist = 1301,
        /// <summary>冰抗</summary>
        IceResist = 1302,
        /// <summary>雷抗</summary>
        ElectricResist = 1303,

        // 易伤区属性 1400 -- 1499
        /// <summary>受到伤害增加（如 0.2 表示多受 20% 伤害）</summary>
        Vulnerability = 1400,

        // 增伤区属性 1500 -- 1699
        /// <summary>造成伤害增加（全类型，如 0.15 表示 +15%）</summary>
        DamageBonus = 1500,
        /// <summary>物理增伤</summary>
        PhysicDamageBonus = 1501,
        /// <summary>火伤增伤</summary>
        FireDamageBonus = 1502,
        /// <summary>冰伤增伤</summary>
        IceDamageBonus = 1503,
        /// <summary>雷伤增伤</summary>
        ElectricDamageBonus = 1504,


        //////////////////////////////////////////////////////////////////////////
        ///这里是Buff属性

        //"Buff层数"
        BuffMaxStacks = 10001,
        //"Buff最大持续时间"
        BuffMaxTime = 10002,
        //"Buff触发次数"
        BuffMaxNumber = 10003,
        //"Buff冷却时间"
        BuffCDTime = 10004,
        //"Buff触发时间"
        BuffIntervalTime = 10005,
        //Buff治愈数值
        BuffCurve = 10006,


        // 物理伤害
        PhysicDamage = 30000,
        // 火伤
        FireDamage = 30001,
        // 冰伤
        IceDamage = 30002,
        // 电伤
        ElectricDamage = 30003,
    }


    [System.Serializable]
    public class CubeRange
    {
        public Vector3 pos;
        public Vector3 rotation; //其实是欧拉角
        public Vector3 size = Vector3.one;
        public float radius; //半径 //给球形与胶囊体用
        public float height; //高度 //给胶囊体用
        public ColliderType colliderType = ColliderType.Box;
    }

    //碰撞体类型
    public enum ColliderType
    {
        //方形
        Box,
        //球形
        Sphere,
        //胶囊体
        Capsule,
    }

    public enum TransfromType
    {
        PlyerUnFollow = 0, //以玩家实时的坐标参考系 (发射后不受玩家影响)
        FollowPlayer = 1,  //以玩家实时的坐标参考系 (发射后跟随玩家)
        WorldPos = 2,      //技能启动时的玩家的参考系
    }

    //事件回调 //这个调整成事件触发器，方便在不同帧数处理不同事件
    public enum EventTriggerType
    {
        Wait,        //占时间用,防止技能走完动画就结束, 用于..
        Exit = 1,    //直接中断退出当前运行
        Finish = 2,  //通知当前运行器完成事件
        ParentFinish = 3, //通知总运行器完成事件
        ParentExit = 4,   //通知总运行器退出事件
    }

    public enum MsgType
    {
        All,
        Bool
    }

    //技能释放优先级，越大越优先释放
    public enum SkillSort
    {
        //普攻
        Normal = 100,
        //分支
        Speical = 1000,
        //武器技能
        Weapon = 2000,
        //特殊武器技能
        SpeicalWeapon = 2500,
        //闪避
        Roll = 3000,
        //大招
        Ultimate = 4000,
    }

    /// <summary>
    /// 槽位 Sort = (int)<see cref="SkillSort"/> + Offset，用区间判断技能大类。
    /// </summary>
    public static class SkillSortUtil
    {
        /// <summary>
        /// 是否为普攻（含连招偏移）或大招。闪避、武器技、分支技能返回 false。
        /// </summary>
        public static bool IsNormalOrUltimate(int sort)
        {
            if (sort >= (int)SkillSort.Normal && sort < (int)SkillSort.Speical)
                return true;
            return sort >= (int)SkillSort.Ultimate;
        }

        /// <summary>是否为闪避槽（含同档偏移）。</summary>
        public static bool IsRoll(int sort)
        {
            return sort >= (int)SkillSort.Roll && sort < (int)SkillSort.Ultimate;
        }
    }
}