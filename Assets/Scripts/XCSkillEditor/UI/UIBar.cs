using UnityEngine;
using UnityEngine.UI;
using NaughtyAttributes;
using DG.Tweening;
using EGamePlay;
using ACTGameEditor.Combat;

namespace XiaoCao
{
    public class UIBar : UIBase
    {
        [SerializeField]
        public Image barImg;        
        public Image barImg_noBreak;
        public Image barImgSlow;
        public Text numText;
        public Transform barImgTF;

        //public Color fullColor;
        //public Color emptyColor;
        //public Color playerAColor;
        //public Color playerBColor;
        //public Color NpcColor;

        public Vector3 offSet;
        public Vector3 offSet2;

        [OnValueChanged("TestFill")]
        public float curFill = 1;

        [HideInInspector]
        public Transform target;

        public AgentTag PlayTag;

        public float tweenDuration = 0.2f;

        public bool isShowNum;

        public bool isMove = true;

        public bool autoSize = false;

        private Tween _uiTween;
        private int _lastHp = int.MinValue;
        private int _lastMaxHp = int.MinValue;
        Text _executeHint;
        bool _hintVisible;
        bool _hintInRange;
        Color _harmonyBarColor = new Color(1f, 0.82f, 0.18f, 1f);

        static readonly Color HarmonyChargingColor = new Color(0.32f, 0.78f, 1f, 1f);
        static readonly Color HarmonyReadyColor = new Color(0.92f, 0.99f, 1f, 1f);
        static readonly Color HarmonyExecutingColor = new Color(1f, 0.94f, 0.72f, 1f);
        static readonly Color HarmonyVacuumColor = new Color(0.22f, 0.38f, 0.48f, 1f);
        static readonly Color ExecuteHintColor = new Color(0.78f, 0.96f, 1f, 1f);

        private void Start()
        {
            numText.gameObject.SetActive(isShowNum);
        }

        public void OnUpdate()
        {
            if (!IsCanvasInited || target == null || !isMove)
                return;

            Camera worldCam = MainCam;
            RectTransform followRect = Rect;
            if (worldCam == null || followRect == null || canvas == null)
                return;

            Vector3 worldPos = target.position + offSet;
            Vector3 screen = worldCam.WorldToScreenPoint(worldPos);
            if (screen.z <= 0f)
                return;

            Camera uiCam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            RectTransform parentRect = followRect.parent as RectTransform;
            if (parentRect == null)
                parentRect = canvasRect;
            if (parentRect == null)
                return;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screen, uiCam, out Vector2 localPoint))
                return;

            followRect.anchoredPosition = localPoint;
            ResetBarChildLocal();
            TickExecuteHintPulse();

