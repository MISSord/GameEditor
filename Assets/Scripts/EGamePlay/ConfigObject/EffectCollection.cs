
#if EGAMEPLAY_ET
using Unity.Mathematics;
using Vector3 = Unity.Mathematics.float3;
using Quaternion = Unity.Mathematics.quaternion;
using JsonIgnore = MongoDB.Bson.Serialization.Attributes.BsonIgnoreAttribute;
using StatusConfig = cfg.Status.StatusCfg;
#endif

namespace EGamePlay.Combat
{
    public class DamageEffect : Effect
    {

        public DamageType DamageType;

        //这个可以放到配置也可以做在这里，目前先做在这里
        //伤害数值
        public float DamageValueProperty;

        //伤害公式
        public DamageCalcuFormulaType FormulaType;

        //能否暴击
        public bool CanCrit;
    }

    public class CureEffect : Effect
    {
        public override string Label => "治疗目标";

        /// <summary>
        /// 作用的资源类型（血量/能量等）。
        /// 默认可视为生命值。
        /// </summary>
        public AttributeType AttributeType;

        // 治疗数值
        public float CureValueProperty;
    }

    ////生效方式
    //public enum EffectiveMethod
    //{
    //    /// <summary>
    //    /// 生效时间
    //    /// </summary>
    //    DurTime,
    //    /// <summary>
    //    /// 生效次数
    //    /// </summary>
    //    DurCount,
    //    /// <summary>
    //    /// 生效技能（这里是指由技能来控制加入与移除）
    //    /// </summary>
    //    DurSkill,
    //    /// <summary>
    //    /// 按照事件生效（生效时立马加入，只监听事件来移除，（可以理解为持续时间无限，走事件触发去移除））
    //    /// </summary>
    //    DurEvent,
    //}


    //public class AddStatusEffect : Effect
    //{
    //    public override string Label =>
    //        AddStatusId != 0 ? $"施加 [ {AddStatusId} ] 状态效果" : "施加状态效果";

    //    public int AddStatusId;

    //    public Dictionary<string, string> Params = new Dictionary<string, string>();
    //}

    ////移除状态类型
    //public enum RemoveStatus
    //{
    //    [LabelText("移除单个Buff")]
    //    Single,
    //    [LabelText("移除某种大类型的全部某小类型Buff")]
    //    SingleSmalBuffType,
    //    [LabelText("移除某个大类型Buff")]
    //    SingleBigBuffType
    //}

    //[Effect("移除状态效果", 40)]
    //public class RemoveStatusEffect : Effect
    //{
    //    public override string Label
    //    {
    //        get
    //        {
    //            if (this.RemoveStatusId != 0)
    //            {
    //                return $"移除 [ {this.RemoveStatusId} ] 状态效果";
    //            }
    //            return "移除状态效果";
    //        }
    //    }

    //    [LabelText("移除状态类型")]
    //    public RemoveStatus RemoveStatus;

    //    private bool _isShowBig => RemoveStatus == RemoveStatus.Single || RemoveStatus == RemoveStatus.SingleBigBuffType;

    //    [ToggleGroup("Enabled"), ShowIf("_isShowBig"), LabelText("大Buff类型")]
    //    public int BigBuffType;

    //    [ToggleGroup("Enabled"), ShowIf("RemoveStatus", RemoveStatus.SingleSmalBuffType), LabelText("小Buff类型")]
    //    public int SmallBuffType;

    //    [ToggleGroup("Enabled"), ShowIf("RemoveStatus", RemoveStatus.Single), LabelText("移除BuffID")]
    //    public int RemoveStatusId = 0;
    //}

    //[Effect("移除所有状态效果", 50)]
    //public class ClearAllStatusEffect : Effect
    //{
    //    public override string Label => "移除所有状态效果";
    //}
}