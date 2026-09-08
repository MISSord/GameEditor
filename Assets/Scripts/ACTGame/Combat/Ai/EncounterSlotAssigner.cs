using EGamePlay;
using UnityEngine;

namespace ACTGameEditor.Combat.Ai
{
    /// <summary>
    /// 近战 4 槽：前 / 左翼 / 右翼 / 后。0.25s 重分配，已占槽且仍合法则保留。
    /// 溢出的人站在更大半径上，不进 4 槽。
    /// </summary>
    public sealed class EncounterSlotAssigner
    {
        /// <summary>近战槽数量。</summary>
        public const int MeleeSlotCount = 4;

        /// <summary>重分配间隔（世界钟）。</summary>
        public const float AssignInterval = 0.25f;

        /// <summary>槽半径回退值，与杂兵 KeepDistance 初值对齐。分配时用导演传入的场上均值。</summary>
        public const float DefaultKeepDistance = 3.6f;

        /// <summary>未进 4 槽时的半径倍率。</summary>
        public const float OverflowKeepScale = 1.45f;

        /// <summary>槽位徘徊幅度（弧度，±约 17°）。雷火文「战斗圈左右徘徊」：无状态正弦摆动。</summary>
        const float SlotWanderAmplitude = 0.30f;

        /// <summary>溢出环徘徊幅度（更大半径上摆幅相当）。</summary>
        const float OverflowWanderAmplitude = 0.40f;

        readonly long[] _slotIds = new long[MeleeSlotCount];

        /// <summary>清空全部槽。</summary>
        public void Clear()
        {
            for (int i = 0; i < MeleeSlotCount; i++)
                _slotIds[i] = 0;
        }

        /// <summary>注销时挪走该 Id。</summary>
        public void Remove(long entityId)
        {
            if (entityId == 0)
                return;
            for (int i = 0; i < MeleeSlotCount; i++)
            {
                if (_slotIds[i] == entityId)
                    _slotIds[i] = 0;
            }
        }

        /// <summary>把 inSlotId 所在槽换给 outId（导演的欲望驱动内外圈轮换用）。</summary>
        public void Swap(long inSlotId, long outId)
        {
            if (inSlotId == 0 || outId == 0)
                return;
            for (int i = 0; i < MeleeSlotCount; i++)
            {
                if (_slotIds[i] != inSlotId)
                    continue;
                _slotIds[i] = outId;
                return;
            }
        }

        /// <summary>0～3 为近战槽，未分配返回 -1。</summary>
        public int IndexOf(long entityId)
        {
            if (entityId == 0)
                return -1;
            for (int i = 0; i < MeleeSlotCount; i++)
            {
                if (_slotIds[i] == entityId)
                    return i;
            }

            return -1;
        }

        /// <summary>重分配。Melee Token 持有者优先占前槽。半径用场上活人 KeepDistance 均值。</summary>
        public void Reassign(CombatEntity focus, CombatEntity meleeHolder, CombatEntity[] living, int livingCount, float keepDistance)
        {
            if (keepDistance < 0.5f)
                keepDistance = DefaultKeepDistance;

            for (int s = 0; s < MeleeSlotCount; s++)
            {
                long id = _slotIds[s];
                if (id == 0)
                    continue;
                if (!Contains(living, livingCount, id))
                    _slotIds[s] = 0;
            }

            if (meleeHolder != null && !meleeHolder.IsDisposed && !meleeHolder.IsDead)
                PreferFront(meleeHolder.Id);

            if (focus == null || focus.IsDisposed)
                return;

            for (int s = 0; s < MeleeSlotCount; s++)
            {
                if (_slotIds[s] != 0)
                    continue;
                CombatEntity best = PickClosestUnassigned(focus, s, living, livingCount, keepDistance);
                if (best != null)
                    _slotIds[s] = best.Id;
            }
        }

