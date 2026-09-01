using ACTGameEditor.Combat;
using ACTGameEditor.Locomotion;
using EGamePlay;
using EGamePlay.Combat;
using EGamePlay.Unity;
using System.Collections.Generic;
using UnityEngine;

namespace ACTGameEditor
{
    public class NormalActPlayer : ActPlayer, IAttackPlayer
    {
        [Tooltip("槽位定义：输入绑定、默认技能、释放条件")]
        public SkillSlotConfig SlotConfig;
        [Tooltip("角色/武器覆盖：槽位→技能，换角色时加载")]
        public CharacterSlotConfig CharacterSlotConfig;
        [Tooltip("形态入口表（明心境、变身等）。空则 Idle 只用槽位表")]
        public List<SkillFormConfig> Forms = new List<SkillFormConfig>();

        [Header("被动技能（Buff实现）")]
        [Tooltip("额外被动技能ID（硬编码/测试用；会按 PassiveSkillBuffMaps 映射为 Buff 常驻挂载）")]
        public List<int> ExtraPassiveSkillIds = new List<int>();

        [Header("输入缓冲")]
        [Tooltip("Idle / 槽位预输入有效时长（秒），槽位未单独配置时使用")]
        [Range(0.05f, 2f)]
        public float InputTimeout = 0.2f;
        [Tooltip("连招窗边预输入寿命（秒）。边上 InputTimeout≤0 时使用。对齐崩3/绝区零：只认窗附近的按键，不认开打瞬间那一下")]
        [Range(0.05f, 1f)]
        public float ComboBufferTimeout = 0.15f;

        /// <summary> 槽位运行时：SlotId → 当前 SkillId </summary>
        public SkillSlotRuntime SlotRuntime { get; private set; }
        /// <summary> 技能冷却管理器，供 UI 查询剩余/总时长、填充等。 </summary>
        public SkillCDTimer CDTimer { get; private set; }
        /// <summary>分槽位预输入。</summary>
        public InputBuffer InputBuffer { get; private set; }
        // 目标缓存，默认使用 LockSystem 的锁定目标
        CombatEntity Target;

        // 缓存动画组件，避免每帧 GetComponent
        private AnimComponent _animComponent;

        protected override void StartCallBack()
        {
            Combat.ListenActionPoint(ActionPointType.PreSpell, OnPreSpell);
            Combat.ListenActionPoint(ActionPointType.PostSpell, OnPostSpell);
            Combat.ListenActionPoint(ActionPointType.PostReceiveDamage, OnReceiveDamage);
            Combat.ListenActionPoint(ActionPointType.PostCauseDamage, OnCauseDamage);
            Combat.ListenActionPoint(ActionPointType.PostReceiveCure, OnReceiveCure);
            Combat.ListenActionPoint(ActionPointType.PostReceiveStatus, OnReceiveStatus);

            CDTimer = Combat.GetComponent<ActSpellComponent>()?.CDTimer;
            SlotRuntime = new SkillSlotRuntime();
            InputBuffer = new InputBuffer();

            _animComponent = Combat?.GetComponent<AnimComponent>();

            CombatFormComponent formComp = Combat.FormComponent;
            formComp?.Init(Forms);

            if (SlotConfig != null)
            {
                SlotRuntime.Load(SlotConfig, CharacterSlotConfig);
                CDTimer?.InitFromSlotConfig(SlotConfig);
                EnsureSlotAbilitiesAttached();
                SyncPassiveSkillBuffs();
            }
        }

        protected override void DisposeCallBack()
        {
            Combat.UnListenActionPoint(ActionPointType.PreSpell, OnPreSpell);
            Combat.UnListenActionPoint(ActionPointType.PostSpell, OnPostSpell);
            Combat.UnListenActionPoint(ActionPointType.PostReceiveDamage, OnReceiveDamage);
            Combat.UnListenActionPoint(ActionPointType.PostCauseDamage, OnCauseDamage);
            Combat.UnListenActionPoint(ActionPointType.PostReceiveCure, OnReceiveCure);
            Combat.UnListenActionPoint(ActionPointType.PostReceiveStatus, OnReceiveStatus);

            CDTimer = null;
            InputBuffer?.Clear();
            InputBuffer = null;
        }

