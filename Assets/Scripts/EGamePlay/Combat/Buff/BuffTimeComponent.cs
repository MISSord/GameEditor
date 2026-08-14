using System;
using ET;

// 时间生命周期改为 ETTimerManager 集中调度，不再每帧 Update。
namespace EGamePlay.Combat
{
    /// <summary>
    /// Buff 的时间生命周期组件：用 ETTimerManager 注册到点回调，替代每帧 OnUpdate。
    /// 对外接口（ExtendDuration/ResetDuration/GetTimeAttribute/GetIsCanTrigger/RefreshCDTime）保持不变。
    /// </summary>
    public class BuffTimeComponent : Component, ILifecycleLogic
    {
        private readonly TimeLifecycle _lifecycle = new TimeLifecycle();

        public TimeLifecycleConfig Config => _lifecycle.Config;
        public TimeLifecycleState State => _lifecycle.State;
        public TimeLifecycleEvents Events => _lifecycle.Events;

        public override bool DefaultEnable { get; set; } = false;

        private long _delayTimerId;
        private long _tickTimerId;
        private long _endTimerId;
        private long _startTimeMs;
        private long _endTimeMs;
        private long _lastTriggerTimeMs; // CD：上次触发时间，用于 GetIsCanTrigger

        public override void OnEnable()
        {
            _lifecycle.Start();
            ScheduleTimers();
        }

        public override void OnDisable()
        {
            CancelAllTimers();
            _lifecycle.Stop();
        }

        private void ScheduleTimers()
        {
            if (Config == null || ETTimerManager.Instance == null) return;

            long now = TimeHelper.ClientNow();
            float delaySec = Config.DelayTick != null ? Config.DelayTick.Value : 0f;
            float intervalSec = Config.TickInterval != null ? Config.TickInterval.Value : 0f;
            float durationSec = Config.Duration != null ? Config.Duration.Value : 0f;

            long delayMs = (long)(delaySec * 1000);
            long intervalMs = Math.Max(30, (long)(intervalSec * 1000));
            long durationMs = (long)(durationSec * 1000);

            void FireOnStart()
            {
                State.HasStarted = true;
                Events.OnStart?.Invoke();
            }

            void FireOnTick()
            {
                Events.OnTick?.Invoke();
            }

            void ScheduleTickAndEnd()
            {
                _startTimeMs = TimeHelper.ClientNow();
                _endTimeMs = durationMs > 0 ? _startTimeMs + durationMs : 0;

                if (intervalSec > 0)
                {
                    _tickTimerId = ETTimerManager.Instance.NewRepeatedTimer(intervalMs, _ =>
                    {
                        FireOnTick();
                        if (durationMs > 0 && TimeHelper.ClientNow() >= _endTimeMs)
                        {
                            ETTimerManager.Instance.Remove(_tickTimerId);
                            _tickTimerId = 0;
                            if (_endTimerId != 0)
                            {
                                ETTimerManager.Instance.Remove(_endTimerId);
                                _endTimerId = 0;
                            }
                            DoFireOnEnd();
                        }
                    });
                }

                if (durationMs > 0)
                {
                    _endTimerId = ETTimerManager.Instance.NewOnceTimer(_endTimeMs, () =>
                    {
                        _endTimerId = 0;
                        if (_tickTimerId != 0)
                        {
                            ETTimerManager.Instance.Remove(_tickTimerId);
                            _tickTimerId = 0;
                        }
                        DoFireOnEnd();
                    });
                }
            }

            if (delayMs > 0)
            {
                _delayTimerId = ETTimerManager.Instance.NewOnceTimer(now + delayMs, () =>
                {
                    _delayTimerId = 0;
                    FireOnStart();
                    ScheduleTickAndEnd();
                });
            }
            else
            {
                FireOnStart();
                ScheduleTickAndEnd();
            }
        }

        private void CancelAllTimers()
        {
            if (ETTimerManager.Instance == null) return;
            if (_delayTimerId != 0) { ETTimerManager.Instance.Remove(_delayTimerId); _delayTimerId = 0; }
            if (_tickTimerId != 0) { ETTimerManager.Instance.Remove(_tickTimerId); _tickTimerId = 0; }
            if (_endTimerId != 0) { ETTimerManager.Instance.Remove(_endTimerId); _endTimerId = 0; }
        }

