using UnityEngine;
using UnityEngine.UI;
using TMPro;
using EGamePlay.Combat;

namespace XiaoCao
{
    /// <summary>
    /// 显示单个 Buff 信息的 UI 图标：
    /// - 图标、剩余时间文本、阴影进度条、层数、剩余次数。
    /// 层数/次数：通过订阅 Buff 的 PropertyUpdateEvent 按需刷新；
    /// 时间/进度条：每帧在 OnUpdate 中刷新（时间连续变化，不适合仅靠事件）。
    /// </summary>
    public class BuffIcon : MonoBehaviour
    {
        [Header("Basic UI")]
        public Image iconImage;                 // Buff 图标
        public Image cooldownMaskImage;        // 冷却遮罩（填充从 1 -> 0）
        // public Image shadowFillImage;          // 阴影进度条（填充从 0 -> 1）

        [Header("Text UI")]
        public TextMeshProUGUI remainingTimeText;  // 剩余时间
        public TextMeshProUGUI stackCountText;     // Buff 层数
        public TextMeshProUGUI remainingTimesText; // Buff 剩余次数

        // 运行时数据引用
        private Buff _buff;
        private BuffTimeComponent _timeComponent;
        private BuffAttributesComponent _attributesComponent;
        private BuffFrequencyComponent _frequencyComponent;
        private BuffProperty _stackProperty;

        // 用于计算剩余时间的总时长（秒）
        private float _maxDuration;

        // 简单的缓存，避免重复改同样的文本
        private float _lastRemainingSeconds = -1f;
        private float _lastStackValue = -1f;
        private float _lastTimesValue = -1f;

        /// <summary>
        /// 层数、次数由 Buff 的 PropertyUpdateEvent 驱动刷新，避免每帧轮询。
        /// </summary>
        private void OnBuffPropertyChanged(PropertyUpdateEvent ev)
        {
            if (ev?.Property == null || _buff == null || !_buff.Enable)
            {
                return;
            }

            if (ev.Property.AttributeType == AttributeType.BuffMaxStacks)
            {
                RefreshStackUI();
            }
            else if (ev.Property.AttributeType == AttributeType.BuffMaxNumber)
            {
                RefreshTimesUI();
            }
        }

        /// <summary>
        /// 绑定一个 Buff 实例和对应的图标。
        /// 外部可以从配置里根据 BuffID 找到 Sprite 然后传进来。
        /// </summary>
        public void Bind(Buff targetBuff, Sprite iconSprite = null)
        {
            _buff = targetBuff;

            if (_buff == null)
            {
                gameObject.SetActive(false);
                return;
            }

            _buff.TryGet(out _timeComponent);
            _attributesComponent = _buff.GetComponent<BuffAttributesComponent>();
            _buff.TryGet(out _frequencyComponent);

            // 仅当 Buff 有时间配置且持续时间大于 0 时，才显示剩余时间与冷却遮罩
            if (_timeComponent != null && _timeComponent.Lifecycle != null && _timeComponent.Lifecycle.Config != null)
            {
                float duration = _timeComponent.Lifecycle.Config.Duration.Value;
                _maxDuration = duration > 0f ? duration : 0f;
            }
            else
            {
                _maxDuration = 0f;
            }

            if (_attributesComponent != null && _buff.IsCanStack)
            {
                _stackProperty = _attributesComponent.GetNumeric(AttributeType.BuffMaxStacks);
            }
            else
            {
                _stackProperty = null;
            }

            if (iconSprite != null && iconImage != null)
            {
                iconImage.sprite = iconSprite;
            }

            // 重置缓存，强制刷新
            _lastRemainingSeconds = -1f;
            _lastStackValue = -1f;
            _lastTimesValue = -1f;

            _buff.Subscribe<PropertyUpdateEvent>(OnBuffPropertyChanged);
            UpdateTimeUI();
            RefreshStackUI();
            RefreshTimesUI();
        }

