using EGamePlay;
using EGamePlay.Combat;
using UnityEngine;

namespace ACTGameEditor.Combat
{
    /// <summary>小队槽位在场状态。候场隐藏；退场中继续跑完当前轴。</summary>
    public enum SquadPresence : byte
    {
        OnField = 0,
        Exiting = 1,
        Bench = 2,
    }

    /// <summary>换人原因。Manual / Death 已落地；黄闪 / 红闪 / 快速支援按窗推断。Chain 已废弃，勿赋值。</summary>
    public enum SwitchReason : byte
    {
        Manual = 0,
        QuickAssist = 1,
        DefensiveAssist = 2,
        EvasiveAssist = 3,
        /// <summary>已废弃：原失衡连携窗。保留枚举值以免 Death 错位。</summary>
        Chain = 4,
        Death = 5,
    }

    /// <summary>
    /// 三人小队运行时：三槽、Presence、换人 Gate、支援窗。
    /// 只持有 <see cref="CombatEntity"/>；显隐 / Warp / 相机 / 输入在 <see cref="PlayerManager"/>。
    /// 切人无次数、无冷却（绝区零支援点已移除）。
    /// </summary>
    public sealed class CombatSquad : Entity
    {
        /// <summary>固定三人队。</summary>
        public const int SlotCount = 3;

        /// <summary>合轴进场相对旧人的水平偏移（米）。</summary>
        public const float ComboEnterOffset = 1.7f;

        const long SwitchIFrameSourceId = 90001;

        /// <summary>战局单例；由 <see cref="CombatContext"/> 子实体持有。</summary>
        public static CombatSquad Instance { get; private set; }

        readonly CombatEntity[] _members = new CombatEntity[SlotCount];
        int _activeSlot;

        float _quickExpireAtWorld;

        float _assistWindow = 1.2f;
        float _switchIFrame = 0.4f;

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
            _quickExpireAtWorld = 0f;
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
            ReloadSquadSettings();
            _quickExpireAtWorld = 0f;
            return true;
        }

        /// <summary>按槽位取成员；越界返回 null。</summary>
        public CombatEntity GetMember(int slot)
        {
            if ((uint)slot >= SlotCount)
                return null;
            return _members[slot];
        }

        /// <summary>相对当前槽偏移后换人。死亡补位等循环用；键盘走 <see cref="TrySwitchTo"/>。</summary>
        public bool TrySwitchRelative(int offset)
        {
            if (!IsBound)
                return false;
            int wrapped = offset % SlotCount;
            if (wrapped < 0)
                wrapped += SlotCount;
            int target = (_activeSlot + wrapped) % SlotCount;
            return TrySwitchTo(target);
        }

