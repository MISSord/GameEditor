using System;
using UnityEngine;

namespace ACTGameEditor
{

    public class XCTimer : IResettable
    {
        public string name { get; private set; }
        //动态进行时间缩放
        public float CDRate = 1; 
        public float FillAmount
        {
            get
            {
                if (_duration == 0)
                {
                    return 0;
                }

                return Mathf.Min(1, _elapsedTime / _duration);
            }
        }
        public float TotalTime => _duration;

        // 配置参数
        private float _duration;
        private float _interval;
        private bool _isRepeate;
        private int _repeateNumber;

        // 状态变量
        private float _elapsedTime;
        private float _intervalAccumulator;
        private bool _isRunning;
        private bool _isCompleted;
        private int _curRepeateNumber;

        // 回调事件
        private Action _onComplete;
        private Action _onInterval;
        private Action _onRepeate;

        // 确保剩余时间不会小于0
        public float RemainingTime => Mathf.Max(0, _duration - _elapsedTime);
        public bool IsRunning => _isRunning;

        public void Init(string name, float exitTime, Action action = null)
        {
            this.name = name;
            this._duration = exitTime;
            this._onComplete = action;
            ResetState();
        }

        /// <summary>
        /// 添加间隔事件
        /// </summary>
        /// <param name="intervalTime"></param>
        /// <param name="action"></param>
        public void AddEventInterval(float intervalTime, Action action)
        {
            this._interval = intervalTime;
            this._onInterval = action;
        }

        /// <summary>
        /// 添加重复回调
        /// </summary>
        /// <param name="isRepeate"></param>
        /// <param name="number"> -1 代表无限次</param>
        /// <param name="_onRepeate"></param>
        public void AddRepeate(bool isRepeate, int number = -1, Action _onRepeate = null)
        {
            this._isRepeate = isRepeate;
            this._repeateNumber = number;
            this._onRepeate = _onRepeate;
        }

        /// <summary>
        /// 更新结束方法
        /// </summary>
        /// <param name="action"></param>
        public void ChangeAction(Action action)
        {
            this._onComplete = action;
        }

        /// <summary>
        /// 【新增核心方法】动态增加或减少时间
        /// </summary>
        /// <param name="seconds">要增加的秒数（传负数则为减少）</param>
        public void AddTime(float seconds)
        {
            _duration += seconds;

            // 特殊情况处理：如果计时器之前已经结束了，但加时后剩余时间又大于0了
            // 我们需要把它“复活”，标记为未完成，并确保它处于运行状态
            if (_isCompleted && _duration > _elapsedTime)
            {
                _isCompleted = false;
                _isRunning = true; // 可选：加时后自动继续运行
            }
        }

        public void Update(float outDeltaTime)
        {
            if (!_isRunning || _isCompleted) return;

            float deltaTime = outDeltaTime * CDRate;

            _elapsedTime += deltaTime;
            _intervalAccumulator += deltaTime;

            //间隔回调
            if (_interval > 0 && _intervalAccumulator >= _interval)
            {
                _onInterval?.Invoke();
                _intervalAccumulator -= _interval;
            }

            // 结束回调
            // 这里的判断动态依赖于 _duration，所以修改 _duration 会直接影响结束时间
            if (_elapsedTime >= _duration)
            {
                //如果能重复回调
                if (_isRepeate == true && (_repeateNumber == -1 || _curRepeateNumber < _repeateNumber))
                {
                    _curRepeateNumber++;
                    _onRepeate?.Invoke();
                    _elapsedTime -= _duration;
                }
                else
                {
                    // 防止剩余时间显示负数，强行修正
                    _elapsedTime = _duration;
                    Complete();
                }
            }
        }

        public void Start() => _isRunning = true;
        public void Pause() => _isRunning = false;
        public void Cancel() { _isRunning = false; ResetState(); }
        public void Restart() { ResetState(); Start(); }

        private void Complete()
        {
            _isRunning = false;
            _isCompleted = true;
            _onComplete?.Invoke();
        }

        private void ResetState()
        {
            _curRepeateNumber = 0;
            _elapsedTime = 0;
            _intervalAccumulator = 0;
            _isCompleted = false;
        }

        public void Reset()
        {
            _duration = 0;
            _interval = 0;
            _isRepeate = false;
            _repeateNumber = 0;

            _elapsedTime = 0;
            _intervalAccumulator = 0;
            _curRepeateNumber = 0;
            _isCompleted = false;
            _isRunning = false;

            _onComplete = null;
            _onInterval = null;
            _onRepeate = null;

            CDRate = 1;
            name = string.Empty;
        }
    }
}
