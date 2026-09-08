using Sirenix.OdinInspector;
using UnityEngine;
using EGamePlay;
using ACTGameEditor;
using ACTGameEditor.Combat;

#if UNITY_EDITOR

/// <summary>
/// 测试/调试统一入口（编辑器）：Odin 按钮 + 调试快捷键。
/// 测试方法一律挂这里，不要污染 CameraManager / MainUIPanel / 渲染控制器等正常逻辑。
///
/// 热键一览：
///   F2 刷杂兵 / F3 刷精英 / F4 生成测试小队（4 杂兵 + 2 精英）
///   F7 技能镜头测试 / F8 震屏测试 / F9 时空断裂 / F10 普攻组等级切换
///   数字键 5 显现球 / 6 深度视界 / 7 显现锥 / 8 玩家雾
/// </summary>
public class SkillEditorScene : MonoBehaviour
{
    [Header("添加一个Npc")]
    public AgentModelType agentName;
    public AgentTag agentTag;
    public Vector3 startPos = Vector3.zero;

    [Header("测试小队")]
    [Tooltip("杂兵相对玩家前方扇形分布的距离（米）")]
    public float gruntDistance = 5f;
    [Tooltip("精英体型放大倍数，战场上好辨认")]
    public float eliteScale = 1.3f;

    [Button("添加Npc")]
    public void AddNpc()
    {
        PlayerManager.Instance.AddFakePlayer(startPos, false, agentTag, agentName);
    }

    /// <summary>4 只杂兵前方扇形站位 + 2 只精英放两翼稍后，精英体型放大。</summary>
    [Button("生成测试小队（4 杂兵 + 2 精英）")]
    public void SpawnTestSquad()
    {
        PlayerManager pm = PlayerManager.Instance;
        if (pm == null)
            return;

        Transform lt = pm.LocalPlayer != null ? pm.LocalPlayer.transform : null;
        Vector3 forward = lt != null ? lt.forward : Vector3.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.forward;
        forward.Normalize();
        Vector3 origin = lt != null ? lt.position : Vector3.zero;

        // 杂兵：前方扇形 4 只（≈进入内圈 4 槽）
        float[] gruntAngles = { -60f, -20f, 20f, 60f };
        for (int i = 0; i < gruntAngles.Length; i++)
        {
            Vector3 dir = Quaternion.AngleAxis(gruntAngles[i], Vector3.up) * forward;
            pm.AddFakePlayer(origin + dir * gruntDistance, true, AgentTag.enemy, AgentModelType.Player);
        }

        // 精英：两翼稍后一点，体型放大方便观察 Special / 假前摇
        float[] eliteAngles = { -95f, 95f };
        for (int i = 0; i < eliteAngles.Length; i++)
        {
            Vector3 dir = Quaternion.AngleAxis(eliteAngles[i], Vector3.up) * forward;
            ActPlayer elite = pm.AddFakePlayer(origin + dir * (gruntDistance + 1.5f), true, AgentTag.enemy, AgentModelType.Player);
            if (elite != null)
                elite.transform.localScale = Vector3.one * eliteScale;
        }
    }

    /// <summary>本地玩家普攻组在 1 与 MaxLevel 间切换，便于核对 RatioByLevel（原 MainUIPanel.F9）。</summary>
    [Button("切换普攻组等级（1/满级）")]
    public void DebugToggleSkillGroupLevel()
    {
        var player = PlayerManager.Instance != null ? PlayerManager.Instance.LocalPlayer : null;
        if (player == null)
            return;
        var levels = player.Combat?.SkillLevels;
        if (levels == null)
            return;

        const int debugSkillId = 11001;
        int current = levels.GetLevel(debugSkillId);
        int max = SkillSettingMgr.Instance != null
            ? SkillSettingMgr.Instance.ResolveSkillMaxLevel(debugSkillId)
            : 10;
        int next = current >= max ? 1 : max;
        int applied = levels.SetLevelBySkill(debugSkillId, next);

        var mgr = SkillSettingMgr.Instance;
        float r1 = mgr?.GetSkillDamageSetting(11001, 1)?.GetRatioAtLevel(applied) ?? 0f;
        float r21 = mgr?.GetSkillDamageSetting(11002, 1)?.GetRatioAtLevel(applied) ?? 0f;
        float r22 = mgr?.GetSkillDamageSetting(11002, 2)?.GetRatioAtLevel(applied) ?? 0f;
        float r3 = mgr?.GetSkillDamageSetting(11003, 1)?.GetRatioAtLevel(applied) ?? 0f;
        Debug.Log($"[SkillLevel] 普攻组 lv={applied} ratio 11001={r1} 11002={r21}/{r22} 11003={r3}");
    }

    void Update()
    {
        TickDebugHotkeys();
    }

    void TickDebugHotkeys()
    {
        PlayerManager pm = PlayerManager.Instance;
        if (pm == null)
            return;

        // ── 生成 ──
        if (Input.GetKeyDown(KeyCode.F2))
            pm.AddEnemyFromUI();
        else if (Input.GetKeyDown(KeyCode.F3))
            pm.AddEliteEnemyFromUI();
        else if (Input.GetKeyDown(KeyCode.F4))
            SpawnTestSquad();

        // ── 战斗表现调试 ──
        if (Input.GetKeyDown(KeyCode.F7))
            CameraManager.Instance?.DebugPlaySkillCamIntent();
        else if (Input.GetKeyDown(KeyCode.F8))
            CameraShakeController.Instance?.Play(CameraShakeProfile.Heavy(), ignoreGate: true);
        else if (Input.GetKeyDown(KeyCode.F9))
        {
            CombatEntity owner = pm.LocalPlayer != null ? pm.LocalPlayer.Combat : null;
            CombatFxPackagePlayer.PlayDebugTimeFracture(owner, 2f);
        }
        else if (Input.GetKeyDown(KeyCode.F10))
            DebugToggleSkillGroupLevel();

        // ── 渲染开关（原各控制器上的数字键轮询）──
        if (Input.GetKeyDown(KeyCode.Alpha5))
            FindObjectOfType<RevealVisionController>()?.Toggle();
        else if (Input.GetKeyDown(KeyCode.Alpha6))
            FindObjectOfType<DepthVisionController>()?.Toggle();
        else if (Input.GetKeyDown(KeyCode.Alpha7))
        {
            RevealConeController cone = FindObjectOfType<RevealConeController>();
            if (cone != null && !cone.HoldToReveal)
                cone.Toggle();
        }
        else if (Input.GetKeyDown(KeyCode.Alpha8))
            FindObjectOfType<PlayerFogController>()?.Toggle();
    }
}
#endif
