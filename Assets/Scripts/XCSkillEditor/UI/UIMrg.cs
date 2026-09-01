using System;
using System.Collections.Generic;
using ACTGameEditor;
using EGamePlay.Combat;
using UnityEngine;

namespace XiaoCao
{
    public class UIMrg : MonoBehaviour, ACTGameEditor.IDamageTextPresenter
    {
        protected static UIMrg _instance = null;
        public static UIMrg Instance
        {
            get
            {
                //if (_instance == null)
                //{
                //    GameObject obj = GameObject.Instantiate(AssetBundleManager.Instance.LoadAssetSync<GameObject>("ui/mainui_prefab", "UIMrg"));
                //    _instance = obj.GetComponent<UIMrg>();
                //    DontDestroyOnLoad(obj);
                //}
                return _instance;
            }
        }

        public Dictionary<SingletonUIType, UIState> uiStateDic = new Dictionary<SingletonUIType, UIState>();

        public Dictionary<SingletonUIType, UIBase> singletonUITDic = new Dictionary<SingletonUIType, UIBase>();
        public Camera UICamera { get; private set; }
        public Canvas[] CanvasList { get; private set; }
        public MainUIPanel MainUIPanel { get; private set; }

        private void Awake()
        {
            _instance = this;
            DontDestroyOnLoad(this);
            ACTGameEditor.DamageTextPresenter.Register(this);
            Init();
        }

        private void OnDestroy()
        {
            ACTGameEditor.DamageTextPresenter.Unregister(this);
            if (_instance == this)
                _instance = null;
        }

        private void Init()
        {
            MainUIPanel = GameObject.Find("MainPanel").GetComponent<MainUIPanel>();
            UICamera =  GameObject.FindWithTag("UICamera").GetComponent<Camera>();
            CanvasList = transform.GetComponentsInChildren<Canvas>();
            if(CanvasList.Length < Enum.GetValues(typeof(UICanvasParent)).Length)
            {
                Debug.LogError("no enough cans");
            }
        }

        public UIBase CallPanel(SingletonUIType uiType, UICanvasParent canvasParent = UICanvasParent.Mid, bool isMoveTop = true)
        {
            UIState uIState = GetUIState(uiType);
            if (uIState == UIState.Null)
            {
                var ui = Instantiate(Resources.Load<UIBase>(PrefabPath.SingletonUI(uiType)));
                ui.transform.SetParent(GetCanvasParenet(canvasParent));
                ui.InitCanvas(CanvasList[(int)uiType]);
                if (isMoveTop)
                    ui.transform.SetAsLastSibling();

                return ui;
            }
            else
            {
                return singletonUITDic[uiType];
            }
        }

        private void Hide(SingletonUIType uiType)
        {
            UIState uIState = GetUIState(uiType);
            if (uIState != UIState.Null)
            {
                singletonUITDic[uiType].Hide();
            }
        }

        private void Show(SingletonUIType uiType)
        {
            UIState uIState = GetUIState(uiType);
            if (uIState != UIState.Null)
            {
                singletonUITDic[uiType].Show();
            }
        }

        private void DestroyUI(SingletonUIType uiType)
        {
            UIState uIState = GetUIState(uiType);
            if (uIState != UIState.Null)
            {
                var ui = singletonUITDic[uiType];
                GameObject.Destroy(ui.gameObject);
                uiStateDic.Remove(uiType);
                singletonUITDic.Remove(uiType);
            }
        }

        private UIState GetUIState(SingletonUIType uiType)
        {
            if (uiStateDic.ContainsKey(uiType) && singletonUITDic.ContainsKey(uiType))
            {
                return uiStateDic[uiType];
            }
            return UIState.Null;
        }

        private Transform GetCanvasParenet(UICanvasParent type)
        {
            if(CanvasList.Length > (int)type)
            {
                return CanvasList[(int)type].transform;
            }
            return null;
        }

        /// <inheritdoc />
        public void ShowDamage(float damageValue, Vector3 worldPosition)
        {
            ShowDamage(new DamageTextRequest(
                damageValue, worldPosition, DamageTextKind.Skill, 0, false, DamageType.Physic, false));
        }

        /// <inheritdoc />
        public void ShowDamage(float damageValue, Vector3 worldPosition, DamageTextKind kind, long targetId)
        {
            ShowDamage(new DamageTextRequest(
                damageValue, worldPosition, kind, targetId, false, DamageType.Physic, false));
        }

        /// <inheritdoc />
        public void ShowDamage(in DamageTextRequest request)
        {
            MainUIPanel.ShowDamageText(request);
        }

        public void PlayDamageText(float damageValue, Vector3 vector3)
        {
            ShowDamage(damageValue, vector3);
        }
    }

    public enum SingletonUIType
    {
        None = 0,
    }

    public enum UIState
    {
        Null,
        OnShow,
        OnHide
    }

    public enum UICanvasParent:int
    {
        Lowest,
        Low,
        Mid,
        Top,
        Topest
    }


}