        protected virtual void OnPreSpell(Entity combatAction)
        {
            if (combatAction is not ICombatSpellActionContext spellCtx || Combat.ModelTrans == null)
                return;

            if (SkillSortUtil.IsRoll(spellCtx.Sort))
            {
                ApplyDodgeFacing(spellCtx);
                Combat.ChangeInputRotateState(false);
                return;
            }

            Vector3 lookDelta = spellCtx.InputTarget != null
                ? spellCtx.InputTarget.Position - Combat.ModelTrans.position
                : spellCtx.InputPoint - Combat.ModelTrans.position;
            if (TryMakePlanarLook(lookDelta, out Quaternion rot))
                transform.rotation = rot;
        }

        /// <summary>闪避起步：当前摇杆优先，其次技能中最后一次指向，否则沿技能点/身前。</summary>
        void ApplyDodgeFacing(ICombatSpellActionContext spellCtx)
        {
            if (CameraRelativeMove.TryGetWorldDirOrLast(out Vector3 dir))
            {
                ApplyDodgeYaw(dir);
                return;
            }

            Vector3 toPoint = spellCtx.InputPoint - Combat.Position;
            toPoint.y = 0f;
            if (toPoint.sqrMagnitude >= 0.0001f)
                ApplyDodgeYaw(toPoint.normalized);
        }

        void ApplyDodgeYaw(Vector3 planarDir)
        {
            planarDir.y = 0f;
            if (planarDir.sqrMagnitude < 0.0001f)
                return;
            planarDir.Normalize();

            Quaternion yaw = Quaternion.LookRotation(planarDir, Vector3.up);
            Transform root = Combat.RootTransform != null ? Combat.RootTransform : transform;
            root.rotation = yaw;
            Combat.Rotation = yaw;
        }

        static bool TryMakePlanarLook(Vector3 worldDelta, out Quaternion rotation)
        {
            worldDelta.y = 0f;
            if (worldDelta.sqrMagnitude < 0.0001f)
            {
                rotation = default;
                return false;
            }

            rotation = Quaternion.LookRotation(worldDelta.normalized, Vector3.up);
            return true;
        }

        protected virtual void OnPostSpell(Entity combatAction)
        {
            if (combatAction is ICombatSpellActionContext spellCtx && SkillSortUtil.IsRoll(spellCtx.Sort))
                Combat.ChangeInputRotateState(true);
        }

        protected virtual void OnCauseDamage(Entity combatAction)
        {
            if (!Combat.isTruePlayer)
                return;

            var damageAction = combatAction as DamageAction;
            if (damageAction == null || damageAction.Target == null)
                return;
            if (damageAction.Creator?.Id != Combat.Id)
                return;

            var kind = damageAction.DamageSource == DamageSource.Buff
                ? DamageTextKind.Buff
                : DamageTextKind.Skill;
            long targetId = damageAction.Target.Id;
            Vector3 worldPos = ResolveDamageTextWorldPosition(damageAction, damageAction.Target);
            DamageTextPresenter.Active?.ShowDamage(new DamageTextRequest(
                damageAction.DamageValue,
                worldPos,
                kind,
                targetId,
                damageAction.IsCritical,
                damageAction.AppliedDamageType,
                incoming: false));
        }

        protected virtual void OnReceiveDamage(Entity combatAction)
        {
            if (!Combat.isTruePlayer)
                return;

            var damageAction = combatAction as DamageAction;
            if (damageAction == null || Combat == null)
                return;

            // 本地玩家作为攻击者时，飘字只走 OnCauseDamage
            if (damageAction.Creator?.Id == Combat.Id)
                return;

            var kind = damageAction.DamageSource == DamageSource.Buff
                ? DamageTextKind.Buff
                : DamageTextKind.Skill;
            DamageTextPresenter.Active?.ShowDamage(new DamageTextRequest(
                damageAction.DamageValue,
                ResolveDamageTextWorldPosition(damageAction, Combat),
                kind,
                Combat.Id,
                damageAction.IsCritical,
                damageAction.AppliedDamageType,
                incoming: true));

            // 致死伤已在 ApplyDeath 中处理；霸体不进 Hit
            if (Combat.IsDead)
                return;

            long hitSrc = damageAction.Id;
            Combat.TryApplyHitReaction(hitSrc, 0.35f);
        }

