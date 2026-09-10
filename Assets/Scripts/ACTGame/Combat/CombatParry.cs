using ACTGameEditor.Combat.Ai;
using EGamePlay;
using EGamePlay.Combat;
using UnityEngine;

namespace ACTGameEditor.Combat
{
    /// <summary>
    /// 黄闪招架（极限支援模型）：窗在来刀上，按键成交并吸附；11005 只播演出。
    /// 不问技能名，问 Tag。表现不进 DamageAction。
    /// </summary>
    public static class CombatParry
    {
        /// <summary>玩家招架演出轴。</summary>
        public const int PlayerSkillId = 11005;

        /// <summary>11005 轴未导出时借用普攻 1（避免翻滚位移把吸附冲掉）；成交后 ParryWindow 当 i-frame。</summary>
        public const int PlayerFallbackTimelineSkillId = 11001;

        /// <summary>敌人黄闪近劈。</summary>
        public const int EnemyYellowSkillId = 12006;

        /// <summary>12006 轴未导出时借用 12001 盒；Session 入轴仍登记来刀。</summary>
        public const int EnemyFallbackTimelineSkillId = 12001;

        /// <summary>表缺列时的失衡倍率（Impact 100 → 35 点）。</summary>
        public const float DefaultParryDazeRatio = 0.35f;

        /// <summary>吸附后与攻击者的水平间距（米）。</summary>
        public const float SnapDistance = 1.35f;

        /// <summary>最远可成交距离（米）。超出按 L 不成交。</summary>
        public const float MaxCommitRange = 12f;

        /// <summary>11005 轴读不到时长时的振刀目标秒数（约 16 帧 @30fps）。</summary>
        public const float DefaultPlayerParrySeconds = 0.55f;

        /// <summary>玩家振刀结束后敌人再停一拍，让玩家先能动。</summary>
        public const float EnemyStunAfterPlayer = 0.28f;

        const int Capacity = 8;
        const float MaxCommitRangeSq = MaxCommitRange * MaxCommitRange;
        const float DefaultWindowLife = 2.5f;

        struct IncomingStrike
        {
            public CombatEntity Attacker;
            public CombatEntity Defender;
            public long RunnerId;
            public float ExpireAtWorld;
            public bool BreakSkill;
            public bool Consumed;
            public bool WindowOpen;
            public bool PendingExpire;
        }

        static readonly IncomingStrike[] _strikes = new IncomingStrike[Capacity];

        /// <summary>是否为玩家招架演出技。</summary>
        public static bool IsPlayerParrySkill(int skillId) => skillId == PlayerSkillId;

        /// <summary>是否为 M2 黄闪敌招（轴缺失时 Session 仍要挂可招架 Tag）。</summary>
        public static bool IsEnemyYellowSkill(int skillId) => skillId == EnemyYellowSkillId;

        /// <summary>
        /// 黄闪预警亮起时开窗（早于 Session）。玩家在闪的当下按 L 才能成交。
        /// 问招表 <see cref="TelegraphKind.Parry"/>，不把所有闪都当成可招架。
        /// </summary>
        public static void ArmIncoming(
            CombatEntity attacker,
            CombatEntity threatened,
            int skillId,
            float telegraphSeconds,
            TelegraphKind telegraphKind)
        {
            if (attacker == null || attacker.IsDisposed)
            {
                Log($"arm skip attacker null/disposed skill={skillId}");
                return;
            }

            if (telegraphKind != TelegraphKind.Parry && !IsEnemyYellowSkill(skillId))
            {
                Log($"arm skip not-yellow skill={skillId} telegraph={telegraphKind} attacker={attacker.Id}");
                return;
            }

            CombatEntity defender = threatened;
            if (defender == null || defender.IsDisposed || defender.IsDead)
                defender = CombatEncounterDirector.Instance != null
                    ? CombatEncounterDirector.Instance.FocusTarget
                    : null;
            if (defender == null || defender.IsDisposed || defender.IsDead)
            {
                Log($"arm skip no defender skill={skillId} attacker={attacker.Id}");
                return;
            }

            float life = telegraphSeconds > 0.01f ? telegraphSeconds + 2f : DefaultWindowLife;
            long runnerId = attacker.ActiveExecution != null ? attacker.ActiveExecution.Id : 0;
            Register(attacker, defender, runnerId, breakSkill: true, GameTimeManager.WorldTime + life);
            Log($"arm skill={skillId} attacker={attacker.Id} defender={defender.Id} runner={runnerId} life={life:0.00} dist={PlanarDistance(defender, attacker):0.00}");
        }

