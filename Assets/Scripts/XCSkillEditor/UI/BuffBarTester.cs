using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using ACTGameEditor;
using EGamePlay;
using EGamePlay.Combat;
using UnityEngine;

namespace XiaoCao
{
    /// <summary>
    /// Buff 栏测试用组件：通过 GUI 面板对场景中选中单位添加/移除 Buff。
    /// 挂到场景中任意 GameObject 即可，运行后按 F11 显示/隐藏测试面板。
    /// </summary>
    public class BuffBarTester : MonoBehaviour
    {
        const string CloneSuffix = "(Clone)";

        [Header("Test Buff Ids (from BuffDemo config)")]
        [Tooltip("单个添加/移除时使用的 BuffId")]
        public int testBuffId = 20601;
        [Tooltip("批量添加时依次使用的 BuffId 列表")]
        public int[] batchBuffIds = new[] { 1, 2, 3 };

        [Header("GUI Panel")]
        [Tooltip("按此键切换测试面板显示/隐藏")]
        public KeyCode togglePanelKey = KeyCode.F11;
        [Tooltip("面板初始位置")]
        public Vector2 panelPosition = new Vector2(10, 120);
        [Tooltip("面板宽度")]
        public float panelWidth = 280f;
        [Tooltip("当前 BuffId 列表区域高度")]
        public float buffListHeight = 140f;

        static BuffBarTester _active;
        static readonly Comparison<ActPlayer> CompareByNetId = ComparePlayersByNetId;

        private bool _showPanel = true;
        private Rect _panelRect;
        private string _buffIdInput = "20601";
        private string _durationInput = "";
        private Vector2 _buffListScroll;
        private readonly StringBuilder _listLabel = new StringBuilder(48);
        private readonly StringBuilder _targetLabel = new StringBuilder(64);
        private readonly List<ActPlayer> _targets = new List<ActPlayer>(8);
        private ActPlayer _selected;
        private int _targetIndex;

        private void OnEnable()
        {
            _active = this;
        }

        private void OnDisable()
        {
            if (_active == this)
                _active = null;
        }

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
            const int controlLines = 13;
            float listH = Mathf.Max(60f, buffListHeight);
            float h = padding * 2 + lineH * controlLines + listH;
            _panelRect.width = panelWidth;
            _panelRect.height = h;
            _panelRect = GUILayout.Window(GetInstanceID(), _panelRect, DrawPanel, "Buff 栏测试", GUILayout.Width(panelWidth), GUILayout.Height(h));
        }

