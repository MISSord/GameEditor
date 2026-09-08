using EGamePlay.Combat;
using UnityEngine;

namespace ACTGameEditor.Combat.Ai
{
    /// <summary>一次打分查询。全是值/引用字段，不装箱。</summary>
    public struct EnemySelectQuery
    {
        public CombatEntity Owner;
        public float Distance;
        public bool HasMeleeToken;
        public bool HasRangedToken;
        public bool HasSpecialToken;
        public int LastSkillId;
        public int PhaseIndex;
        public bool OnScreen;
        public bool TempoBlocksToken;
        public ICooldownQuery CdTimer;
    }

    /// <summary>
    /// D 切片效用选招。只在 Windup 节流调用；乘算 Consideration，无 LINQ。
    /// </summary>
    public sealed class EnemySkillSelector
    {
        public const float RejectScore = 0.05f;

        /// <summary>把 MoveSet 里的 SkillId 挂到 AbilityComponent，与玩家 BindSkillInput 同源。</summary>
        public static void AttachAbilities(AbilityComponent abilities, EnemyMoveSet moveSet)
        {
            if (abilities == null || moveSet?.Moves == null)
                return;

            EnemyMoveEntry[] moves = moveSet.Moves;
            int count = moves.Length;
            if (count > EnemyMoveSet.MaxMoves)
                count = EnemyMoveSet.MaxMoves;

            for (int i = 0; i < count; i++)
            {
                int skillId = moves[i].SkillId;
                if (skillId > 0 && !abilities.IdAbilities.ContainsKey(skillId))
                    abilities.AttachAbility(skillId);
                // 连段第二击也要提前挂能力，否则 Gate 报 NoAbility
                int followUp = moves[i].FollowUpSkillId;
                if (followUp > 0 && !abilities.IdAbilities.ContainsKey(followUp))
                    abilities.AttachAbility(followUp);
            }
        }

        /// <summary>
        /// 取最高分招。score 低于 <see cref="RejectScore"/> 时返回 false，调用方贴近/环绕，不乱放。
        /// </summary>
        public static bool TrySelect(in EnemySelectQuery query, EnemyMoveSet moveSet, out EnemyMoveEntry move, out float score)
        {
            move = default;
            score = 0f;
            if (query.Owner == null || query.Owner.IsDisposed || moveSet?.Moves == null)
                return false;

            EnemyMoveEntry[] moves = moveSet.Moves;
            int count = moves.Length;
            if (count > EnemyMoveSet.MaxMoves)
                count = EnemyMoveSet.MaxMoves;

            float best = -1f;
            int bestIndex = -1;
            for (int i = 0; i < count; i++)
            {
                float s = ScoreMove(in query, in moves[i]);
                if (s <= best)
                    continue;
                best = s;
                bestIndex = i;
            }

            if (bestIndex < 0 || best < RejectScore)
                return false;

            move = moves[bestIndex];
            score = best;
            return true;
        }

        /// <summary>带内 1、Preferred 峰、边缘 0.3、带外 0。</summary>
        public static float DistanceScore(float dist, float minRange, float maxRange, float preferred)
        {
            if (maxRange < minRange)
            {
                float tmp = minRange;
                minRange = maxRange;
                maxRange = tmp;
            }

            if (dist < minRange || dist > maxRange)
                return 0f;

            float span = maxRange - minRange;
            if (span < 0.0001f)
                return 1f;

            preferred = Mathf.Clamp(preferred, minRange, maxRange);
            const float edge = 0.3f;
            if (dist <= preferred)
            {
                float rise = preferred - minRange;
                if (rise < 0.0001f)
                    return 1f;
                return Mathf.Lerp(edge, 1f, (dist - minRange) / rise);
            }

            float fall = maxRange - preferred;
            if (fall < 0.0001f)
                return 1f;
            return Mathf.Lerp(1f, edge, (dist - preferred) / fall);
        }

        static float ScoreMove(in EnemySelectQuery query, in EnemyMoveEntry entry)
        {
            if (entry.SkillId <= 0 || entry.BaseWeight <= 0f)
                return 0f;
            // 假前摇不进选招，只走精英 Orbit 施压路径（TryFeint）
            if (entry.IsFeint)
                return 0f;

            float distance = DistanceScore(query.Distance, entry.MinRange, entry.MaxRange, entry.PreferredRange);
            if (distance <= 0f)
                return 0f;

            float token = TokenScore(in query, in entry);
            if (token <= 0f)
                return 0f;

            if (query.TempoBlocksToken && entry.RequiresToken)
                return 0f;

            float cd = CdScore(query.CdTimer, entry.SkillId);
            if (cd <= 0f)
                return 0f;

            float gate = GateScore(in query, in entry);
            if (gate <= 0f)
                return 0f;

            float phase = PhaseScore(entry.PhaseMask, query.PhaseIndex);
            if (phase <= 0f)
                return 0f;

            float repeat = RepeatScore(entry.SkillId, query.LastSkillId, entry.RepeatPenalty);
            float screen = query.OnScreen ? 1f : 0.2f;
            float jitter = TieJitter(query.Owner.Id, entry.SkillId);

            return entry.BaseWeight * distance * token * cd * gate * phase * repeat * screen + jitter;
        }

        static float TokenScore(in EnemySelectQuery query, in EnemyMoveEntry entry)
        {
            if (!entry.RequiresToken)
                return 1f;
            return entry.TokenKind switch
            {
                EncounterTokenKind.Melee => query.HasMeleeToken ? 1f : 0f,
                EncounterTokenKind.Ranged => query.HasRangedToken ? 1f : 0f,
                EncounterTokenKind.Special => query.HasSpecialToken ? 1f : 0f,
                _ => 0f,
            };
        }

        static float CdScore(ICooldownQuery cdTimer, int skillId)
        {
            if (cdTimer == null)
                return 1f;
            return cdTimer.IsCDEnd(skillId) ? 1f : 0f;
        }

        static float GateScore(in EnemySelectQuery query, in EnemyMoveEntry entry)
        {
            ActivateFail fail = AbilityActivationGate.Evaluate(
                query.Owner,
                entry.SkillId,
                (int)entry.Sort,
                query.CdTimer,
                checkCostAndCooldown: true);
            return fail == ActivateFail.None ? 1f : 0f;
        }

        static float PhaseScore(int phaseMask, int phaseIndex)
        {
            if (phaseMask == 0)
                return 1f;
            int bit = 1 << phaseIndex;
            return (phaseMask & bit) != 0 ? 1f : 0f;
        }

        static float RepeatScore(int skillId, int lastSkillId, float repeatPenalty)
        {
            if (lastSkillId != skillId || lastSkillId <= 0)
                return 1f;
            float p = repeatPenalty;
            if (p < 0f)
                p = 0f;
            else if (p > 1f)
                p = 1f;
            return 1f - p;
        }

        static float TieJitter(long entityId, int skillId)
        {
            unchecked
            {
                uint h = (uint)entityId * 747796405u + (uint)skillId * 2891336453u;
                h ^= h >> 16;
                h *= 0x7feb352d;
                h ^= h >> 15;
                return (h & 1023u) * (0.01f / 1023f);
            }
        }
    }
}