        /// <summary>
        /// 敌人黄闪轴启动：推可招架 Tag，并把来刀绑到 Runner。
        /// M2 单段同时推断轴 Tag；多段只消伤的招以后只挂 Parryable。
        /// </summary>
        public static void TryBindAttackerTags(
            CombatEntity caster,
            CombatEntity threatened,
            int skillId,
            long runnerId)
        {
            if (caster == null || caster.IsDisposed || !IsEnemyYellowSkill(skillId) || runnerId == 0)
                return;

            TagSource src = TagSource.Skill(runnerId);
            caster.PushTag(src, CombatTags.AiParryable);
            caster.PushTag(src, CombatTags.AiParryBreak);

            CombatEntity defender = threatened;
            if (defender == null || defender.IsDisposed || defender.IsDead)
                defender = CombatEncounterDirector.Instance != null
                    ? CombatEncounterDirector.Instance.FocusTarget
                    : null;
            if (defender == null || defender.IsDisposed || defender.IsDead)
                return;

            float expire = GameTimeManager.WorldTime + DefaultWindowLife;
            for (int i = 0; i < Capacity; i++)
            {
                if (_strikes[i].Attacker != null && _strikes[i].Attacker.Id == caster.Id && _strikes[i].ExpireAtWorld > expire)
                    expire = _strikes[i].ExpireAtWorld;
            }

            Register(caster, defender, runnerId, breakSkill: true, expire);
            Log($"bind runner={runnerId} attacker={caster.Id} defender={defender.Id} expireIn={expire - GameTimeManager.WorldTime:0.00}");
        }

        /// <summary>技能轴结束：关掉来刀窗；已成交的标记留到下一帧输入，让本帧 Flush 仍能消伤。</summary>
        public static void NotifyRunnerFinished(long runnerId)
        {
            if (runnerId == 0)
                return;
            for (int i = 0; i < Capacity; i++)
            {
                if (_strikes[i].RunnerId != runnerId)
                    continue;
                if (_strikes[i].Consumed)
                {
                    _strikes[i].WindowOpen = false;
                    _strikes[i].PendingExpire = true;
                }
                else
                {
                    Free(i);
                }
                return;
            }
        }

        /// <summary>清掉上一帧已结束的成交记录。在玩家出招 Tick 开头调。</summary>
        public static void ExpireFinishedStrikes()
        {
            for (int i = 0; i < Capacity; i++)
            {
                if (_strikes[i].PendingExpire)
                    Free(i);
            }
        }

        /// <summary>玩家面前是否有可成交的黄闪来刀（距离内、未消费、轴仍在）。</summary>
        public static bool HasIncomingStrike(CombatEntity defender)
        {
            return TryFindLiveWindow(defender, out _);
        }

        /// <summary>
        /// 成交：吸附到刀路、加失衡、可选断轴。成功后调用方再播 11005 演出。
        /// </summary>
        public static bool TryCommit(CombatEntity defender, out CombatEntity attacker)
        {
            attacker = null;
            if (defender == null || defender.IsDisposed || defender.IsDead)
                return false;
            if (!TryFindLiveWindow(defender, out int index))
                return false;

            ref IncomingStrike strike = ref _strikes[index];
            attacker = strike.Attacker;
            attacker.GetComponent<ActSpellComponent>()?.ClearQueue();
            SnapToClash(defender, attacker);
            strike.Consumed = true;
            strike.WindowOpen = false;
            ApplySuccess(defender, attacker, strike.BreakSkill);
            Log($"commit ok attacker={attacker.Id} defender={defender.Id} break={strike.BreakSkill}");
            return true;
        }

        /// <summary>按键未成交时的来刀表快照，给 Console 定位用。</summary>
        public static string FormatWindows(CombatEntity defender)
        {
            int n = 0;
            string dump = "";
            for (int i = 0; i < Capacity; i++)
            {
                IncomingStrike s = _strikes[i];
                if (s.Attacker == null && s.RunnerId == 0 && !s.WindowOpen)
                    continue;
                n++;
                float dist = defender != null && s.Attacker != null
                    ? PlanarDistance(defender, s.Attacker)
                    : -1f;
                float remain = s.ExpireAtWorld > 0f ? s.ExpireAtWorld - GameTimeManager.WorldTime : -1f;
                dump += $" [{i} atk={s.Attacker?.Id ?? 0} def={s.Defender?.Id ?? 0} open={s.WindowOpen} consumed={s.Consumed} runner={s.RunnerId} dist={dist:0.00} remain={remain:0.00}]";
            }

            return n == 0 ? "none" : $"count={n}{dump}";
        }

