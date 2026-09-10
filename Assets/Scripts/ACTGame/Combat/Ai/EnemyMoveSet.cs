using System;
using EGamePlay.Combat;
using UnityEngine;

namespace ACTGameEditor.Combat.Ai
{
    /// <summary>MoveSet 一条招。距离带只活在这里，不进段表。</summary>
    [Serializable]
    public struct EnemyMoveEntry
    {
        public int SkillId;
        public SkillSort Sort;
        public EncounterTokenKind TokenKind;
        public bool RequiresToken;
        public float MinRange;
        public float MaxRange;
        public float PreferredRange;
        public float BaseWeight;
        public float RecoverSeconds;
        public TelegraphKind TelegraphKind;
        /// <summary>
        /// 预警时长（秒，unscaled）。&lt;0 轴上已挂 AiTelegraph，Brain 不播；
        /// 0 用 <see cref="CombatTelegraph.DefaultSeconds"/>；&gt;0 固定秒。
        /// </summary>
        public float TelegraphSeconds;
        public int PhaseMask;
        public float RepeatPenalty;
        public bool IsFeint;
        public int FollowUpSkillId;
    }

    /// <summary>杂兵招表。P0 ScriptableObject，稳定后再考虑 Luban。</summary>
    [CreateAssetMenu(menuName = "ACT/AI/Enemy Move Set", fileName = "EnemyMoveSet")]
    public sealed class EnemyMoveSet : ScriptableObject
    {
        public const int MaxMoves = 8;

        /// <summary>招列表，热路径按数组扫，不要超过 <see cref="MaxMoves"/>。</summary>
        public EnemyMoveEntry[] Moves;

        /// <summary>无资产时的杂兵三招：近劈 / 中距突击 / 近第二刀。走敌人专用 12001–03 轴（与玩家 11001–03 分离）。</summary>
        public static EnemyMoveSet CreateGruntFallback()
        {
            EnemyMoveSet set = CreateInstance<EnemyMoveSet>();
            set.Moves = CreateGruntMoves();
            return set;
        }

        /// <summary>默认三招数据，给资产和运行时回退共用。</summary>
        public static EnemyMoveEntry[] CreateGruntMoves()
        {
            return new[]
            {
                new EnemyMoveEntry
                {
                    SkillId = 12001,
                    Sort = SkillSort.Normal,
                    TokenKind = EncounterTokenKind.Melee,
                    RequiresToken = true,
                    MinRange = 0.8f,
                    MaxRange = 2.8f,
                    PreferredRange = 2.0f,
                    BaseWeight = 1f,
                    RecoverSeconds = 0.65f,
                    TelegraphKind = TelegraphKind.Dodge,
                    TelegraphSeconds = 0.45f,
                    PhaseMask = 1,
                    RepeatPenalty = 0.35f,
                    FollowUpSkillId = 12003,
                },
                new EnemyMoveEntry
                {
                    // 突进斩：轴上带 3.2m 前冲曲线，带收窄到 5.5m 防远端挥空
                    SkillId = 12002,
                    Sort = SkillSort.Normal,
                    TokenKind = EncounterTokenKind.Melee,
                    RequiresToken = true,
                    MinRange = 2.5f,
                    MaxRange = 5.5f,
                    PreferredRange = 4.2f,
                    BaseWeight = 1.15f,
                    RecoverSeconds = 0.8f,
                    TelegraphKind = TelegraphKind.Dodge,
                    TelegraphSeconds = -1f,
                    PhaseMask = 1,
                    RepeatPenalty = 0.45f,
                },
                new EnemyMoveEntry
                {
                    SkillId = 12003,
                    Sort = SkillSort.Normal,
                    TokenKind = EncounterTokenKind.Melee,
                    RequiresToken = true,
                    MinRange = 0.8f,
                    MaxRange = 2.4f,
                    PreferredRange = 1.6f,
                    BaseWeight = 0.9f,
                    RecoverSeconds = 0.7f,
                    TelegraphKind = TelegraphKind.Dodge,
                    TelegraphSeconds = 0.45f,
                    PhaseMask = 1,
                    RepeatPenalty = 0.5f,
                },
                new EnemyMoveEntry
                {
                    SkillId = 12006,
                    Sort = SkillSort.Normal,
                    TokenKind = EncounterTokenKind.Melee,
                    RequiresToken = true,
                    MinRange = 0.8f,
                    MaxRange = 5.5f,
                    PreferredRange = 2.4f,
                    BaseWeight = 2.2f,
                    RecoverSeconds = 0.7f,
                    TelegraphKind = TelegraphKind.Parry,
                    TelegraphSeconds = 0.5f,
                    PhaseMask = 1,
                    RepeatPenalty = 0.4f,
                },
            };
        }
    }
}