        public void Init(float duration, float interval = 0, float delay = 0, float cdTime = 0)
        {
            _lifecycle.Init(duration, interval, delay, cdTime);
        }

        /// <summary>
        /// 在当前持续时间基础上叠加额外持续时间（秒）；集中调度下会重注册结束定时器。
        /// </summary>
        public void ExtendDuration(float extraDuration)
        {
            _lifecycle.ExtendDuration(extraDuration);
            if (extraDuration > 0)
            {
                _endTimeMs += (long)(extraDuration * 1000);
                RescheduleEndTimer();
            }
        }

        /// <summary>
        /// 重置 Buff 的持续时间，并重置内部计时状态；集中调度下会重注册结束定时器。
        /// </summary>
        public void ResetDuration(float newDuration)
        {
            _lifecycle.ResetDuration(newDuration);
            _endTimeMs = TimeHelper.ClientNow() + (long)(newDuration * 1000);
            RescheduleEndTimer();
        }

        /// <summary>
        /// 兼容旧接口，按照 Buff 的属性类型返回对应时间数值。
        /// </summary>
        public FloatNumeric GetTimeAttribute(AttributeType type)
        {
            if (type == AttributeType.BuffMaxTime)
                return _lifecycle.Config.Duration;
            if (type == AttributeType.BuffIntervalTime)
                return _lifecycle.Config.TickInterval;
            if (type == AttributeType.BuffCDTime)
                return _lifecycle.Config.CDTime;
            return null;
        }

        /// <summary>刷新 CD（记录当前时间为上次触发时间）。</summary>
        public void RefreshCDTime()
        {
            _lastTriggerTimeMs = TimeHelper.ClientNow();
            _lifecycle.RefreshCD();
        }

        /// <summary>是否已过 CD 可触发（按“距上次触发时间”计算，不依赖每帧累积）。</summary>
        public bool GetIsCanTrigger()
        {
            if (Config == null || Config.CDTime.Value <= 0) return true;
            if (_lastTriggerTimeMs == 0) return true;
            return (TimeHelper.ClientNow() - _lastTriggerTimeMs) >= (long)(Config.CDTime.Value * 1000);
        }

        /// <summary>对外暴露底层生命周期对象，供只读查询使用。</summary>
        public TimeLifecycle Lifecycle => _lifecycle;

        /// <summary>
        /// 获取已过去的时间（秒）。基于 ETTimerManager 时间戳计算，不依赖每帧 OnUpdate。
        /// 供 BuffIcon 等 UI 使用，替代已失效的 Lifecycle.State.TimeElapsed。
        /// </summary>
        public float GetElapsedSeconds()
        {
            if (_startTimeMs == 0) return 0f;
            if (State.IsFinished)
            {
                float dur = Config?.Duration?.Value ?? 0f;
                return dur > 0f ? dur : 0f;
            }
            float elapsed = (TimeHelper.ClientNow() - _startTimeMs) / 1000f;
            float maxDuration = Config?.Duration?.Value ?? 0f;
            if (maxDuration > 0f && elapsed > maxDuration) elapsed = maxDuration;
            return elapsed >= 0f ? elapsed : 0f;
        }

        /// <summary>集中调度下不再每帧调用；保留以满足 ILifecycleLogic，实际由定时器驱动。</summary>
        public bool OnUpdate(float deltaTime)
        {
            return State.IsFinished;
        }

        private void DoFireOnEnd()
        {
            _endTimerId = 0;
            if (_tickTimerId != 0)
            {
                ETTimerManager.Instance?.Remove(_tickTimerId);
                _tickTimerId = 0;
            }
            State.IsFinished = true;
            Events.OnEnd?.Invoke();
            if (Entity is Buff b && !b.IsDisposed)
                b.CheckIsCanRemove();
        }

        private void RescheduleEndTimer()
        {
            if (ETTimerManager.Instance == null) return;
            if (_endTimerId != 0)
            {
                ETTimerManager.Instance.Remove(_endTimerId);
                _endTimerId = 0;
            }
            if (_endTimeMs > 0)
                _endTimerId = ETTimerManager.Instance.NewOnceTimer(_endTimeMs, DoFireOnEnd);
        }
    }
}