        /// <summary>战局销毁时清空来刀表。</summary>
        public static void ClearAll()
        {
            for (int i = 0; i < Capacity; i++)
                _strikes[i] = default;
        }

        /// <summary>本段来刀已成交：后续盒打到原目标只标招架，不再结算成功。</summary>
        public static bool IsConsumedHit(CombatEntity attacker, CombatEntity defender)
        {
            if (attacker == null || defender == null)
                return false;
            for (int i = 0; i < Capacity; i++)
            {
                IncomingStrike s = _strikes[i];
                if (!s.Consumed || s.RunnerId == 0)
                    continue;
                if (s.Attacker == null || s.Defender == null)
                    continue;
                if (s.Attacker.Id != attacker.Id || s.Defender.Id != defender.Id)
                    continue;
                return true;
            }
            return false;
        }

        /// <summary>招架成功：加失衡；有 <see cref="CombatTags.AiParryBreak"/> 才断轴。</summary>
        public static void ApplySuccess(CombatEntity defender, CombatEntity attacker, bool breakSkill)
        {
            if (attacker == null || attacker.IsDisposed)
                return;

            float ratio = attacker.DazeMeter != null
                ? attacker.DazeMeter.ParryDazeRatio
                : DefaultParryDazeRatio;
            attacker.DazeMeter?.AddDaze(ratio, DazeSource.Parry);

            float playerSeconds = ResolvePlayerParrySeconds();
            float enemyStun = playerSeconds + EnemyStunAfterPlayer;

            if (breakSkill)
            {
                FaceAttacker(attacker, defender);
                attacker.TryApplyParryStun(defender != null ? defender.Id : 0, enemyStun);
                if (defender != null && defender.isTruePlayer)
                    CombatEncounterDirector.Instance?.NotifyPlayerParry(enemyStun);
            }

            PlayPresentation(defender, attacker);
            Log($"clash playerAnim={playerSeconds:0.00} enemyStun={enemyStun:0.00} break={breakSkill}");
        }

        /// <summary>11005 轴最长动画轨秒数；缺轴回退设计目标时长。</summary>
        public static float ResolvePlayerParrySeconds()
        {
            SkillAllEventData data = ActSkillTimelineLoader.GetOrLoad(PlayerSkillId);
            if (data?.skillAllEventDatas == null || data.skillAllEventDatas.Count == 0)
                return DefaultPlayerParrySeconds;

            int maxFrame = 0;
            float speed = 1f;
            for (int i = 0; i < data.skillAllEventDatas.Count; i++)
            {
                SkillNewEventData sub = data.skillAllEventDatas[i];
                if (sub == null)
                    continue;
                if (sub.Speed > 0.01f)
                    speed = sub.Speed;
                var anims = sub.AnimEvents != null ? sub.AnimEvents.Events : null;
                if (anims == null)
                    continue;
                for (int e = 0; e < anims.Count; e++)
                {
                    var anim = anims[e];
                    if (anim?.Range == null)
                        continue;
                    if (anim.Range.End > maxFrame)
                        maxFrame = anim.Range.End;
                }
            }

            if (maxFrame <= 0)
                return DefaultPlayerParrySeconds;

            float seconds = maxFrame * XCSetting.FramePerSec / speed;
            if (seconds < 0.05f)
                return DefaultPlayerParrySeconds;
            if (seconds > 1f)
                Log($"player anim {seconds:0.00}s (target {DefaultPlayerParrySeconds:0.00}s) 请缩短 11005");
            return seconds;
        }

        static void Register(
            CombatEntity attacker,
            CombatEntity defender,
            long runnerId,
            bool breakSkill,
            float expireAtWorld)
        {
            int slot = -1;
            for (int i = 0; i < Capacity; i++)
            {
                if (_strikes[i].Attacker != null && _strikes[i].Attacker.Id == attacker.Id)
                {
                    if (_strikes[i].Consumed)
                    {
                        _strikes[i].RunnerId = runnerId;
                        return;
                    }

                    slot = i;
                    break;
                }
                if (slot < 0 && _strikes[i].Attacker == null && _strikes[i].RunnerId == 0)
                    slot = i;
            }
            if (slot < 0)
                slot = 0;

            _strikes[slot] = new IncomingStrike
            {
                Attacker = attacker,
                Defender = defender,
                RunnerId = runnerId,
                ExpireAtWorld = expireAtWorld,
                BreakSkill = breakSkill,
                Consumed = false,
                WindowOpen = true,
                PendingExpire = false,
            };
        }

