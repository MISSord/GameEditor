using ACTGameEditor.Combat.Ai;
using EGamePlay;
using EGamePlay.Combat;
using UnityEngine;

namespace ACTGameEditor.Combat
{
    /// <summary>
    /// 场上 F 谐度破坏：读换入者 Kit.<c>HarmonyBreakSkillId</c>。空列不触发、不回退 13001。
    /// </summary>
    public static class CombatHarmonyBreak
    {
        /// <summary>最远锁定距离（米）。</summary>
        public const float MaxRange = 14f;

        /// <summary>处决镜头优先级。结束时按同档取消，避免占住 SkillCamera 槽。</summary>
        public const int CameraPriority = 28;

        const float CameraDistance = 3.15f;
        const float CameraBlendSeconds = 0.18f;
        const float CameraHoldSeconds = 6f;
        const float LookBlendToTarget = 0.72f;
        const float LookHeight = 1.15f;

        const float MaxRangeSq = MaxRange * MaxRange;

        /// <summary>是否为谐度破坏技（读 SkillCategory，不写死 14001）。</summary>
        public static bool IsHarmonyBreakSkill(int skillId)
        {
            return SkillSettingMgr.Instance != null
                && SkillSettingMgr.Instance.GetSkillCategory(skillId) == SkillCategory.HarmonyBreak;
        }

        /// <summary>场上角色 Kit 破坏技。0 = 没配置，这次 F 失败。</summary>
        public static int ResolveSkillId(CombatEntity actor)
        {
            if (actor == null || SkillSettingMgr.Instance == null)
                return 0;
            CharacterKitSetting kit = SkillSettingMgr.Instance.GetCharacterKitOrNull(actor.CharacterId);
            return kit != null ? kit.HarmonyBreakSkillId : 0;
        }

        /// <summary>
        /// 范围内失谐目标上插入 Kit 破坏技。空列 / 超距 / Gate 拒绝都不清条。
        /// </summary>
        public static bool TryExecute(CombatEntity caster)
        {
            if (!CombatMeterComponent.HarmonyBreakEnabled || caster == null || caster.IsDisposed)
                return false;
            if (caster.IsDead || caster.IsBench || caster.SquadPresence == SquadPresence.Exiting)
                return false;

            int skillId = ResolveSkillId(caster);
            if (skillId <= 0)
                return false;

            if (IsBlockedByCurrentSkill(caster))
                return false;

            if (!TryResolveTarget(caster, out CombatEntity target))
                return false;

            AbilityComponent abilities = caster.GetComponent<AbilityComponent>();
            if (abilities == null)
                return false;
            if (!abilities.IdAbilities.ContainsKey(skillId))
                abilities.AttachAbility(skillId);

            int sort = SkillSortUtil.FromSkillId(skillId);
            ActSpellComponent spell = caster.GetComponent<ActSpellComponent>();
            if (spell == null)
                return false;
            ActivateFail fail = AbilityActivationGate.Evaluate(caster, skillId, sort, spell.CDTimer);
            if (fail != ActivateFail.None)
                return false;

            // 入队前快照下一发普攻：LaunchRunner 会 Break 当前轴，OnSessionStarted 时已经没了。
            (caster.AttackPlayer as IAttackPlayer)?.CaptureHarmonyComboMemory();

            SkillSpellInfo info = PoolManager.Instance.TryGet<SkillSpellInfo>();
            info.SkillId = skillId;
            info.Sort = sort;
            info.Target = target;
            info.Point = target.Position;
            info.IgnoreCooldown = false;
            spell.Enqueue(info);
            return true;
        }

        /// <summary>轴已启动：目标进 Executing，播处决镜头包。</summary>
        public static bool OnSessionStarted(CombatEntity caster, CombatEntity target)
        {
            if (!IsReadyTarget(target) || !InRange(caster, target))
                return false;
            if (!target.DazeMeter.TryBeginHarmonyExecute())
                return false;
            (caster.AttackPlayer as IAttackPlayer)?.SetHarmonyComboPause(true);
            PlayPresentation(caster, target);
            return true;
        }