        /// <summary>按绝对槽换人。1/2/3 与肖像走这里。目标上一刀未打完则拦截。黄/红 Kit 空列则整次失败，不降级 Manual。无次数、无冷却。</summary>
        public bool TrySwitchTo(int slot)
        {
            if (!IsBound)
                return false;
            if ((uint)slot >= SlotCount || slot == _activeSlot)
                return false;

            CombatEntity incoming = _members[slot];
            CombatEntity outgoing = _members[_activeSlot];
            if (!TryResolveSwitchIntent(outgoing, incoming, out SwitchReason reason, out int skillId, out CombatEntity skillTarget))
                return false;
            if (!TrySwitch(slot, reason))
                return false;

            if (reason == SwitchReason.DefensiveAssist)
            {
                if (!CombatParry.TryCommit(incoming, out CombatEntity attacker))
                    return true;
                EnqueueIncomingSkill(incoming, reason, skillId, attacker, ignoreCooldown: false);
                return true;
            }

            if (reason == SwitchReason.EvasiveAssist)
            {
                if (!CombatEvasiveAssist.TryConsume(incoming, out CombatEntity attacker))
                    return true;
                CombatEvasiveAssist.SnapToSide(incoming, attacker);
                ApplySwitchIFrame(incoming);
                EnqueueIncomingSkill(incoming, reason, skillId, attacker, ignoreCooldown: false);
                return true;
            }

            if (reason == SwitchReason.QuickAssist)
            {
                _quickExpireAtWorld = 0f;
                EnqueueIncomingSkill(incoming, reason, skillId, skillTarget, ignoreCooldown: true);
                return true;
            }

            if (reason != SwitchReason.Manual && reason != SwitchReason.Death)
                EnqueueIncomingSkill(incoming, reason, skillId, skillTarget, ignoreCooldown: false);
            return true;
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
            if (HasUnfinishedExitSkill(incoming))
                return false;

            if (reason != SwitchReason.Death)
            {
                if (FindExitingSlot() >= 0)
                    return false;
                if (reason == SwitchReason.Manual && IsSwitchBlockedBySkill(outgoing))
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

        /// <summary>肖像是否可点：存活候场、目标上一刀已打完；占轴时仅黄/红/快速支援窗可点。</summary>
        public bool CanSwitchTo(int slot)
        {
            if (!IsBound)
                return false;
            if ((uint)slot >= SlotCount || slot == _activeSlot)
                return false;

            CombatEntity incoming = _members[slot];
            if (incoming == null || incoming.IsDisposed || incoming.IsDead)
                return false;
            if (HasUnfinishedExitSkill(incoming))
                return false;
            if (FindExitingSlot() >= 0)
                return false;

            CombatEntity outgoing = _members[_activeSlot];
            if (outgoing != null && !outgoing.IsDisposed && IsSwitchBlockedBySkill(outgoing)
                && !CombatParry.HasIncomingStrike(outgoing)
                && !CombatEvasiveAssist.HasIncomingStrike(outgoing)
                && !HasQuickAssistWindow())
                return false;
            return true;
        }

        /// <summary>场上玩家真实挨打后开快速支援窗（世界钟）。</summary>
        public void NotifyPlayerHit()
        {
            if (!IsBound)
                return;
            OpenQuickAssistWindow(0f);
        }

        /// <summary>轴 Msg AssistCue：FloatMsg 为窗长秒，0=表默认。</summary>
        public void NotifyAssistCue(float durationSeconds)
        {
            if (!IsBound)
                return;
            OpenQuickAssistWindow(durationSeconds);
        }

        /// <summary>快速支援窗是否仍开。</summary>
        public bool HasQuickAssistWindow()
        {
            if (_quickExpireAtWorld <= 0f)
                return false;
            if (GameTimeManager.WorldTime > _quickExpireAtWorld)
            {
                _quickExpireAtWorld = 0f;
                return false;
            }
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

        void OpenQuickAssistWindow(float durationSeconds)
        {
            ReloadSquadSettings();
            float life = durationSeconds > 0.01f ? durationSeconds : _assistWindow;
            if (life <= 0f)
                return;
            float expire = GameTimeManager.WorldTime + life;
            if (expire > _quickExpireAtWorld)
                _quickExpireAtWorld = expire;
        }

        void ReloadSquadSettings()
        {
            SquadSetting row = SkillSettingMgr.Instance != null
                ? SkillSettingMgr.Instance.GetSquadSettingOrNull()
                : null;
            if (row == null)
                return;

            _assistWindow = row.AssistWindow;
            _switchIFrame = row.SwitchIFrame;
        }

        void ApplySwitchIFrame(CombatEntity incoming)
        {
            if (incoming == null || incoming.IsDisposed)
                return;
            if (_switchIFrame <= 0f)
                return;
            incoming.GrantTagFor(TagSource.Manual(SwitchIFrameSourceId), CombatTags.CombatSwitchIFrame, _switchIFrame);
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
                if (HasUnfinishedExitSkill(member))
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

        /// <summary>目标还在打切走前那一刀：合轴 Exiting，或轴还没结束（误进 Bench 仍占轴）。</summary>
        static bool HasUnfinishedExitSkill(CombatEntity unit)
        {
            if (unit.SquadPresence == SquadPresence.Exiting)
                return true;
            return HasOccupyingSkill(unit);
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
            if (ability == null)
                return false;
            SkillCategory cat = SkillSettingMgr.Instance != null
                ? SkillSettingMgr.Instance.GetSkillCategory(ability.SkillID)
                : SkillCategory.None;
            return cat == SkillCategory.QuickAssist
                || cat == SkillCategory.EvasiveAssist
                || cat == SkillCategory.AssistFollowUp
                || cat == SkillCategory.HarmonyBreak
                || cat == SkillCategory.Chain;
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

        /// <summary>
        /// 黄闪 → 防御支援；红闪 → 回避支援；支援窗 → 快速支援；否则 Manual。
        /// 窗口要求的 Kit 列为 0 时失败（不降级 Manual）。切人无次数闸。
        /// </summary>
        bool TryResolveSwitchIntent(
            CombatEntity outgoing,
            CombatEntity incoming,
            out SwitchReason reason,
            out int skillId,
            out CombatEntity skillTarget)
        {
            reason = SwitchReason.Manual;
            skillId = 0;
            skillTarget = null;
            if (outgoing == null || outgoing.IsDisposed || incoming == null || incoming.IsDisposed)
                return false;

            ReloadSquadSettings();
            CharacterKitSetting kit = SkillSettingMgr.Instance != null
                ? SkillSettingMgr.Instance.GetCharacterKitOrNull(incoming.CharacterId)
                : null;

            if (CombatParry.HasIncomingStrike(outgoing))
            {
                int id = kit != null ? kit.DefensiveAssistSkillId : 0;
                if (id <= 0)
                    return false;
                reason = SwitchReason.DefensiveAssist;
                skillId = id;
                return true;
            }

            if (CombatEvasiveAssist.HasIncomingStrike(outgoing))
            {
                int id = kit != null ? kit.EvasiveAssistSkillId : 0;
                if (id <= 0)
                    return false;
                reason = SwitchReason.EvasiveAssist;
                skillId = id;
                return true;
            }

            if (HasQuickAssistWindow())
            {
                int id = kit != null ? kit.QuickAssistSkillId : 0;
                if (id <= 0)
                    return false;
                reason = SwitchReason.QuickAssist;
                skillId = id;
                return true;
            }

            return true;
        }

        static void EnqueueIncomingSkill(
            CombatEntity incoming,
            SwitchReason reason,
            int skillId,
            CombatEntity skillTarget,
            bool ignoreCooldown)
        {
            if (incoming == null || incoming.IsDisposed || skillId <= 0)
                return;

            ActSpellComponent spell = incoming.GetComponent<ActSpellComponent>();
            if (spell == null)
                return;

            SkillSpellInfo info = PoolManager.Instance.TryGet<SkillSpellInfo>();
            info.SkillId = skillId;
            info.Sort = ResolveIncomingSort(reason);
            info.Target = skillTarget;
            info.Point = skillTarget != null
                ? skillTarget.Position
                : incoming.Position + incoming.Rotation * Vector3.forward * 3f;
            info.IgnoreCooldown = ignoreCooldown;
            spell.Enqueue(info);
        }

        static int ResolveIncomingSort(SwitchReason reason)
        {
            if (reason == SwitchReason.DefensiveAssist)
                return (int)SkillSort.Parry;
            if (reason == SwitchReason.EvasiveAssist)
                return (int)SkillSort.Speical;
            return (int)SkillSort.Speical;
        }
    }
}
