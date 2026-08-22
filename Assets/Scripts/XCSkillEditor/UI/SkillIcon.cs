using System;
using UnityEngine;
using UnityEngine.UI;
using ACTGameEditor;

namespace XiaoCao
{
    public class SkillIcon : MonoBehaviour
    {
        public Image mainImg;
        public Image maskImg;
        public Image greyImg;

        public Text text;

        public XCTimer timer;

        private SkillSlotId _boundSlotId;
        private SkillSlotRuntime _slotRuntime;
        private SkillSlotConfig _slotConfig;
        private SkillCDTimer _cdTimer;
        private NormalActPlayer _player;

        /// <summary>
        /// 绑定槽位：按 SlotId 显示当前技能图标、CD、按键提示。
        /// 换角色/武器后自动显示新技能。
        /// </summary>
        public void InitBySlot(SkillSlotId slotId, NormalActPlayer player, Sprite icon = null)

        {
            _boundSlotId = slotId;
            _player = player;
            _slotRuntime = player?.SlotRuntime;
            _slotConfig = player?.SlotConfig;
            _cdTimer = player?.CDTimer;

            int skillId = player != null ? player.ResolveIdleSkillId(slotId) : (_slotRuntime?.GetSkillId(slotId) ?? 0);
            var entry = _slotConfig?.FindBySlot(slotId);
            var cdTimerForSkill = _cdTimer?.GetTimer(skillId);

            Init(skillId, null, cdTimerForSkill, icon);

            if (text != null && text.gameObject.transform.parent != null && entry != null)
            {
                string keyText = InputTypeToDisplayString(entry.InputType);
                bool showKey = !string.IsNullOrEmpty(keyText);
                text.gameObject.transform.parent.gameObject.SetActive(showKey);
                if (showKey) text.text = keyText;
            }
        }

        /// <summary>
        /// 使用 IdleSkillMapping 与可选计时器/图标初始化技能图标（兼容旧逻辑）。
        /// </summary>
        public void Init(int skillId, IdleSkillMapping mapping, XCTimer cdTimer = null, Sprite icon = null)
        {
            timer = cdTimer ?? new XCTimer();
            if (icon != null && mainImg != null)
                mainImg.sprite = icon;

            if (text != null && text.gameObject.transform.parent != null)
            {
                bool showKey = false;
                string keyText = "";
                if (mapping != null && mapping.Mappings != null)
                {
                    foreach (var m in mapping.Mappings)
                    {
                        if (m.SkillId == skillId)
                        {
                            keyText = InputTypeToDisplayString(m.InputType);
                            showKey = !string.IsNullOrEmpty(keyText);
                            break;
                        }
                    }
                }
                text.gameObject.transform.parent.gameObject.SetActive(showKey);
                if (showKey)
                    text.text = keyText;
            }
        }

        /// <summary>
        /// 使用字符串技能 ID 的便捷重载。
        /// </summary>
        public void Init(string skillID, IdleSkillMapping mapping, XCTimer cdTimer = null, Sprite icon = null)
        {
            if (!int.TryParse(skillID, out int skillId))
                skillId = 0;
            Init(skillId, mapping, cdTimer, icon);
        }

        /// <summary>
        /// 将 InputListernType 转为按键显示文本（如 ButtonX -> "X"）。
        /// </summary>
        public static string InputTypeToDisplayString(InputListernType inputType)
        {
            if (inputType == InputListernType.ButtonX || inputType == InputListernType.LongButtonX) return "X";
            if (inputType == InputListernType.ButtonY || inputType == InputListernType.LongButtonY) return "Y";
            if (inputType == InputListernType.ButtonA || inputType == InputListernType.LongButtonA) return "A";
            if (inputType == InputListernType.ButtonB || inputType == InputListernType.LongButtonB) return "B";
            return "";
        }

        public void OnUpdate()
        {
            if (_slotRuntime != null && _cdTimer != null)
            {
                int skillId = _player != null
                    ? _player.ResolveIdleSkillId(_boundSlotId)
                    : _slotRuntime.GetSkillId(_boundSlotId);
                timer = _cdTimer.GetTimer(skillId);
            }
            if (timer != null && maskImg != null)
            {
                maskImg.fillAmount = timer.IsRunning ? 1f - timer.FillAmount : 0f;
                if (greyImg != null) greyImg.enabled = timer.IsRunning;
            }
        }

        public void OnDisUpdate()
        {
            if (timer != null)
            {
                if (timer.IsRunning)
                {
                    maskImg.fillAmount = timer.IsRunning ? 1 - timer.FillAmount : 0;
                    greyImg.enabled = timer.IsRunning;
                }
                gameObject.SetActive(timer.IsRunning);
            }
        }

        public string KeyCodeToString(KeyCode key)
        {
            return Convert.ToChar(key).ToString().ToUpper();
        }

        //str->转KeyCode
        public KeyCode StringToKeyCode(string str)
        {
            if (str.Length <= 0) return KeyCode.None;
            if (char.IsDigit(str[0]))
            {
                return (KeyCode)System.Enum.Parse(typeof(KeyCode), ("Alpha" + str.ToUpper().Substring(str.Length - 1, 1)));
            }
            else if (char.IsLetter(str[0]))
            {
                return (KeyCode)System.Enum.Parse(typeof(KeyCode), str.ToUpper().Substring(str.Length - 1, 1));
            }

            return KeyCode.None;
        }
    }
}