        static Vector3 ResolveDamageTextWorldPosition(DamageAction damageAction, ICombatUnit fallbackTarget)
        {
            if (damageAction != null && damageAction.HasHitWorldPosition)
                return damageAction.HitWorldPosition;
            return ResolveDamageTextAnchor(fallbackTarget);
        }

        static Vector3 ResolveDamageTextAnchor(ICombatUnit target)
        {
            if (target?.Entity is CombatEntity combatEntity && combatEntity.AttackPlayer != null)
                return combatEntity.AttackPlayer.GetDamageTextAnchor();

            return target != null ? target.Position + Vector3.up * 1.05f : Vector3.zero;
        }

        protected virtual void OnReceiveCure(Entity combatAction)
        {

        }

        protected virtual void OnReceiveStatus(Entity combatAction)
        {

        }

        protected virtual void OnRemoveStatus(RemoveStatusEvent eventData)
        {

        }

        /// <summary>Idle 时按形态×空中解析槽位技能。</summary>
        public int ResolveIdleSkillId(SkillSlotId slotId)
        {
            return SkillResolver.ResolveIdle(Combat, SlotRuntime, slotId);
        }

        /// <summary>进入/退出战斗形态。formId≤0 回到默认槽位表。</summary>
        public void SetCombatForm(int formId)
        {
            CombatFormComponent form = Combat?.FormComponent;
            if (form == null)
                return;
            if (formId <= 0)
                form.ClearForm();
            else
                form.SetForm(formId);
            EnsureSlotAbilitiesAttached();
        }

        /// <summary>战斗 Tick 内提交 Idle/硬打断槽位，并推进受击动画自动交回。</summary>
        public void TickSkillInput()
        {
            CheckInitialInput();
            _animComponent?.Director?.Tick();
        }

        /// <summary>Idle 用槽位表；技能中仅更高 Sort 槽位可立刻打断。</summary>
        private void CheckInitialInput()
        {
            if (SlotConfig == null || SlotRuntime == null || InputBuffer == null) return;

            InputBuffer.Tick(PlayerTime);
            if (!InputBuffer.HasAny()) return;

            // ParentFinish 后 IsMainFinish：角色已可 Idle 出招，但 Runner 可能仍在播后摇
            bool occupying = Combat.SpellingExecution != null && !Combat.SpellingExecution.IsMainFinish;
            if (occupying)
            {
                if (!Combat.IsCanSelfCancelSkill) return;
            }
            else if (!Combat.IsCanSpellSkill)
            {
                return;
            }

            TryCommitFromSlots(interruptOnly: occupying);
        }

        /// <summary>
        /// 从槽位提交一发。Gate 通过后才消费预输入。
        /// interruptOnly 时只允许 Sort 更高的硬打断（闪避/大招）。
        /// </summary>
        void TryCommitFromSlots(bool interruptOnly)
        {
            int currentSort = interruptOnly ? Combat.SpellingExecution.Sort : int.MinValue;
            for (int i = 0; i < SlotConfig.Slots.Count; i++)
            {
                SkillSlotConfig.SlotEntry entry = SlotConfig.Slots[i];
                if (entry == null) continue;
                if (interruptOnly && !SkillCancelService.IsHardInterrupt(currentSort, entry.Sort))
                    continue;
                if (!InputBuffer.HasSlot(entry.SlotId)) continue;
                if (!InputBuffer.MatchesSlot(entry.SlotId, entry.InputType, entry.PressType, entry.InputCallBackType))
                    continue;

                int skillId = SkillResolver.ResolveIdle(Combat, SlotRuntime, entry.SlotId);
                if (skillId <= 0) continue;

                ActivateFail fail = AbilityActivationGate.Evaluate(Combat, skillId, entry.Sort, CDTimer, true);
                if (fail != ActivateFail.None)
                    continue;

                InputBuffer.Consume(entry.SlotId);
                AddSpellInfo(skillId, entry.Sort);
                break;
            }
        }