        /// <summary>
        /// 清空当前显示（回到对象池时调用）。
        /// </summary>
        public void ResetView()
        {
            if (_buff != null)
            {
                _buff.UnSubscribe<PropertyUpdateEvent>(OnBuffPropertyChanged);
            }

            _buff = null;
            _timeComponent = null;
            _attributesComponent = null;
            _frequencyComponent = null;
            _stackProperty = null;
            _maxDuration = 0f;

            _lastRemainingSeconds = -1f;
            _lastStackValue = -1f;
            _lastTimesValue = -1f;

            if (cooldownMaskImage != null) cooldownMaskImage.fillAmount = 0f;
            // if (shadowFillImage != null) shadowFillImage.fillAmount = 0f;
            if (remainingTimeText != null) remainingTimeText.text = string.Empty;
            if (stackCountText != null) stackCountText.text = string.Empty;
            if (remainingTimesText != null) remainingTimesText.text = string.Empty;
        }

        /// <summary>
        /// 外部每帧调用。仅刷新时间相关 UI；层数、次数由 PropertyUpdateEvent 驱动。
        /// </summary>
        public void OnUpdate()
        {
            if (_buff == null || !_buff.Enable)
            {
                gameObject.SetActive(false);
                return;
            }

            UpdateTimeUI();
        }

        /// <summary>
        /// 更新时间相关 UI：文本 + 阴影进度条。
        /// </summary>
        private void UpdateTimeUI()
        {
            if (_timeComponent == null || _maxDuration <= 0f)
            {
                if (cooldownMaskImage != null) cooldownMaskImage.fillAmount = 0f;
                // if (shadowFillImage != null) shadowFillImage.fillAmount = 0f;
                if (remainingTimeText != null && _lastRemainingSeconds != 0f)
                {
                    remainingTimeText.text = string.Empty;
                    _lastRemainingSeconds = 0f;
                }
                return;
            }

            // GetElapsedSeconds 跟战斗世界钟，时空断裂会拉长，单体减速不会
            float elapsed = _timeComponent.GetElapsedSeconds();
            if (elapsed < 0f) elapsed = 0f;
            if (elapsed > _maxDuration) elapsed = _maxDuration;

            float remaining = _maxDuration - elapsed;
            if (remaining < 0f) remaining = 0f;

            float normalized = _maxDuration > 0f ? elapsed / _maxDuration : 0f;

            if (cooldownMaskImage != null)
            {
                // 遮罩：从满到空
                cooldownMaskImage.fillAmount = 1f - normalized;
            }

            // if (shadowFillImage != null)
            // {
            //     // 阴影条：从 0 到 1
            //     shadowFillImage.fillAmount = normalized;
            // }

            if (remainingTimeText != null)
            {
                // 只在数值变化明显时更新文本，减少 GC
                if (Mathf.Abs(remaining - _lastRemainingSeconds) > 0.05f)
                {
                    _lastRemainingSeconds = remaining;
                    remainingTimeText.text = remaining.ToString("0.0");
                }
            }
        }

        /// <summary>
        /// 刷新 Buff 层数显示（由 PropertyUpdateEvent 或 Bind 时调用）。
        /// </summary>
        private void RefreshStackUI()
        {
            if (stackCountText == null)
            {
                return;
            }

            if (_stackProperty == null || _stackProperty.CurrentValue <= 1f)
            {
                _lastStackValue = 0f;
                stackCountText.text = string.Empty;
                return;
            }

            float current = _stackProperty.CurrentValue;
            _lastStackValue = current;
            stackCountText.text = current.ToString("0");
        }

        /// <summary>
        /// 刷新 Buff 剩余次数显示（由 PropertyUpdateEvent 或 Bind 时调用）。
        /// </summary>
        private void RefreshTimesUI()
        {
            if (remainingTimesText == null)
            {
                return;
            }

            if (_frequencyComponent == null || _frequencyComponent.NumberTimes == null)
            {
                _lastTimesValue = 0f;
                remainingTimesText.text = string.Empty;
                return;
            }

            float current = _frequencyComponent.NumberTimes.CurrentValue;
            if (current < 0f) current = 0f;
            _lastTimesValue = current;
            remainingTimesText.text = current.ToString("0");
        }
    }
}