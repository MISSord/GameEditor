using EGamePlay;
using EGamePlay.Combat;
using UnityEngine;
using ACTGameEditor;

namespace ACTGameEditor.Combat
{
    /// <summary>小队槽位在场状态。候场隐藏；退场中继续跑完当前轴。</summary>
    public enum SquadPresence : byte
    {
        OnField = 0,
        Exiting = 1,
        Bench = 2,
    }

    /// <summary>换人原因。M4.0 只跑 Manual 与 Death。</summary>
    public enum SwitchReason : byte
    {
        Manual = 0,
        QuickAssist = 1,
        DefensiveAssist = 2,
        EvasiveAssist = 3,
        Chain = 4,
        Death = 5,
    }

    /// <summary>
    /// 三人小队运行时：三槽、Presence、换人 Gate。
    /// 只持有 <see cref="CombatEntity"/>；显隐 / Warp / 相机 / 输入在 <see cref="PlayerManager"/>。
    /// </summary>
    public sealed class CombatSquad : Entity
    {
        /// <summary>固定三人队。</summary>
        public const int SlotCount = 3;

        /// <summary>合轴进场相对旧人的水平偏移（米）。</summary>
        public const float ComboEnterOffset = 1.7f;

        /// <summary>战局单例；由 <see cref="CombatContext"/> 子实体持有。</summary>
        public static CombatSquad Instance { get; private set; }

        readonly CombatEntity[] _members = new CombatEntity[SlotCount];
        int _activeSlot;

        /// <summary>三槽已绑定。</summary>
        public bool IsBound => _members[0] != null && !_members[0].IsDisposed;

        /// <summary>当前上场槽。</summary>
        public int ActiveSlot => _activeSlot;

        /// <summary>当前上场成员。</summary>
        public CombatEntity ActiveMember => IsBound ? _members[_activeSlot] : null;

        /// <inheritdoc />
        public override void Awake()
        {
            Instance = this;
        }

        /// <inheritdoc />
        public override void OnDestroy()
        {
            for (int i = 0; i < SlotCount; i++)
                _members[i] = null;
            _activeSlot = 0;
            if (Instance == this)
                Instance = null;
        }

        /// <summary>开战绑定三槽。槽 0 上场，1/2 候场。</summary>
        public bool Bind(CombatEntity slot0, CombatEntity slot1, CombatEntity slot2)
        {
            if (slot0 == null || slot1 == null || slot2 == null)
                return false;
            if (!slot0.IsPlayerSquad || !slot1.IsPlayerSquad || !slot2.IsPlayerSquad)
                return false;

            _members[0] = slot0;
            _members[1] = slot1;
            _members[2] = slot2;
            _activeSlot = 0;
            return true;
        }

        /// <summary>按槽位取成员；越界返回 null。</summary>
        public CombatEntity GetMember(int slot)
        {
            if ((uint)slot >= SlotCount)
                return null;
            return _members[slot];
        }

        /// <summary>Q/E：相对当前槽 +1 / +2。</summary>
        public bool TrySwitchRelative(int offset)
        {
            if (!IsBound)
                return false;
            int wrapped = offset % SlotCount;
            if (wrapped < 0)
                wrapped += SlotCount;
            int target = (_activeSlot + wrapped) % SlotCount;
            return TrySwitch(target, SwitchReason.Manual);
        }

        /// <summary>肖像 / 死亡补位入口。</summary>
        public bool TrySwitch(int slot, SwitchReason reason)
        {
            if (!IsBound)
                return false;
            if ((uint)slot >= SlotCount || slot == _activeSlot)
                return false;

            CombatEntity incoming = _members[slot];
            CombatEntity outgoing = _members[_activeSlot];
            if (incoming == null || incoming.IsDisposed || incoming.IsDead)
                return false;
            if (outgoing == null || outgoing.IsDisposed)
                return false;
            if (incoming.SquadPresence == SquadPresence.Exiting)
                return false;

            if (reason != SwitchReason.Death)
            {
                if (FindExitingSlot() >= 0)
                    return false;
                if (IsSwitchBlockedBySkill(outgoing))
                    return false;
            }

            bool combo = reason == SwitchReason.Manual && ShouldComboExit(outgoing);
            Vector3 enterPos;
            Quaternion enterRot = outgoing.Rotation;
            if (combo)
                enterPos = ResolveComboEnterPosition(outgoing);
            else
                enterPos = outgoing.Position;

            outgoing.isTruePlayer = false;
            incoming.isTruePlayer = true;
            _activeSlot = slot;

            PlayerManager.Instance.ApplySquadSwitch(outgoing, incoming, combo, enterPos, enterRot);
            GameLog.CombatDebug($"[Squad] switch {reason} {outgoing.SquadSlot}->{incoming.SquadSlot} combo={combo}");
            return true;
        }

