namespace EGamePlay.Combat
{
    /// <summary>
    /// 能力生命周期组件，基于次数
    /// </summary>
    public class BuffFrequencyComponent : Component, ILifecycleLogic
    {
        public BuffProperty NumberTimes;

        public override void Awake()
        {
            // 折中方案：次数数据统一由 BuffAttributesComponent 托管，
            // BuffFrequencyComponent 只提供“次数生命周期”的语义封装（Consume/FillUp 等）。
            var buff = Entity.As<Buff>();
            var baseTimes = buff?.Setting?.BaseTimes ?? 0;

            var attrs = Entity.GetComponent<BuffAttributesComponent>();
            if (attrs == null)
            {
                // 理论上 Buff.Awake 一定会先 AddComponent<BuffAttributesComponent>()。
                NumberTimes = Entity.AddChild<BuffProperty>();
                NumberTimes.AttributeType = AttributeType.BuffMaxNumber;
                NumberTimes.MaxValue.SetBase(baseTimes);
                NumberTimes.CurrentValue = NumberTimes.MaxValue.Value;
                return;
            }

            NumberTimes = attrs.GetNumeric(AttributeType.BuffMaxNumber);
            if (NumberTimes == null)
            {
                // 如果外部未提前创建（例如某些老配置），这里兜底创建。
                NumberTimes = attrs.AddNumeric(AttributeType.BuffMaxNumber, baseTimes);
                NumberTimes.CurrentValue = NumberTimes.MaxValue.Value;
            }
        }

        public FloatNumeric GetNumberNumeric()
        {
            return NumberTimes.MaxValue;
        }

        //刷满次数
        public void FillUpStack()
        {
            NumberTimes.CurrentValue = NumberTimes.MaxValue.Value;
        }

        // 提供给 Buff 系统调用的方法
        public bool ConsumeStack()
        {
            NumberTimes.CurrentValue--;
            return NumberTimes.CurrentValue <= 0; // 次数用完，返回 true
        }

        //增加Buff次数
        public void AddStack()
        {
            NumberTimes.CurrentValue++;
        }

        public bool OnUpdate(float deltaTime)
        {
            // ILifecycleLogic 约定：返回 true 表示应该结束
            return NumberTimes.CurrentValue <= 0;
        }
    }
}