        /// <summary>轴结束或被销毁：解暂停连招记忆，还镜头，目标进入真空锁零。不自动打下一发普攻。</summary>
        public static void OnSessionEnded(CombatEntity caster, CombatEntity target)
        {
            (caster?.AttackPlayer as IAttackPlayer)?.SetHarmonyComboPause(false);
            CancelExecuteCamera();
            target?.DazeMeter?.BeginHarmonyVacuum();
        }

        /// <summary>锁敌优先，否则导演登记里最近的 Ready 敌人。</summary>
        public static bool TryResolveTarget(CombatEntity caster, out CombatEntity target)
        {
            target = null;
            if (caster == null || caster.IsDisposed)
                return false;

            CombatEntity locked = ACTGameEditor.LockSystem.Instance != null
                ? ACTGameEditor.LockSystem.Instance.LockedCombatEntity
                : null;
            if (IsReadyTarget(locked) && InRange(caster, locked))
            {
                target = locked;
                return true;
            }

            CombatEncounterDirector director = CombatEncounterDirector.Instance;
            if (director != null && director.TryFindNearestHarmonyReady(caster, MaxRangeSq, out target))
                return true;

            target = null;
            return false;
        }

        /// <summary>目标失谐且在范围内。</summary>
        public static bool IsValidReadyTarget(CombatEntity caster, CombatEntity target)
        {
            return IsReadyTarget(target) && InRange(caster, target);
        }

        static bool IsReadyTarget(CombatEntity target)
        {
            if (target == null || target.IsDisposed || target.IsDead)
                return false;
            CombatMeterComponent meter = target.DazeMeter;
            return meter != null && meter.IsHarmonyReady;
        }

        static bool IsBlockedByCurrentSkill(CombatEntity caster)
        {
            ISkillExecutionHandle exec = caster.ActiveExecution;
            if (exec == null || exec.IsDisposed || exec.IsMainFinish)
                return false;
            if (exec.Sort >= (int)SkillSort.Ultimate)
                return true;

            ActSkillRunner runner = caster.SpellingExecution;
            Ability ability = runner != null ? runner.AbilityEntity : null;
            if (ability == null)
                return false;

            SkillCategory cat = SkillSettingMgr.Instance != null
                ? SkillSettingMgr.Instance.GetSkillCategory(ability.SkillID)
                : SkillCategory.None;
            return cat == SkillCategory.Ultimate
                || cat == SkillCategory.HarmonyBreak
                || cat == SkillCategory.AssistFollowUp
                || cat == SkillCategory.DefensiveAssist
                || cat == SkillCategory.EvasiveAssist
                || cat == SkillCategory.QuickAssist
                || cat == SkillCategory.Chain;
        }

        static bool InRange(CombatEntity caster, CombatEntity target)
        {
            if (caster == null || target == null)
                return false;
            Vector3 d = target.Position - caster.Position;
            d.y = 0f;
            return d.sqrMagnitude <= MaxRangeSq;
        }

        static void PlayPresentation(CombatEntity caster, CombatEntity target)
        {
#if UNITY
            var fx = CombatFxPlayContext.ForOwner(caster, CombatFxSource.Entity(caster.Id));
            fx.ActionCreator = caster;
            fx.ActionTarget = target;
            CombatFxPackagePlayer.Play(CombatFxPackageId.HarmonyBreak, in fx);

            CameraManager cam = CameraManager.Instance;
            if (cam == null)
                return;

            Vector3 look = Vector3.Lerp(caster.Position, target.Position, LookBlendToTarget);
            look.y += LookHeight;
            cam.SubmitIntent(new CameraIntent(
                CameraIntentSource.SkillCamera,
                look,
                CameraDistance,
                priority: CameraPriority,
                blendTime: CameraBlendSeconds,
                duration: CameraHoldSeconds));
#endif
        }

        static void CancelExecuteCamera()
        {
#if UNITY
            CameraManager.Instance?.CancelIntent(CameraIntentSource.SkillCamera, CameraPriority);
#endif
        }
    }
}