            if (autoSize)
            {
                float dis = Vector3.Distance(worldPos, worldCam.transform.position);
                if (dis < 0.01f)
                    dis = 0.01f;
                followRect.localScale = GetScaleByDistance(dis, scaleRate_Bar) * Vector3.one;
            }
        }

        void ResetBarChildLocal()
        {
            if (barImgTF == null)
                return;

            if (barImgTF is RectTransform barRt)
                barRt.anchoredPosition = Vector2.zero;
            else
                barImgTF.localPosition = Vector3.zero;
        }

        public void SetFillValue(int value, int count)
        {
            if (count <= 0)
                count = 1;
            if (value == _lastHp && count == _lastMaxHp)
                return;

            _lastHp = value;
            _lastMaxHp = count;
            SetFill(value / (float)count);
            if (isShowNum && numText != null)
                numText.text = string.Format("{0}/{1}", value, count);
        }

        private void SetFill(float p)
        {
            if (_uiTween != null)
                _uiTween.Kill();

            float from = barImgSlow != null ? barImgSlow.fillAmount : p;
            _uiTween = DOTween.To(x =>
            {
                if (barImgSlow != null)
                    barImgSlow.fillAmount = x;
            }, from, p, tweenDuration);
            if (barImg != null)
                barImg.fillAmount = p;
        }

        /// <summary>血条下偏谐条。show=false 时关掉 Image，避免玩家空条。</summary>
        public void SetDazeFill(float ratio, bool show)
        {
            if (barImg_noBreak == null)
                return;
            barImg_noBreak.enabled = show;
            if (show)
                barImg_noBreak.fillAmount = Mathf.Clamp01(ratio);
            else
                SetExecuteHintVisible(false);
        }

        /// <summary>
        /// 偏谐条。Ready 且可处决时在条右侧显示 F；范围内脉冲更明显。真空锁零用暗色空条。不提示 Q/E。
        /// </summary>
        public void SetHarmonyFill(float ratio, bool show, DazePhase phase, bool showExecuteHint, bool executeInRange)
        {
            SetDazeFill(ratio, show);
            if (barImg_noBreak == null)
                return;
            if (!show)
            {
                _hintInRange = false;
                return;
            }

            Color target = ResolveHarmonyColor(phase);
            if (_harmonyBarColor != target)
            {
                _harmonyBarColor = target;
                barImg_noBreak.color = target;
            }

            _hintInRange = showExecuteHint && executeInRange;
            SetExecuteHintVisible(showExecuteHint);
        }

        /// <summary>兼容旧调用：Ready 亮条 + F，其它相位当攒条。</summary>
        public void SetHarmonyFill(float ratio, bool show, bool ready)
        {
            SetHarmonyFill(ratio, show, ready ? DazePhase.Ready : DazePhase.Charging, ready, ready);
        }

        static Color ResolveHarmonyColor(DazePhase phase)
        {
            switch (phase)
            {
                case DazePhase.Ready:
                    return HarmonyReadyColor;
                case DazePhase.Executing:
                    return HarmonyExecutingColor;
                case DazePhase.Vacuum:
                    return HarmonyVacuumColor;
                default:
                    return HarmonyChargingColor;
            }
        }

        public void SetFillValueNoBreak(int value, int count)
        {
            if(count == 0)
            {
                count = 1;
                value = 0;
            }

            SetFillNoBreak(value / (float)count);
        }

        public void SetFillNoBreak(float p)
        {
            if (barImg_noBreak)
            {
                barImg_noBreak.fillAmount = p;
            }
        }

        public void SetTarget(Transform transform)
        {
            target = transform;
            _lastHp = int.MinValue;
            _lastMaxHp = int.MinValue;
            ResetBarChildLocal();
        }

        void SetExecuteHintVisible(bool visible)
        {
            if (_hintVisible == visible)
                return;
            _hintVisible = visible;
            if (visible)
                EnsureExecuteHint();
            if (_executeHint != null)
            {
                _executeHint.gameObject.SetActive(visible);
                if (!visible)
                    _executeHint.rectTransform.localScale = Vector3.one;
            }
        }

        void EnsureExecuteHint()
        {
            if (_executeHint != null || numText == null)
                return;

            Transform parent = barImgTF != null ? barImgTF : transform;
            _executeHint = Instantiate(numText, parent);
            _executeHint.gameObject.name = "HarmonyExecuteHint";
            _executeHint.text = "F";
            _executeHint.alignment = TextAnchor.MiddleLeft;
            _executeHint.raycastTarget = false;
            _executeHint.fontStyle = FontStyle.Bold;
            _executeHint.color = ExecuteHintColor;
            if (_executeHint.fontSize < 22)
                _executeHint.fontSize = 22;
            RectTransform rt = _executeHint.rectTransform;
            rt.anchorMin = new Vector2(1f, 0.5f);
            rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(10f, 5.6f);
            rt.localScale = Vector3.one;
            _executeHint.gameObject.SetActive(false);
        }

        void TickExecuteHintPulse()
        {
            if (!_hintVisible || _executeHint == null || !_executeHint.gameObject.activeSelf)
                return;

            float speed = _hintInRange ? 6.2f : 3.4f;
            float wave = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * speed);
            Color c = ExecuteHintColor;
            c.a = _hintInRange ? 0.55f + 0.45f * wave : 0.72f + 0.18f * wave;
            _executeHint.color = c;
            float s = _hintInRange ? 1f + 0.1f * wave : 1f;
            _executeHint.rectTransform.localScale = new Vector3(s, s, 1f);
        }

        public float scaleRate_Bar = 2;
        public float scaleRate_Trig = 2;

        private float GetScaleByDistance(float d2,float h2)
        {
            float d1 = MainCam.nearClipPlane;
            float h1 = h2 * d1 / d2;
            return h1 * scaleRate_Bar;
        }
    }
}