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
}