using UnityEngine;

namespace EGamePlay.Combat
{
    /// <summary>
    /// 通用的“次数生命周期”组件，可用于技能可用次数、Buff 触发次数等。
    /// </summary>
    public sealed class CountLifecycleComponent : Component, ILifecycleLogic
    {
        /// <summary>最大可用次数（可被数值系统修饰）。</summary>
        public FloatNumeric MaxCount { get; private set; }

        /// <summary>当前剩余次数。</summary>
        public float CurrentCount { get; private set; }

        public override void Awake()
        {
            MaxCount = new FloatNumeric();
        }

        /// <summary>
        /// 初始化次数（基础最大次数）。
        /// </summary>
        public void Init(float baseCount)
        {
            MaxCount.SetBase(baseCount);
            CurrentCount = MaxCount.Value;
        }

        /// <summary>
        /// 将当前次数填满为最大值。
        /// </summary>
        public void FillUp()
        {
            CurrentCount = MaxCount.Value;
        }

        /// <summary>
        /// 增加次数（已做上限保护）。
        /// </summary>
        public void Add(float value = 1f)
        {
            var max = MaxCount.Value;
            var next = CurrentCount + value;
            if (next > max)
            {
                next = max;
            }
            CurrentCount = next;
        }

        /// <summary>
        /// 消耗一次或多次，返回是否已经耗尽（<=0）。
        /// </summary>
        public bool Consume(float value = 1f)
        {
            CurrentCount -= value;
            return CurrentCount <= 0f;
        }

        /// <summary>
        /// ILifecycleLogic 更新：当前次数耗尽则返回 true。
        /// </summary>
        public bool OnUpdate(float deltaTime)
        {
            return CurrentCount <= 0f;
        }
    }
}