        static bool TryFindLiveWindow(CombatEntity defender, out int index)
        {
            index = -1;
            float bestSq = MaxCommitRangeSq;
            for (int i = 0; i < Capacity; i++)
            {
                ref IncomingStrike s = ref _strikes[i];
                if (!s.WindowOpen || s.Consumed)
                    continue;
                if (s.Attacker == null || s.Attacker.IsDisposed || s.Attacker.IsDead)
                {
                    Free(i);
                    continue;
                }
                if (s.ExpireAtWorld > 0f && GameTimeManager.WorldTime > s.ExpireAtWorld)
                {
                    Free(i);
                    continue;
                }
                if (!IsThreatened(defender, in s))
                    continue;
                if (!InCommitRange(defender, s.Attacker, out float sq))
                    continue;
                if (sq > bestSq)
                    continue;
                bestSq = sq;
                index = i;
            }
            return index >= 0;
        }

        static bool IsThreatened(CombatEntity defender, in IncomingStrike s)
        {
            if (s.Defender != null && !s.Defender.IsDisposed && s.Defender.Id == defender.Id)
                return true;
            return defender.isTruePlayer;
        }

        static bool InCommitRange(CombatEntity defender, CombatEntity attacker, out float sqrDistance)
        {
            sqrDistance = PlanarDistanceSq(defender, attacker);
            return sqrDistance <= MaxCommitRangeSq;
        }

        static float PlanarDistance(CombatEntity a, CombatEntity b) => Mathf.Sqrt(PlanarDistanceSq(a, b));

        static float PlanarDistanceSq(CombatEntity a, CombatEntity b)
        {
            Vector3 d = b.Position - a.Position;
            d.y = 0f;
            return d.sqrMagnitude;
        }

        static void Log(string message)
        {
            GameLog.CombatError($"[Parry] {message}");
        }

        static void SnapToClash(CombatEntity defender, CombatEntity attacker)
        {
            Vector3 fwd = attacker.Rotation * Vector3.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.0001f)
            {
                fwd = defender.Position - attacker.Position;
                fwd.y = 0f;
            }
            if (fwd.sqrMagnitude < 0.0001f)
                fwd = Vector3.forward;
            fwd.Normalize();

            Vector3 pos = attacker.Position + fwd * SnapDistance;
            pos.y = defender.Position.y;

            Vector3 look = attacker.Position - pos;
            look.y = 0f;
            Quaternion rot = look.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(look, Vector3.up)
                : Quaternion.LookRotation(-fwd, Vector3.up);

            Warp(defender, pos, rot);
        }

        static void FaceAttacker(CombatEntity attacker, CombatEntity defender)
        {
            if (attacker == null || defender == null)
                return;

            Vector3 look = defender.Position - attacker.Position;
            look.y = 0f;
            if (look.sqrMagnitude < 0.0001f)
                return;

            Quaternion rot = Quaternion.LookRotation(look, Vector3.up);
            Transform root = attacker.RootTransform;
            if (root != null)
                root.rotation = rot;
            attacker.Rotation = rot;
        }

        static void Warp(CombatEntity unit, Vector3 pos, Quaternion rot)
        {
            Transform root = unit.RootTransform;
            if (root == null)
            {
                unit.Position = pos;
                unit.Rotation = rot;
                return;
            }

            CharacterController cc = root.GetComponent<CharacterController>();
            if (cc != null && cc.enabled)
            {
                cc.enabled = false;
                root.SetPositionAndRotation(pos, rot);
                cc.enabled = true;
            }
            else
            {
                root.SetPositionAndRotation(pos, rot);
            }

            Physics.SyncTransforms();
            unit.Position = pos;
            unit.Rotation = rot;
        }

        static void Free(int index)
        {
            _strikes[index] = default;
        }

        static void PlayPresentation(CombatEntity defender, CombatEntity attacker)
        {
#if UNITY
            var fx = CombatFxPlayContext.ForOwner(defender, CombatFxSource.Entity(defender != null ? defender.Id : 0));
            fx.ActionCreator = attacker;
            fx.ActionTarget = defender;
            CombatFxPackagePlayer.Play(CombatFxPackageId.ParrySuccess, in fx);
#endif
        }
    }
}
