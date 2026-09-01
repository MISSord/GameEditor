using System.Collections.Generic;
using System.Text;
using ACTGameEditor;
using EGamePlay;
using EGamePlay.Combat;
using UnityEngine;

namespace XiaoCao
{
    /// <summary>
    /// Buff 栏测试用组件：通过 GUI 面板或按键对当前本地玩家添加/移除 Buff。
    /// 挂到场景中任意 GameObject 即可，运行后按 F11 显示/隐藏测试面板。
    /// </summary>
    public class BuffBarTester : MonoBehaviour
    {
        [Header("Test Buff Ids (from BuffDemo config)")]
        [Tooltip("单个添加/移除时使用的 BuffId")]
        public int testBuffId = 1;
        [Tooltip("批量添加时依次使用的 BuffId 列表")]
        public int[] batchBuffIds = new[] { 1, 2, 3 };

        [Header("GUI Panel")]
        [Tooltip("按此键切换测试面板显示/隐藏")]
        public KeyCode togglePanelKey = KeyCode.F11;
        [Tooltip("面板初始位置")]
        public Vector2 panelPosition = new Vector2(10, 120);
        [Tooltip("面板宽度")]
        public float panelWidth = 240f;
        [Tooltip("当前 BuffId 列表区域高度")]
        public float buffListHeight = 140f;

        private bool _showPanel = true;
        private Rect _panelRect;
        private string _buffIdInput = "1";
        private Vector2 _buffListScroll;
        private readonly StringBuilder _listLabel = new StringBuilder(48);

        private void Start()
        {
            _buffIdInput = testBuffId.ToString();
            _panelRect = new Rect(panelPosition.x, panelPosition.y, panelWidth, 10);
        }

        private void Update()
        {
            if (Input.GetKeyDown(togglePanelKey))
                _showPanel = !_showPanel;
        }

        private void OnGUI()
        {
            if (!_showPanel)
            {
                Rect btn = new Rect(panelPosition.x, panelPosition.y, 80, 24);
                if (GUI.Button(btn, "Buff 测试"))
                    _showPanel = true;
                return;
            }

            const float lineH = 24f;
            const float padding = 8f;
            const int controlLines = 9;
            float listH = Mathf.Max(60f, buffListHeight);
            float h = padding * 2 + lineH * controlLines + listH;
            _panelRect.width = panelWidth;
            _panelRect.height = h;
            _panelRect = GUILayout.Window(GetInstanceID(), _panelRect, DrawPanel, "Buff 栏测试", GUILayout.Width(panelWidth), GUILayout.Height(h));
        }

        private void DrawPanel(int id)
        {
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true));

            var statusComp = GetLocalStatusComponent();
            int count = statusComp?.Statuses?.Count ?? 0;
            GUILayout.Label($"当前 Buff 数量: {count}");

            DrawOwnedBuffIdList(statusComp);

            GUILayout.Space(4);
            GUILayout.Label("BuffId:");
            _buffIdInput = GUILayout.TextField(_buffIdInput, 8);
            if (int.TryParse(_buffIdInput, out int parsed))
                testBuffId = parsed;

