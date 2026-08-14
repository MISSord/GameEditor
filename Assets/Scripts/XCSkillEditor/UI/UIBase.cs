using UnityEngine;

namespace XiaoCao
{
    public class UIBase : MonoBehaviour
    {
        [HideInInspector]
        public Canvas canvas;
        [HideInInspector]
        public RectTransform canvasRect;
        [HideInInspector]
        public bool IsCanvasInited;

        private RectTransform _rect;
        public RectTransform Rect
        {
            get
            {
                if (_rect == null)
                {
                    _rect = transform as RectTransform;
                }
                return _rect;
            }
        }

        private Camera _mainCam;
        public Camera MainCam
        {
            get
            {
                if(_mainCam == null)
                {
                    _mainCam = Camera.main;
                }
                return _mainCam;
            }
            set
            {
                _mainCam = value;
            }
        }

        public void InitCanvas(Canvas canvas)
        {
            this.canvas = canvas;
            canvasRect = canvas.transform as RectTransform;
            IsCanvasInited = true;
        }

        public virtual void Show()
        {

        }

        public virtual void Hide()
        {

        }

    }
}