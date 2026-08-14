using UnityEngine;
using UnityEngine.UI;

namespace XiaoCao
{
    /// <summary>
    /// 挂载在视角切换按钮预制体上，用于显示切换冷却（参考崩坏3）。
    /// 每帧由 MainUIPanel 调用 SetCooldown 刷新；可绑定冷却文字、填充图等。
    /// </summary>
    public class SwitchViewButtonCooldown : MonoBehaviour
    {
        [Tooltip("可选，显示剩余秒数如 3.2s，无冷却时可为空或隐藏")]
        public Text cooldownText;
        [Tooltip("可选，冷却填充图 FillAmount 0=冷却中 1=可用")]
        public Image cooldownFillImage;
        [Tooltip("可选，当前视角高亮（如边框/背景），非当前时隐藏或置灰")]
        public GameObject currentHighlight;

        private void Awake()
        {
            if (cooldownText == null) cooldownText = GetComponentInChildren<Text>(true);
            if (cooldownFillImage == null) cooldownFillImage = GetComponentInChildren<Image>(true);
        }

        /// <summary>
        /// 每帧刷新冷却显示。remaining 剩余秒数，total 总冷却时长，isCurrentTarget 是否为当前跟随视角。
        /// </summary>
        public void SetCooldown(float remaining, float total, bool isCurrentTarget)
        {
            if (cooldownText != null)
            {
                if (remaining > 0f)
                {
                    cooldownText.gameObject.SetActive(true);
                    cooldownText.text = $"{remaining:F1}s";
                }
                else
                {
                    cooldownText.gameObject.SetActive(false);
                    cooldownText.text = string.Empty;
                }
            }

            if (cooldownFillImage != null)
            {
                float fill = total > 0f ? Mathf.Clamp01(1f - remaining / total) : 1f;
                cooldownFillImage.fillAmount = fill;
            }

            if (currentHighlight != null)
                currentHighlight.SetActive(isCurrentTarget);
        }
    }
}
