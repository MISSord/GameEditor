using UnityEngine;
using UnityEngine.UI;
using NaughtyAttributes;
using DG.Tweening;
using EGamePlay;

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