            GUILayout.Space(4);
            if (GUILayout.Button("添加 Buff", GUILayout.Height(28)))
                AddTestBuff(testBuffId);
            if (GUILayout.Button("移除 Buff", GUILayout.Height(28)))
                RemoveTestBuff(testBuffId);
            if (GUILayout.Button("批量添加", GUILayout.Height(28)))
                AddBatchBuffs();
            if (GUILayout.Button("清空全部", GUILayout.Height(28)))
                RemoveAllBuffs();

            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0, 0, _panelRect.width, 20));
        }

        /// <summary>
        /// 绘制当前角色已拥有的 BuffId 列表。点击某一行会把该 Id 填入输入框，便于移除测试。
        /// </summary>
        private void DrawOwnedBuffIdList(StatusComponent statusComp)
        {
            GUILayout.Space(4);
            GUILayout.Label("当前 Buff Id（点击填入）:");

            float listH = Mathf.Max(60f, buffListHeight);
            _buffListScroll = GUILayout.BeginScrollView(_buffListScroll, GUI.skin.box, GUILayout.Height(listH));

            List<Buff> statuses = statusComp?.Statuses;
            if (statuses == null || statuses.Count == 0)
            {
                GUILayout.Label("（无）");
                GUILayout.EndScrollView();
                return;
            }

            for (int i = 0; i < statuses.Count; i++)
            {
                Buff buff = statuses[i];
                if (buff == null)
                    continue;

                if (GUILayout.Button(BuildBuffListLabel(buff), GUILayout.Height(22)))
                {
                    int buffId = buff.BuffID;
                    _buffIdInput = buffId.ToString();
                    testBuffId = buffId;
                }
            }

            GUILayout.EndScrollView();
        }

        /// <summary>
        /// 拼一行列表文本：优先显示 BuffId，配置有名字时附带名称。
        /// </summary>
        private string BuildBuffListLabel(Buff buff)
        {
            _listLabel.Clear();
            _listLabel.Append(buff.BuffID);
            string name = buff.Setting != null ? buff.Setting.Name : null;
            if (!string.IsNullOrEmpty(name))
            {
                _listLabel.Append("  ");
                _listLabel.Append(name);
            }
            return _listLabel.ToString();
        }

        /// <summary>
        /// 对当前本地玩家添加指定 Buff（可被 UI 按钮或测试代码调用）。
        /// 已存在同 Id 时不重复 Attach，避免 StatusComponent.IdStatuses 抛重复键。
        /// </summary>
        /// <param name="buffId">配置表中的 BuffId</param>
        /// <returns>是否添加成功</returns>
        public bool AddTestBuff(int buffId)
        {
            var statusComp = GetLocalStatusComponent();
            if (statusComp == null) return false;

            if (SkillSettingMgr.Instance.GetBuffDemoSetting(buffId) == null)
            {
                Debug.LogWarning($"[BuffBarTester] BuffId {buffId} 在配置中不存在，请检查 BuffDemo 表。");
                return false;
            }

            if (statusComp.HasBuffId(buffId))
            {
                Debug.Log($"[BuffBarTester] Buff Id={buffId} 已存在，跳过重复添加。当前 Buff 数量={statusComp.Statuses.Count}");
                return true;
            }

            Buff buff = statusComp.AttachStatus(buffId);
            buff?.ActivateBuff();
            Debug.Log($"[BuffBarTester] 添加 Buff Id={buffId}，当前 Buff 数量={statusComp.Statuses.Count}");
            return true;
        }

        /// <summary>
        /// 移除当前本地玩家身上指定 BuffId 的 Buff。
        /// </summary>
        public bool RemoveTestBuff(int buffId)
        {
            var statusComp = GetLocalStatusComponent();
            if (statusComp == null || !statusComp.HasBuffId(buffId)) return false;

            statusComp.RemoveStatus(buffId);
            Debug.Log($"[BuffBarTester] 移除 Buff Id={buffId}，当前 Buff 数量={statusComp.Statuses.Count}");
            return true;
        }

        /// <summary>
        /// 批量添加 batchBuffIds 中的 Buff，用于测试多图标、层数、次数等。
        /// </summary>
        public void AddBatchBuffs()
        {
            if (batchBuffIds == null || batchBuffIds.Length == 0) return;
            int added = 0;
            for (int i = 0; i < batchBuffIds.Length; i++)
            {
                if (AddTestBuff(batchBuffIds[i])) added++;
            }
            Debug.Log($"[BuffBarTester] 批量添加 {added}/{batchBuffIds.Length} 个 Buff");
        }

        /// <summary>
        /// 移除当前本地玩家身上全部 Buff，用于清空 Buff 栏测试。
        /// </summary>
        public void RemoveAllBuffs()
        {
            var statusComp = GetLocalStatusComponent();
            if (statusComp == null || statusComp.Statuses.Count == 0) return;

            int count = statusComp.Statuses.Count;
            for (int i = statusComp.Statuses.Count - 1; i >= 0; i--)
            {
                var buff = statusComp.Statuses[i];
                statusComp.RemoveStatus(buff.BuffID);
            }
            Debug.Log($"[BuffBarTester] 已移除全部 Buff，共 {count} 个");
        }

        /// <summary>
        /// 获取当前本地玩家当前 Buff 数量（供 UI 或测试断言用）。
        /// </summary>
        public static int GetCurrentBuffCount()
        {
            var statusComp = GetLocalStatusComponent();
            return statusComp?.Statuses?.Count ?? 0;
        }

        /// <summary>
        /// 将当前本地玩家身上的 BuffId 写入 dest（先 Clear），返回写入数量。
        /// </summary>
        /// <param name="dest">调用方提供的列表，避免热路径临时分配。</param>
        public static int CopyCurrentBuffIds(List<int> dest)
        {
            if (dest == null)
                return 0;

            dest.Clear();
            var statuses = GetLocalStatusComponent()?.Statuses;
            if (statuses == null)
                return 0;

            for (int i = 0; i < statuses.Count; i++)
            {
                Buff buff = statuses[i];
                if (buff != null)
                    dest.Add(buff.BuffID);
            }
            return dest.Count;
        }

        /// <summary>
        /// 取当前本地玩家的 StatusComponent。玩家未就绪时返回 null。
        /// </summary>
        private static StatusComponent GetLocalStatusComponent()
        {
            return PlayerManager.Instance?.LocalPlayer?.Combat?.GetComponent<StatusComponent>();
        }
    }
}