        /// <summary>槽或溢出环上的世界锚点。slotIndex&lt;0 走溢出。</summary>
        public static Vector3 WorldAnchor(CombatEntity focus, int slotIndex, float keepDistance, long enemyId)
        {
            GetFocusBasis(focus, out Vector3 fwd, out Vector3 right);
            Vector3 origin = focus.Position;
            float radius = keepDistance;
            Vector3 dir;
            if (slotIndex >= 0 && slotIndex < MeleeSlotCount)
            {
                dir = slotIndex switch
                {
                    0 => fwd,
                    1 => -right,
                    2 => right,
                    _ => -fwd,
                };
            }
            else
            {
                radius *= OverflowKeepScale;
                dir = OverflowDir(enemyId, fwd, right);
            }

            // 徘徊：锚点绕基线角正弦慢摆，对峙中的怪左右游走而不是站桩
            float now = GameTimeManager.WorldTime;
            float wander = slotIndex >= 0 && slotIndex < MeleeSlotCount
                ? SlotWanderAngle(slotIndex, now)
                : OverflowWanderAngle(enemyId, now);
            if (Mathf.Abs(wander) > 0.0001f)
                dir = Quaternion.AngleAxis(wander * Mathf.Rad2Deg, Vector3.up) * dir;

            origin.x += dir.x * radius;
            origin.z += dir.z * radius;
            return origin;
        }

        /// <summary>槽位徘徊角：不同槽不同速率/相位，看起来像各自随机游走。</summary>
        static float SlotWanderAngle(int slotIndex, float now)
        {
            return Mathf.Sin(now * (0.45f + 0.13f * slotIndex) + slotIndex * 1.7f) * SlotWanderAmplitude;
        }

        /// <summary>溢出环徘徊角：按 Id 哈希速率/相位。</summary>
        static float OverflowWanderAngle(long id, float now)
        {
            unchecked
            {
                uint h = (uint)id * 2654435761u;
                float t = (h & 1023u) * (1f / 1023f);
                return Mathf.Sin(now * (0.30f + 0.25f * t) + t * 6.2832f) * OverflowWanderAmplitude;
            }
        }

        /// <summary>焦点平面前/右。无朝向时用世界 +Z。</summary>
        public static void GetFocusBasis(CombatEntity focus, out Vector3 forward, out Vector3 right)
        {
            forward = Vector3.forward;
            right = Vector3.right;
            if (focus == null)
                return;

            Vector3 f = focus.Rotation * Vector3.forward;
            f.y = 0f;
            if (f.sqrMagnitude < 0.0001f)
                f = Vector3.forward;
            else
                f.Normalize();
            forward = f;
            right = new Vector3(f.z, 0f, -f.x);
        }

        void PreferFront(long holderId)
        {
            if (_slotIds[0] == holderId)
                return;

            int old = IndexOf(holderId);
            long occupant = _slotIds[0];
            _slotIds[0] = holderId;
            if (old >= 0)
            {
                _slotIds[old] = occupant;
                return;
            }

            if (occupant == 0)
                return;
            for (int s = 1; s < MeleeSlotCount; s++)
            {
                if (_slotIds[s] != 0)
                    continue;
                _slotIds[s] = occupant;
                return;
            }
        }

        CombatEntity PickClosestUnassigned(CombatEntity focus, int slot, CombatEntity[] living, int livingCount, float keepDistance)
        {
            Vector3 anchor = WorldAnchor(focus, slot, keepDistance, 0);
            float bestSq = float.MaxValue;
            CombatEntity best = null;
            for (int i = 0; i < livingCount; i++)
            {
                CombatEntity enemy = living[i];
                if (enemy == null || IndexOf(enemy.Id) >= 0)
                    continue;
                Vector3 d = enemy.Position - anchor;
                d.y = 0f;
                float sq = d.sqrMagnitude;
                if (sq >= bestSq)
                    continue;
                bestSq = sq;
                best = enemy;
            }

            return best;
        }

        static bool Contains(CombatEntity[] living, int livingCount, long id)
        {
            for (int i = 0; i < livingCount; i++)
            {
                CombatEntity e = living[i];
                if (e != null && e.Id == id)
                    return true;
            }

            return false;
        }

        static Vector3 OverflowDir(long id, Vector3 forward, Vector3 right)
        {
            unchecked
            {
                uint h = (uint)id * 747796405u;
                float t = (h & 1023u) * (1f / 1023f);
                float rad = (0.25f + t) * (Mathf.PI * 2f);
                Vector3 dir = forward * Mathf.Cos(rad) + right * Mathf.Sin(rad);
                dir.y = 0f;
                float magSq = dir.sqrMagnitude;
                if (magSq < 0.0001f)
                    return forward;
                return dir * (1f / Mathf.Sqrt(magSq));
            }
        }
    }
}