        /// <summary>确保槽位技能及技能链已 AttachAbility。</summary>
        void EnsureSlotAbilitiesAttached()
        {
            var abilityComp = Combat.GetComponent<AbilityComponent>();
            if (abilityComp == null) return;

            var toAttach = new HashSet<int>();
            SlotRuntime.GetAllSkillIdsToAttach(toAttach);
            Combat.FormComponent?.CollectSkillIds(toAttach);
            foreach (int skillId in toAttach)
            {
                if (skillId <= 0) continue;
                if (!abilityComp.IdAbilities.ContainsKey(skillId))
                    abilityComp.AttachAbility(skillId);
            }
        }

        /// <summary>切换角色/武器时调用，重新加载槽位技能。</summary>
        public void LoadCharacterSlots(CharacterSlotConfig charConfig)
        {
            CharacterSlotConfig = charConfig;
            if (SlotRuntime != null && SlotConfig != null)
            {
                SlotRuntime.LoadCharacter(charConfig);
                Combat.FormComponent?.Init(Forms);
                EnsureSlotAbilitiesAttached();
                SyncPassiveSkillBuffs();
            }
        }

        /// <summary>
        /// 汇总三类来源（槽位链 / 学习树(外部) / 测试列表）并同步常驻被动 Buff。
        /// </summary>
        private void SyncPassiveSkillBuffs()
        {
            if (Combat == null) return;
            var comp = Combat.GetComponent<EGamePlay.Combat.PassiveSkillBuffComponent>();
            if (comp == null)
                comp = Combat.AddComponent<EGamePlay.Combat.PassiveSkillBuffComponent>();

            var ids = PoolManager.Instance.TryGet<List<int>>();
            ids.Clear();

            CollectPassiveSkillIdsFromSlots(ids);
            CollectPassiveSkillIdsFromLearnedTable(ids);
            CollectPassiveSkillIdsFromExtraList(ids);

            comp.SyncFromPassiveSkillIds(ids);
            PoolManager.Instance.Return(ids);
        }

        private void CollectPassiveSkillIdsFromExtraList(List<int> outIds)
        {
            if (ExtraPassiveSkillIds == null || ExtraPassiveSkillIds.Count == 0) return;
            for (int i = 0; i < ExtraPassiveSkillIds.Count; i++)
            {
                int id = ExtraPassiveSkillIds[i];
                if (id > 0) outIds.Add(id);
            }
        }

        /// <summary>
        /// 从“角色技能表/学习树系统”收集被动技能ID。
        /// 这里先留空实现，方便你后续把真实系统接进来（例如：角色成长、天赋、装备词条）。
        /// </summary>
        protected virtual void CollectPassiveSkillIdsFromLearnedTable(List<int> outIds)
        {

        }

        private void CollectPassiveSkillIdsFromSlots(List<int> outIds)
        {
            if (SlotRuntime == null) return;

            var tmp = PoolManager.Instance.TryGet<HashSet<int>>();
            tmp.Clear();
            SlotRuntime.GetAllSkillIdsToAttach(tmp);
            Combat.FormComponent?.CollectSkillIds(tmp);

            foreach (int skillId in tmp)
            {
                if (skillId <= 0) continue;
                var setting = SkillSettingMgr.Instance.GetSkillDemoSetting(skillId);
                if (setting == null) continue;
                if (setting.Type == EGamePlay.Combat.AbilityType.PassiveSkill.ToString())
                    outIds.Add(skillId);
            }

            PoolManager.Instance.Return(tmp);
        }

        private void AddSpellInfo(int skillId, int sort)
        {
            bool useGate = CombatContext.Instance != null && CombatContext.Instance.UseAbilityGate;
            if (!useGate)
            {
                if (CDTimer == null || !CDTimer.IsCDEnd(skillId))
                    return;
                CDTimer.StartCooldown(skillId);
            }

            var info = PoolManager.Instance.TryGet<SkillSpellInfo>();
            info.SkillId = skillId;
            info.Sort = sort;
            if (SkillSortUtil.IsRoll(sort))
            {
                info.Target = null;
                if (CameraRelativeMove.TryGetWorldDirOrLast(out Vector3 dodgeDir))
                    info.Point = Combat.Position + dodgeDir * 3f;
                else
                    info.Point = MathHelper.GetPositionInFront(Combat.Position, Combat.Rotation, 3f);
            }
            else
            {
                info.Target = LockSystem.Instance?.LockedCombatEntity ?? Target;
                info.Point = MathHelper.GetPositionInFront(Combat.Position, Combat.Rotation, 3f);
            }

            Combat.GetComponent<ActSpellComponent>().Enqueue(info);
        }

