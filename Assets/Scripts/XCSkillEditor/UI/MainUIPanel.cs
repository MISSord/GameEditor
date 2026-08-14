using ACTGameEditor;
using DG.Tweening;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace XiaoCao
{
    public class MainUIPanel : UIBase
    {
        #region UI
        public Transform CustomHpBarParent;
        public UIBar localUIBar;
        public Transform DamageTextParent;
        //public Transform SkillIconParent;
        //public Transform DisSkillIconParent;
        public DamageTextSetting DamageUITSetting;
        public List<Text> DamageTexts;

        /// <summary> 添加假玩家按钮，点击后在本地玩家旁生成同阵营假玩家。 </summary>
        public Button addFakePlayerButton;
        /// <summary> 添加敌人按钮，点击后在本地玩家前方生成敌人。 </summary>
        public Button addEnemyButton;
        /// <summary> 视角切换按钮的父节点；若与 switchViewButtonPrefab 均赋值则每增加一个单位会生成一个切换视角按钮。 </summary>
        public Transform switchViewButtonParent;
        /// <summary> 视角切换按钮预制体，需带 Button 组件，子节点可有 Text 显示名称。 </summary>
        public GameObject switchViewButtonPrefab;

        //主动与被动技能显示
        //private List<SkillIcon> skillIcons = new List<SkillIcon>();
        //private List<SkillIcon> disSkillIcons = new List<SkillIcon>();

        private List<DamageTextTween> DamageTextTweens = new List<DamageTextTween>();
        // private AgentModelType modelType = AgentModelType.Player;
        private int _nextText;
        private Vector2 _changeVec2;
        #endregion

        #region prefab
        public GameObject uiBarPrefab;
        //public GameObject uiBarPrefab_local;
        //public SkillIcon skillIconPrefab;

        #endregion

        #region data
        //改为netID 比较安全
        public Dictionary<uint, UIBar> uiBarDic = new Dictionary<uint, UIBar>();
        public TypePool<UIBar> barPool;
        /// <summary> NetId -> 视角切换按钮，用于点击时切换摄像机跟随。 </summary>
        private Dictionary<uint, Button> _switchViewButtonDic = new Dictionary<uint, Button>();
        /// <summary> NetId -> 冷却显示组件，每帧刷新冷却信息。 </summary>
        private Dictionary<uint, SwitchViewButtonCooldown> _switchViewCooldownDic = new Dictionary<uint, SwitchViewButtonCooldown>();
        #endregion

        private bool _isReady = false;

        void Awake()
        {
            InitCanvas(GetComponentInParent<Canvas>());
            barPool = new TypePool<UIBar>(uiBarPrefab.GetComponent<UIBar>(), OnRecyleUIBar);
        }

        private void Start()
        {
            foreach (var item in DamageTexts)
            {
                DamageTextTweens.Add(new DamageTextTween { text = item });
            }
            PlayerManager.Instance.AddAckerAct += AddNewBar;
            PlayerManager.Instance.RemoveAckerAct += RemoveOne;

            if (addFakePlayerButton != null)
                addFakePlayerButton.onClick.AddListener(OnAddFakePlayerClick);
            if (addEnemyButton != null)
                addEnemyButton.onClick.AddListener(OnAddEnemyClick);
        }

        private void WaitStart()
        {
            _isReady = true;
        }

        private void OnDestroy()
        {
            PlayerManager.Instance.AddAckerAct -= AddNewBar;
            PlayerManager.Instance.RemoveAckerAct -= RemoveOne;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F2))
            {
                PlayerManager.Instance.AddEnemyFromUI();
            }

            RefreshSwitchViewCooldowns();
        }

        /// <summary> 每帧刷新所有视角切换按钮的冷却显示（参考崩坏3）。 </summary>
        private void RefreshSwitchViewCooldowns()
        {
            if (_switchViewCooldownDic.Count == 0) return;
            float remaining = PlayerManager.Instance.GetRemainingSwitchCooldown();
            float total = PlayerManager.Instance.SwitchCooldownDuration;
            uint currentNetId = PlayerManager.Instance.CurrentFollowNetId;
            foreach (var kv in _switchViewCooldownDic)
            {
                if (kv.Value == null) continue;
                kv.Value.SetCooldown(remaining, total, currentNetId == kv.Key);
            //if (Input.GetKeyDown(KeyCode.F3))
            //{
            //    var player = playerMrg.LocalPlayer;
            //    Vector3 forward = player.transform.forward;
            //    forward.y = 0;

            //    playerMrg.AddFakePlayer(player.transform.position + forward * 5, GameSetting.HasAIEnable, AgentTag.PlayerA, modelType);
            //}

            //if (Input.GetKeyDown(KeyCode.F5))
            //{
            //    //playerMrg.LocalPlayer.skin
            //    int len = Enum.GetValues(typeof(PlayerSkin)).Length;
            //    int next = (int)playerMrg.LocalPlayer.skin + 1;
            //    if (next >= len)
            //    {
            //        next = 0;
            //    }
            //    playerMrg.LocalPlayer.ChangeSkin((PlayerSkin)next);
            //}

            //需要的功能 (AI开关)
            //if (Input.GetKeyDown(KeyCode.F1))
            //{
            //    bool isEnbale = true;
            //    bool isFrist = true;
            //    foreach (var item in playerMrg.MonoAttackerDic.Values)
            //    {
            //        if (!item.isTruePlayer)
            //        {
            //            if (isFrist)
            //            {
            //                isEnbale = !item.AI.enabled;
            //                isFrist = false;
            //                Debug.Log($"yns AI enble {isEnbale} count {playerMrg.MonoAttackerDic.Count}");
            //            }
            //            GameSetting.HasAIEnable = isEnbale;
            //            item.AI.enabled = isEnbale;
            //        }
            //    }
            //    ShowDamageText("AI "+isEnbale, PlayerManager.Instance.LocalPlayer.transform.position, true);
            //}

            //if (Input.GetKeyDown(KeyCode.F4))
            //{
            //    var enums = Enum.GetValues(typeof(AgentModelType));
            //    int len = enums.Length;
            //    modelType = (AgentModelType)(((int)modelType + 1) % len);
            //    ShowDamageText(modelType.ToString(), PlayerManager.Instance.LocalPlayer.transform.position,true);
            //}

            //if (Input.GetKeyDown(KeyCode.F6))
            //{
            //    int len = playerMrg.MonoAttackerDic.Count;
            //    Debug.Log($"len = {len}");

            //    foreach (var item in playerMrg.MonoAttackerDic)
            //    {
            //        Debug.Log($"yns {item.Key} {item.Value.gameObject}");
            //    }
            //}

            //foreach (var item in skillIcons)
            //{
            //    item.OnUpdate();
            //}

            //foreach (var item in disSkillIcons)
            //{
            //    item.OnDisUpdate();
            //}
        }

            
        }

        private void FixedUpdate()
        {
            if (_isReady)
            {
                UpdateUIBars();
            }
        }

        private void AddNewBar(ActPlayer item)
        {
            UIBar newUIBar = null;
            if (item.Combat.isTruePlayer)
            {
                newUIBar = localUIBar;
                newUIBar.SetTarget(item.UINode);
            }
            else
            {
                newUIBar = barPool.GetOne();
                newUIBar.gameObject.SetActive(true);
                newUIBar.transform.SetParent(CustomHpBarParent, true);
                newUIBar.transform.localScale = Vector3.one;
                newUIBar.SetTarget(item.UINode);
            }
            newUIBar.InitCanvas(canvas);
            uiBarDic.Add(item.Combat.NetId, newUIBar);

            AddSwitchViewButton(item);
        }

        /// <summary> 为指定单位创建视角切换按钮，点击后摄像机会跟随该单位。 </summary>
        private void AddSwitchViewButton(ActPlayer item)
        {
            if (switchViewButtonPrefab == null || switchViewButtonParent == null) return;
            GameObject go = Object.Instantiate(switchViewButtonPrefab, switchViewButtonParent);
            go.SetActive(true);
            go.transform.localScale = Vector3.one;

            Button btn = go.GetComponent<Button>();
            if (btn == null) btn = go.GetComponentInChildren<Button>(true);
            if (btn != null)
            {
                uint netId = item.Combat.NetId;
                string label = item.Combat.isTruePlayer ? "玩家(主)" : (item.Agent == AgentTag.enemy ? "敌人" : "假玩家");
                Text labelText = go.GetComponentInChildren<Text>(true);
                if (labelText != null) labelText.text = $"{label} {netId}";

                btn.onClick.AddListener(() => PlayerManager.Instance.SwitchCameraToPlayer(netId));
                _switchViewButtonDic.Add(netId, btn);

                var cooldown = go.GetComponent<SwitchViewButtonCooldown>();
                if (cooldown == null) cooldown = go.GetComponentInChildren<SwitchViewButtonCooldown>(true);
                if (cooldown != null) _switchViewCooldownDic.Add(netId, cooldown);
            }
        }

        private void RemoveOne(uint netID)
        {
            if (uiBarDic.ContainsKey(netID))
            {
                barPool.Recyle(uiBarDic[netID]);
                uiBarDic.Remove(netID);
            }
            else
            {
                Debug.LogWarning($"yns bar RemoveOne no netID {netID}");
            }

            if (_switchViewButtonDic.TryGetValue(netID, out Button btn))
            {
                _switchViewButtonDic.Remove(netID);
                if (btn != null && btn.gameObject != null)
                    Object.Destroy(btn.gameObject);
            }
            _switchViewCooldownDic.Remove(netID);
        }

        private void OnAddFakePlayerClick()
        {
            PlayerManager.Instance.AddFakePlayerFromUI();
        }

        private void OnAddEnemyClick()
        {
            PlayerManager.Instance.AddEnemyFromUI();
        }

        private void OnPlayerValueChange(uint netID)
        {
            //TODO 暂时偷懒
            UpdateUIBars();
        }

        public void UpdateUIBars()
        {
            foreach (var kv in PlayerManager.Instance.MonoAttackerDic)
            {
                var item = kv.Value;
                var NetId = kv.Key;
                if (item == null)
                {
                    RemoveOne(NetId);
                    PlayerManager.Instance.MonoAttackerDic.Remove(NetId);
                }
                else
                {
                    //if (item.IsHideHpBar)
                    //{
                    //    RemoveOne(NetId);
                    //}
                    //else
                    //{
                    //    if (!uiBarDic.ContainsKey(NetId))
                    //    {
                    //        AddNewBar(item);
                    //    }
                    //    uiBarDic[NetId].SetFillValue(item.Hp, item.MaxHp);
                    //    uiBarDic[NetId].SetTagUI(item.Agent);
                    //    uiBarDic[NetId].OnUpdate();
                    //}
                }
            }
        }

        public void ShowDamageText(string num, Vector3 mTarget, bool isBlod = false)
        {
            //获取屏幕坐标  
            Vector3 mScreen = UIMrg.Instance.UICamera.WorldToScreenPoint(mTarget);
            if (mScreen.z < 0)
            {
                return;
            }

            _changeVec2 = Vector2.Scale(DamageUITSetting.randomVec2, Random.insideUnitCircle);  //波动值
            _changeVec2 += DamageUITSetting.offSet;

            mScreen.x += _changeVec2.x;
            mScreen.y += _changeVec2.y;

            UnityEngine.Debug.LogError($"{mScreen}");
            Sequence tween = DamageTextTweens[_nextText].tween;
            if (tween != null)
            {
                tween.Kill();
            }

            DamageTextTweens[_nextText].tween = DOTween.Sequence();
            tween = DamageTextTweens[_nextText].tween;

            _nextText++;
            if (_nextText >= DamageTexts.Count)
            {
                _nextText = 0;
            }

            Text t = DamageTextTweens[_nextText].text;
            t.transform.localScale = Random.Range(DamageUITSetting.randomScaleVec2.x, DamageUITSetting.randomScaleVec2.y) * Vector3.one;
            t.text = num;
            t.rectTransform.anchoredPosition = mScreen;
            tween.Join(DOTween.To(x => t.fontSize = (int)x, DamageUITSetting.frontSizeStart, DamageUITSetting.frontSizeEnd, DamageUITSetting.flyTime / 2).SetLoops(2, LoopType.Yoyo));
            tween.Join(t.rectTransform.DOAnchorPos3DY(mScreen.y + DamageUITSetting.MoveY, DamageUITSetting.flyTime / 2));

            t.color = DamageUITSetting.startColor;

            //tween.Join(t.DOColor(DamageUITSetting.endColor, DamageUITSetting.flyTime / 2));
            //tween.Append(DOTween.To(x => t.fontSize = (int)x, DamageUITSetting.frontSizeMid , DamageUITSetting.frontSizeStart, DamageUITSetting.flyTime/2));

            Color ac = Color.white;
            ac.a = 0;   
            tween.OnComplete(() => { t.color = ac; });

            gameObject.SetActive(true);
        }

        private void OnRecyleUIBar(UIBar bar)
        {
            bar.gameObject.SetActive(false);
        }

    }


    public class DamageTextTween
    {
        public Text text;
        public Sequence tween;
    }

    [System.Serializable]
    public class DamageTextSetting
    {
        public Ease ease;
        public float frontSizeStart = 10;
        public float frontSizeEnd = 32;
        public float MoveY = 5;
        public float flyTime = 0.5f;
        public Color startColor;
        public Color endColor;
        public Vector2 randomVec2;
        public Vector2 randomScaleVec2;
        public Vector2 offSet;
    }


    public static class UITool
    {

        public static Vector2 WorldToAnchorPos(Vector3 position, RectTransform canvasRectTransform)
        {
            Vector3 screenPoint3 = Camera.main.WorldToScreenPoint(position);//世界坐标转换为屏幕坐标
            if (screenPoint3.z < 0)
            {
                screenPoint3 = -screenPoint3;
            }
            Vector2 screenPoint = screenPoint3;
            Vector2 screenSize = new Vector2(Screen.width, Screen.height);
            screenPoint -= screenSize / 2;//将屏幕坐标变换为以屏幕中心为原点
            Vector2 anchorPos = screenPoint / screenSize * canvasRectTransform.sizeDelta;//缩放得到UGUI坐标
            return anchorPos;
        }

        public static Vector3 WorldToUiPostion(Vector3 position, Camera cam = null)
        {
            if (cam == null)
            {
                cam = Camera.main;
            }
            Vector3 screenPoint3 = cam.WorldToScreenPoint(position);//世界坐标转换为屏幕坐标
            if (screenPoint3.z < 0)
            {
                screenPoint3 = -screenPoint3;
            }
            return screenPoint3;
        }

    }
}