using ACTGameEditor.Combat.Ai;
using EGamePlay;
using EGamePlay.Combat;
using UnityEngine;

namespace ACTGameEditor.Combat
{
    /// <summary>
    /// 红闪回避支援窗：预警亮起时开窗，候场 Z 换入后 i-frame，不成交招架、不断轴。
    /// </summary>
    public static class CombatEvasiveAssist
    {
        /// <summary>最远可换入距离（米），与黄闪一致。</summary>
        public const float MaxCommitRange = 12f;

        /// <summary>换入后与攻击者的水平侧向间距（米）。</summary>
        public const float SideDistance = 1.35f;

        const int Capacity = 8;
        const float MaxCommitRangeSq = MaxCommitRange * MaxCommitRange;
        const float DefaultWindowLife = 2.5f;

        struct IncomingStrike
        {
            public CombatEntity Attacker;
            public CombatEntity Defender;
            public float ExpireAtWorld;
            public bool Consumed;
            public bool WindowOpen;
        }

        static readonly IncomingStrike[] _strikes = new IncomingStrike[Capacity];

        /// <summary>
        /// 红闪预警亮起时开窗。只认 <see cref="TelegraphKind.Unblockable"/>，不把冷白 Dodge 当红闪。
        /// </summary>
        public static void ArmIncoming(
            CombatEntity attacker,
            CombatEntity threatened,
            int skillId,
            float telegraphSeconds,
            TelegraphKind telegraphKind)
        {
            if (attacker == null || attacker.IsDisposed)
                return;
            if (telegraphKind != TelegraphKind.Unblockable)
                return;

            CombatEntity defender = threatened;
            if (defender == null || defender.IsDisposed || defender.IsDead)
                defender = CombatEncounterDirector.Instance != null
                    ? CombatEncounterDirector.Instance.FocusTarget
                    : null;
            if (defender == null || defender.IsDisposed || defender.IsDead)
                return;

            float life = telegraphSeconds > 0.01f ? telegraphSeconds + 2f : DefaultWindowLife;
            Register(attacker, defender, GameTimeManager.WorldTime + life);
            GameLog.CombatDebug($"[Evasive] arm skill={skillId} attacker={attacker.Id} defender={defender.Id} life={life:0.00}");
        }

        /// <summary>场上是否有未消费的红闪来刀。</summary>
        public static bool HasIncomingStrike(CombatEntity defender)
        {
            return TryFindLiveWindow(defender, out _);
        }

        /// <summary>消费红窗并返回攻击者，供换入侧闪。不成交招架。</summary>
        public static bool TryConsume(CombatEntity defender, out CombatEntity attacker)
        {
            attacker = null;
            if (defender == null || defender.IsDisposed || defender.IsDead)
                return false;
            if (!TryFindLiveWindow(defender, out int index))
                return false;

            ref IncomingStrike strike = ref _strikes[index];
            attacker = strike.Attacker;
            strike.Consumed = true;
            strike.WindowOpen = false;
            GameLog.CombatDebug($"[Evasive] consume attacker={attacker.Id} defender={defender.Id}");
            return true;
        }

        /// <summary>换入者闪到攻击者身侧并朝向对方，不吸附撞刃。</summary>
        public static void SnapToSide(CombatEntity incoming, CombatEntity attacker)
        {
            if (incoming == null || incoming.IsDisposed || attacker == null || attacker.IsDisposed)
                return;

            Vector3 right = attacker.Rotation * Vector3.right;
            right.y = 0f;
            if (right.sqrMagnitude < 0.0001f)
                right = Vector3.right;
            else
                right.Normalize();

            Vector3 pos = attacker.Position + right * SideDistance;
            pos.y = incoming.Position.y;

            Vector3 look = attacker.Position - pos;
            look.y = 0f;
            Quaternion rot = look.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(look, Vector3.up)
                : Quaternion.LookRotation(-right, Vector3.up);

            Warp(incoming, pos, rot);
        }

        /// <summary>战局销毁时清空红窗。</summary>
        public static void ClearAll()
        {
            for (int i = 0; i < Capacity; i++)
                _strikes[i] = default;
        }

        static void Register(CombatEntity attacker, CombatEntity defender, float expireAtWorld)
        {
            int slot = -1;
            for (int i = 0; i < Capacity; i++)
            {
                if (_strikes[i].Attacker != null && _strikes[i].Attacker.Id == attacker.Id)
                {
                    slot = i;
                    break;
                }
                if (slot < 0 && _strikes[i].Attacker == null)
                    slot = i;
            }
            if (slot < 0)
                slot = 0;

            _strikes[slot] = new IncomingStrike
            {
                Attacker = attacker,
                Defender = defender,
                ExpireAtWorld = expireAtWorld,
                Consumed = false,
                WindowOpen = true,
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
                    _strikes[i] = default;
                    continue;
                }
                if (s.ExpireAtWorld > 0f && GameTimeManager.WorldTime > s.ExpireAtWorld)
                {
                    _strikes[i] = default;
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
            Vector3 d = attacker.Position - defender.Position;
            d.y = 0f;
            sqrDistance = d.sqrMagnitude;
            return sqrDistance <= MaxCommitRangeSq;
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
    }
}