        public void AddInputRecord(InputListernType cmd, PressType type, InputCallBackType inputCallBackType = InputCallBackType.Performed)
        {
            if (InputBuffer == null)
                return;

            float now = PlayerTime;
            InputBuffer.Tick(now);

            SkillSlotConfig.SlotEntry entry = SlotConfig != null
                ? SlotConfig.FindByInput(cmd, type, inputCallBackType)
                : null;
            if (entry == null)
                return;

            float timeout = entry.InputTimeout > 0 ? entry.InputTimeout : InputTimeout;
            InputBuffer.Set(entry.SlotId, cmd, type, inputCallBackType, now, now + timeout);
        }

        public void ChangeInputMoveState(bool state)
        {
            Combat.ChangeInputMoveState(state);
        }

        // 供轨道调用 //是否有对应的操作
        //未来看看是否能优化一下，每帧遍历不是很友好
        public bool CheckAndConsume(InputListernType cmd, PressType type, InputCallBackType inputCallBackType = InputCallBackType.Performed, float customTimeout = -1f)
        {
            if (InputBuffer == null)
                return false;
            float now = PlayerTime;
            InputBuffer.Tick(now);
            float maxAge = customTimeout > 0f ? customTimeout : InputTimeout;
            return InputBuffer.TryConsume(cmd, type, inputCallBackType, now, maxAge);
        }

        public bool HasValidInput(InputListernType cmd, PressType type, InputCallBackType inputCallBackType = InputCallBackType.Performed)
        {
            if (InputBuffer == null)
                return false;
            InputBuffer.Tick(PlayerTime);
            return InputBuffer.HasCommand(cmd, type, inputCallBackType);
        }

        public void InputRecordsClear()
        {
            InputBuffer?.Clear();
        }

        public bool IsHadInputRecords()
        {
            if (InputBuffer == null)
                return false;
            InputBuffer.Tick(PlayerTime);
            return InputBuffer.HasAny();
        }

        /// <summary>
        /// 连招窗边解析：标签先筛，再按窗边短预输入年龄匹配，Gate 通过后才消费。
        /// 边上 InputTimeout&gt;0 用边配置，否则用 ComboBufferTimeout（不再无限龄）。
        /// </summary>
        public bool TryResolveEdges(List<SkillInputData> edges, out int skillId, out int sort)
        {
            skillId = 0;
            sort = 0;
            if (edges == null || InputBuffer == null || Combat == null)
                return false;

            float now = PlayerTime;
            InputBuffer.Tick(now);
            if (!InputBuffer.HasAny())
                return false;

            for (int i = 0; i < edges.Count; i++)
            {
                SkillInputData data = edges[i];
                if (data == null || data.SkillId <= 0)
                    continue;
                if (Combat.CanSpellSkillWithTagLists(data.RequiredTags, data.BlockedTags) == false)
                    continue;

                float maxAge = ResolveEdgeMaxAge(data.InputTimeout);
                if (!InputBuffer.CanConsume(data.ListernType, data.PressType, data.InputCallBackType, now, maxAge))
                    continue;

                ActivateFail fail = AbilityActivationGate.Evaluate(Combat, data.SkillId, data.SkillSort, CDTimer, true);
                if (fail != ActivateFail.None)
                    continue;

                if (!InputBuffer.TryConsume(data.ListernType, data.PressType, data.InputCallBackType, now, maxAge))
                    continue;

                skillId = data.SkillId;
                sort = data.SkillSort;
                return true;
            }

            return false;
        }

        /// <summary>连招边预输入寿命：边配置优先，否则 ComboBufferTimeout，再否则 InputTimeout。</summary>
        float ResolveEdgeMaxAge(float edgeTimeout)
        {
            if (edgeTimeout > 0f)
                return edgeTimeout;
            if (ComboBufferTimeout > 0f)
                return ComboBufferTimeout;
            return InputTimeout > 0f ? InputTimeout : 0.15f;
        }

    }
}