        private void DrawPanel(int id)
        {
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true));

            RefreshTargetsAndSelection();
            DrawTargetSwitcher();

            var statusComp = GetSelectedStatusComponent();
            int count = statusComp?.Statuses?.Count ?? 0;
            GUILayout.Label($"当前 Buff 数量: {count}");

            DrawOwnedBuffIdList(statusComp);

            GUILayout.Space(4);
            GUILayout.Label("BuffId:");
            _buffIdInput = GUILayout.TextField(_buffIdInput, 8);
            if (int.TryParse(_buffIdInput, out int parsed))
                testBuffId = parsed;

            GUILayout.Label("时长(秒，空=配置):");
            _durationInput = GUILayout.TextField(_durationInput, 12);
            GUILayout.Label(TryParseDurationOverride(out float previewSeconds)
                ? $"将使用 {previewSeconds} 秒"
                : "将使用配置时长");

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
        /// 左右切换生效目标，中间显示对象名；主控角色会在名字后标「主控」。
        /// </summary>
        private void DrawTargetSwitcher()
        {
            GUILayout.BeginHorizontal();
            bool canCycle = _targets.Count > 1;
            GUI.enabled = canCycle;
            if (GUILayout.Button("<", GUILayout.Width(28), GUILayout.Height(28)))
                CycleTarget(-1);
            GUI.enabled = true;
            GUILayout.Box(BuildTargetLabel(), GUILayout.Height(28), GUILayout.ExpandWidth(true));
            GUI.enabled = canCycle;
            if (GUILayout.Button(">", GUILayout.Width(28), GUILayout.Height(28)))
                CycleTarget(1);
            GUI.enabled = true;
            GUILayout.EndHorizontal();
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
        /// 对当前选中目标添加指定 Buff（可被 UI 按钮或测试代码调用）。
        /// 已存在同 Id 时不重复 Attach，避免 StatusComponent.IdStatuses 抛重复键。
        /// </summary>
        /// <param name="buffId">配置表中的 BuffId</param>
        /// <returns>是否添加成功</returns>
        public bool AddTestBuff(int buffId)
        {
            var statusComp = GetSelectedStatusComponent();
            if (statusComp == null) return false;

            BuffDemoSetting setting = SkillSettingMgr.Instance.GetBuffDemoSetting(buffId);
            if (setting == null || setting.BuffId != buffId)
            {
                Debug.LogWarning($"[BuffBarTester] BuffId {buffId} 在配置中不存在，请检查 BuffDemo 表。");
                return false;
            }

            if (statusComp.HasBuffId(buffId))
            {
                Debug.Log($"[BuffBarTester] {BuildTargetLabel()} 已有 Buff Id={buffId}，跳过。当前 Buff 数量={statusComp.Statuses.Count}");
                return true;
            }

            Buff buff = statusComp.AttachStatus(buffId);
            if (buff == null)
                return false;
            if (buff.Caster == null)
                buff.Caster = statusComp.Entity;
            bool overrideDuration = TryParseDurationOverride(out float durationSeconds);
            if (overrideDuration)
                ApplyDurationOverride(buff, durationSeconds);
            buff.ActivateBuff();
            if (overrideDuration)
                Debug.Log($"[BuffBarTester] 对 {BuildTargetLabel()} 添加 Buff Id={buffId}，时长 {durationSeconds} 秒，当前 Buff 数量={statusComp.Statuses.Count}");
            else
                Debug.Log($"[BuffBarTester] 对 {BuildTargetLabel()} 添加 Buff Id={buffId}，时长走配置，当前 Buff 数量={statusComp.Statuses.Count}");
            return true;
        }

        /// <summary>
        /// 空或无法解析为正数秒时返回 false，添加时走配置 BaseDuration。
        /// </summary>
        private bool TryParseDurationOverride(out float seconds)
        {
            seconds = 0f;
            if (string.IsNullOrWhiteSpace(_durationInput))
                return false;

            string text = _durationInput.Trim();
            if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out seconds)
                && !float.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out seconds))
                return false;

            return seconds > 0f && !float.IsNaN(seconds) && !float.IsInfinity(seconds);
        }

        /// <summary>
        /// 在 Activate 前改 BuffMaxTime，这样 TimeBuff 注册结束定时器时用覆盖后的秒数。
        /// 非 TimeBuff 没有时长属性则忽略。
        /// </summary>
        private static void ApplyDurationOverride(Buff buff, float seconds)
        {
            if (buff == null)
                return;

            FloatNumeric duration = buff.GetFloatNumeric(AttributeType.BuffMaxTime);
            if (duration == null)
                return;

            duration.SetBase(seconds);
        }

        /// <summary>
        /// 移除当前选中目标身上指定 BuffId 的 Buff。
        /// </summary>
        public bool RemoveTestBuff(int buffId)
        {
            var statusComp = GetSelectedStatusComponent();
            if (statusComp == null || !statusComp.HasBuffId(buffId)) return false;

            statusComp.RemoveStatus(buffId);
            Debug.Log($"[BuffBarTester] 从 {BuildTargetLabel()} 移除 Buff Id={buffId}，当前 Buff 数量={statusComp.Statuses.Count}");
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
            Debug.Log($"[BuffBarTester] 对 {BuildTargetLabel()} 批量添加 {added}/{batchBuffIds.Length} 个 Buff");
        }

        /// <summary>
        /// 移除当前选中目标身上全部 Buff，用于清空 Buff 栏测试。
        /// </summary>
        public void RemoveAllBuffs()
        {
            var statusComp = GetSelectedStatusComponent();
            if (statusComp == null || statusComp.Statuses.Count == 0) return;

            int count = statusComp.Statuses.Count;
            for (int i = statusComp.Statuses.Count - 1; i >= 0; i--)
            {
                var buff = statusComp.Statuses[i];
                statusComp.RemoveStatus(buff.BuffID);
            }
            Debug.Log($"[BuffBarTester] 已清空 {BuildTargetLabel()} 全部 Buff，共 {count} 个");
        }

        /// <summary>
        /// 获取当前选中目标 Buff 数量（供 UI 或测试断言用）。无选中时回退本地主控。
        /// </summary>
        public static int GetCurrentBuffCount()
        {
            var statusComp = GetActiveStatusComponent();
            return statusComp?.Statuses?.Count ?? 0;
        }

        /// <summary>
        /// 将当前选中目标身上的 BuffId 写入 dest（先 Clear），返回写入数量。
        /// </summary>
        /// <param name="dest">调用方提供的列表，避免热路径临时分配。</param>
        public static int CopyCurrentBuffIds(List<int> dest)
        {
            if (dest == null)
                return 0;

            dest.Clear();
            var statuses = GetActiveStatusComponent()?.Statuses;
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
        /// 从 PlayerManager 已注册的场景单位刷新列表，并尽量保住当前选中。
        /// </summary>
        private void RefreshTargetsAndSelection()
        {
            _targets.Clear();
            Dictionary<uint, ActPlayer> dic = PlayerManager.Instance?.MonoAttackerDic;
            if (dic != null)
            {
                foreach (KeyValuePair<uint, ActPlayer> kv in dic)
                {
                    ActPlayer player = kv.Value;
                    if (player == null || player.Combat == null)
                        continue;
                    if (!player.gameObject.activeInHierarchy)
                        continue;
                    _targets.Add(player);
                }
            }

            if (_targets.Count > 1)
                _targets.Sort(CompareByNetId);

            if (_selected != null)
            {
                int kept = IndexOfTarget(_selected);
                if (kept >= 0)
                {
                    _targetIndex = kept;
                    return;
                }
            }

            SelectDefaultTarget();
        }

        private void SelectDefaultTarget()
        {
            ActPlayer local = PlayerManager.Instance?.LocalPlayer;
            int localIndex = local != null ? IndexOfTarget(local) : -1;
            if (localIndex >= 0)
            {
                _targetIndex = localIndex;
                _selected = local;
                return;
            }

            if (_targets.Count == 0)
            {
                _targetIndex = 0;
                _selected = null;
                return;
            }

            _targetIndex = 0;
            _selected = _targets[0];
        }

        private void CycleTarget(int direction)
        {
            RefreshTargetsAndSelection();
            int count = _targets.Count;
            if (count == 0)
                return;

            _targetIndex = (_targetIndex + direction) % count;
            if (_targetIndex < 0)
                _targetIndex += count;
            _selected = _targets[_targetIndex];
        }

        private int IndexOfTarget(ActPlayer player)
        {
            for (int i = 0; i < _targets.Count; i++)
            {
                if (_targets[i] == player)
                    return i;
            }
            return -1;
        }

        private string BuildTargetLabel()
        {
            _targetLabel.Clear();
            if (_selected == null)
            {
                _targetLabel.Append("（无目标）");
                return _targetLabel.ToString();
            }

            _targetLabel.Append(ResolveDisplayName(_selected));
            if (IsLocalControlled(_selected))
                _targetLabel.Append(" 主控");

            int total = _targets.Count;
            if (total > 0)
            {
                _targetLabel.Append(' ');
                _targetLabel.Append(_targetIndex + 1);
                _targetLabel.Append('/');
                _targetLabel.Append(total);
            }
            return _targetLabel.ToString();
        }

        static string ResolveDisplayName(ActPlayer player)
        {
            string name = player.gameObject.name;
            if (name.EndsWith(CloneSuffix, StringComparison.Ordinal))
                return name.Substring(0, name.Length - CloneSuffix.Length);
            return name;
        }

        static bool IsLocalControlled(ActPlayer player)
        {
            PlayerManager pm = PlayerManager.Instance;
            return pm != null && pm.LocalPlayer == player;
        }

        static int ComparePlayersByNetId(ActPlayer a, ActPlayer b)
        {
            uint na = a?.Combat != null ? a.Combat.NetId : uint.MaxValue;
            uint nb = b?.Combat != null ? b.Combat.NetId : uint.MaxValue;
            return na.CompareTo(nb);
        }

        /// <summary>
        /// 取当前选中目标的 StatusComponent。未就绪时返回 null。
        /// </summary>
        private StatusComponent GetSelectedStatusComponent()
        {
            return _selected?.Combat?.Status;
        }

        /// <summary>
        /// 静态入口：优先用当前激活的测试面板选中目标，否则回退本地主控。
        /// </summary>
        private static StatusComponent GetActiveStatusComponent()
        {
            if (_active != null)
            {
                _active.RefreshTargetsAndSelection();
                StatusComponent selected = _active.GetSelectedStatusComponent();
                if (selected != null)
                    return selected;
            }

            return PlayerManager.Instance?.LocalPlayer?.Combat?.Status;
        }
    }
}
