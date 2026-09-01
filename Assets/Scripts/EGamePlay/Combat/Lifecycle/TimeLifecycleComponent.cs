using System;

namespace EGamePlay.Combat
{
    /// <summary>生命周期轮询。返回 true 表示宿主应当结束。</summary>
    public interface ILifecycleLogic
    {
        bool OnUpdate(float deltaTime);
    }

    /// <summary>
    /// 通用的时间生命周期配置（持续时间、Tick 间隔、延迟、CD）。
    /// Buff、Ability 等都可以复用。
    /// </summary>
    public sealed class TimeLifecycleConfig
    {
        public FloatNumeric Duration;      // 总时长（<=0 代表永久）
        public FloatNumeric TickInterval;  // 周期触发间隔（<=0 代表非周期）
        public FloatNumeric DelayTick;     // 首次触发前的延迟（<=0 代表无延迟）
        public FloatNumeric CDTime;        // 触发 CD（<=0 代表无 CD）

        public TimeLifecycleConfig(float baseDuration = 0, float baseInterval = 0, float baseDelayTime = 0, float baseCDTime = 0)
        {
            Duration = new FloatNumeric();
            Duration.SetBase(baseDuration);

            TickInterval = new FloatNumeric();
            TickInterval.SetBase(baseInterval);

            DelayTick = new FloatNumeric();
            DelayTick.SetBase(baseDelayTime);

            CDTime = new FloatNumeric();
            CDTime.SetBase(baseCDTime);
        }
    }

    /// <summary>
    /// 通用的时间生命周期运行时状态。
    /// </summary>
    public sealed class TimeLifecycleState
    {
        public float TimeElapsed;      // 已经过的时间
        public float TickAccumulator;  // 用于计算下一次 Tick 的累积时间
        public float TickDelayTime;    // 已经延迟的时间
        public float TickCDTime;       // 当前 CD 计时
        public bool HasStarted;        // 是否已经触发过 OnStart
        public bool IsFinished;        // 是否已经结束
    }

    /// <summary>
    /// 通用的时间生命周期事件。
    /// </summary>
    public sealed class TimeLifecycleEvents
    {
        public Action OnStart;  // 生命周期开始瞬间（考虑延迟后）
        public Action OnTick;   // 周期性触发
        public Action OnEnd;    // 生命周期结束瞬间
    }

    /// <summary>
    /// 通用时间生命周期逻辑，不继承 Component，供各组件组合使用。
    /// </summary>
    public sealed class TimeLifecycle : ILifecycleLogic
    {
        public TimeLifecycleConfig Config { get; private set; }
        public TimeLifecycleState State { get; private set; }
        public TimeLifecycleEvents Events { get; private set; }

        private bool _isRunning;

        public TimeLifecycle()
        {
            State = new TimeLifecycleState();
            Events = new TimeLifecycleEvents();
        }

        /// <summary>
        /// 初始化生命周期配置。
        /// </summary>
        public void Init(float duration, float interval = 0, float delay = 0, float cdTime = 0)
        {
            Config = new TimeLifecycleConfig(duration, interval, delay, cdTime);
        }

        /// <summary>开始计时。</summary>
        public void Start()
        {
            _isRunning = true;
            if (Config != null)
            {
                State.TickCDTime = Config.CDTime.Value;
            }
        }

        /// <summary>停止计时。</summary>
        public void Stop()
        {
            _isRunning = false;
        }

        /// <summary>
        /// 在当前持续时间基础上叠加额外持续时间（秒）。
        /// </summary>
        public void ExtendDuration(float extraDuration)
        {
            if (Config == null || extraDuration <= 0f)
            {
                return;
            }

            var current = Config.Duration.Value;
            Config.Duration.SetBase(current + extraDuration);
        }

        /// <summary>
        /// 重置生命周期持续时间，并重置内部计时状态。
        /// </summary>
        public void ResetDuration(float newDuration)
        {
            if (Config == null)
            {
                return;
            }

            if (newDuration > 0f)
            {
                Config.Duration.SetBase(newDuration);
            }

            State.TimeElapsed = 0f;
            State.TickAccumulator = 0f;
            State.TickDelayTime = 0f;
            State.TickCDTime = 0f;
            State.HasStarted = false;
            State.IsFinished = false;
        }

        /// <summary>
        /// 刷新 CD 计时。
        /// </summary>
        public void RefreshCD()
        {
            State.TickCDTime = 0f;
        }

        /// <summary>
        /// 是否已经满足 CD，可以触发。
        /// </summary>
        public bool GetIsCanTrigger()
        {
            if (Config == null)
            {
                return false;
            }
            return State.TickCDTime >= Config.CDTime.Value;
        }

        /// <summary>
        /// 生命周期更新，返回 true 表示应该结束。
        /// </summary>
        public bool OnUpdate(float deltaTime)
        {
            if (!_isRunning || Config == null)
            {
                return false;
            }

            if (!State.HasStarted)
            {
                if (Config.DelayTick.Value > 0)
                {
                    State.TickDelayTime += deltaTime;
                }

                if (State.TickDelayTime >= Config.DelayTick.Value)
                {
                    Events.OnStart?.Invoke();
                    State.HasStarted = true;
                }
                else
                {
                    return false;
                }
            }

            State.TimeElapsed += deltaTime;
            State.TickAccumulator += deltaTime;
            State.TickCDTime += deltaTime;

            if (Config.TickInterval.Value > 0 && State.TickAccumulator >= Config.TickInterval.Value)
            {
                Events.OnTick?.Invoke();
                State.TickAccumulator -= Config.TickInterval.Value;
            }

            // Duration <= 0 代表永久，不进入结束逻辑。
            if (Config.Duration.Value > 0 && State.TimeElapsed >= Config.Duration.Value)
            {
                Events.OnEnd?.Invoke();
                State.IsFinished = true;
                // 保持与旧 BuffTimeComponent 相同的行为：复用 TickInterval 作为重置值。
                State.TimeElapsed = Config.TickInterval.Value;
            }

            return Config.Duration.Value > 0 && State.TimeElapsed >= Config.Duration.Value;
        }
    }
}