        /// <summary>肖像是否可点：存活候场、无人 Exiting、当前轴未禁切。</summary>
        public bool CanSwitchTo(int slot)
        {
            if (!IsBound)
                return false;
            if ((uint)slot >= SlotCount || slot == _activeSlot)
                return false;

            CombatEntity incoming = _members[slot];
            if (incoming == null || incoming.IsDisposed || incoming.IsDead)
                return false;
            if (incoming.SquadPresence == SquadPresence.Exiting)
                return false;
            if (FindExitingSlot() >= 0)
                return false;

            CombatEntity outgoing = _members[_activeSlot];
            if (outgoing != null && !outgoing.IsDisposed && IsSwitchBlockedBySkill(outgoing))
                return false;
            return true;
        }

        /// <summary>当前轴结束或被打断后，把 Exiting 立刻候场。</summary>
        public void NotifyExitComplete(CombatEntity member)
        {
            if (member == null || member.IsDisposed || !member.IsPlayerSquad)
                return;
            if (member.SquadPresence != SquadPresence.Exiting)
                return;
            PlayerManager.Instance.ApplySquadPresence(member, SquadPresence.Bench);
        }

        /// <summary>上场阵亡且有活着的候场时补位；Exiting 阵亡只隐藏该槽。</summary>
        public void NotifyMemberDeath(CombatEntity member)
        {
            if (member == null || !member.IsPlayerSquad)
                return;

            if (member.SquadPresence == SquadPresence.Exiting)
            {
                PlayerManager.Instance.ApplySquadPresence(member, SquadPresence.Bench);
                return;
            }

            if (member != ActiveMember)
                return;

            int next = FindNextLivingSlot(_activeSlot);
            if (next < 0)
                return;
            TrySwitch(next, SwitchReason.Death);
        }

        int FindExitingSlot()
        {
            for (int i = 0; i < SlotCount; i++)
            {
                CombatEntity member = _members[i];
                if (member != null && !member.IsDisposed && member.SquadPresence == SquadPresence.Exiting)
                    return i;
            }

            return -1;
        }

        int FindNextLivingSlot(int fromSlot)
        {
            for (int i = 1; i < SlotCount; i++)
            {
                int slot = (fromSlot + i) % SlotCount;
                CombatEntity member = _members[slot];
                if (member == null || member.IsDisposed || member.IsDead)
                    continue;
                if (member.SquadPresence == SquadPresence.Exiting)
                    continue;
                return slot;
            }

            return -1;
        }

        static bool ShouldComboExit(CombatEntity outgoing)
        {
            if (outgoing.IsDead)
                return false;
            PlayerStateEnum state = outgoing.CurState;
            if (state == PlayerStateEnum.Hit || state == PlayerStateEnum.Control
                || state == PlayerStateEnum.Stagger || state == PlayerStateEnum.Dead)
                return false;
            return HasOccupyingSkill(outgoing);
        }

        static bool HasOccupyingSkill(CombatEntity unit)
        {
            ISkillExecutionHandle exec = unit.ActiveExecution;
            return exec != null && !exec.IsDisposed;
        }

        static bool IsSwitchBlockedBySkill(CombatEntity unit)
        {
            if (!HasOccupyingSkill(unit))
                return false;
            int sort = unit.ActiveExecution.Sort;
            if (SkillSortUtil.IsParry(sort))
                return true;
            if (sort >= (int)SkillSort.Ultimate)
                return true;
            ActSkillRunner runner = unit.SpellingExecution;
            Ability ability = runner != null ? runner.AbilityEntity : null;
            return ability != null && CombatChainSkill.IsChainSkill(ability.SkillID);
        }

        static Vector3 ResolveComboEnterPosition(CombatEntity outgoing)
        {
            Vector3 origin = outgoing.Position;
            Transform root = outgoing.RootTransform;
            Vector3 right = root != null ? root.right : Vector3.right;
            Vector3 back = root != null ? -root.forward : Vector3.back;
            right.y = 0f;
            back.y = 0f;
            Vector3 dir = right + back;
            if (dir.sqrMagnitude < 0.0001f)
                dir = Vector3.right;
            else
                dir.Normalize();
            return origin + dir * ComboEnterOffset;
        }
    }
}
