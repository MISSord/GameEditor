using UnityEngine;

namespace ACTGameEditor.Combat.Ai
{
    /// <summary>
    /// 导演调参资产。缺资产时用默认值（即代码内初值），不会生成失败。
    /// 难度旋钮只动这里：预算 / 冷却 / 拍卖周期，不动伤害表。
    /// </summary>
    [CreateAssetMenu(menuName = "ACT/AI/Encounter Director Profile", fileName = "EncounterDirectorProfile")]
    public sealed class EncounterDirectorProfile : ScriptableObject
    {
        public const string ResourcePath = "Config/Ai/DefaultEncounter";

        [Header("预算（同时出手数）")]
        public int MeleeBudget = 1;
        [Tooltip("精英重击/点名额；场上有精英才开 1")]
        public int SpecialBudget = 1;

        [Header("租约与冷却（秒，世界钟）")]
        public float GrantTimeout = 1.4f;
        public float PerEnemyCooldown = 0.8f;
        public float FailedLaunchTimeout = 0.35f;
        [Tooltip("拍卖周期：两次发放的最小间隔，即进攻频率旋钮")]
        public float GrantInterval = 0.25f;

        [Header("Relax 空窗（秒）")]
        public float RelaxPerfectDodge = 0.45f;
        public float RelaxNormalDodge = 0.15f;
        public float RelaxWhiff = 0.25f;
        [Tooltip("招架成功空窗；运行时优先用 11005 时长+余量")]
        public float RelaxParry = 0.85f;

        [Header("动态进攻欲望（雷火文：车轮战）")]
        public float DesireBasePerSec = 0.45f;
        public float DesireSlotBonusPerSec = 0.35f;
        public float DesireCap = 1.0f;
        [Tooltip("内外圈轮换的欲望差阈值")]
        public float SwapDesireMargin = 0.4f;

        [Header("抢夺与打分")]
        public float StealGrace = 0.35f;
        public float StealHysteresis = 1.05f;
        [Tooltip("持牌者已进战术 Windup 时，其竞拍分再乘这个系数，减少刚抬手就被抢走")]
        public float StealWindupProtect = 1.2f;
        public float DistanceScoreSpan = 12f;
        public float OffscreenScore = 0.15f;
        [Tooltip("攻击请求有效期；Brain 每帧续期")]
        public float WantTtl = 0.3f;

        static EncounterDirectorProfile _runtime;

        /// <summary>当前生效调参；缺 Resources 资产时回退默认实例（不反复 new）。</summary>
        public static EncounterDirectorProfile Active
        {
            get
            {
                if (_runtime != null)
                    return _runtime;
                _runtime = Resources.Load<EncounterDirectorProfile>(ResourcePath);
                if (_runtime == null)
                    _runtime = CreateInstance<EncounterDirectorProfile>();
                return _runtime;
            }
        }

        /// <summary>切场景 / 重进时丢弃缓存（编辑器改资产后重新进 Play 生效）。</summary>
        public static void ResetCache() => _runtime = null;
    }
}
