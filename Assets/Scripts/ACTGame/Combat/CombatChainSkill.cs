using ACTGameEditor.Combat.Ai;
using EGamePlay.Combat;
using UnityEngine;

namespace ACTGameEditor.Combat
{
    /// <summary>
    /// 单人连携：失衡窗内按 K / ButtonA 打 13001。
    /// 三人轮转 / 喧响不在本切片。
    /// </summary>
    public static class CombatChainSkill
    {
        /// <summary>连携技 SkillId。</summary>
        public const int SkillId = 13001;

        /// <summary>13001 轴未导出时暂用普攻 3 的盒与动画。</summary>
        public const int FallbackTimelineSkillId = 11003;

        /// <summary>最远锁定距离（米）。</summary>
        public const float MaxRange = 14f;

        const float MaxRangeSq = MaxRange * MaxRange;

        /// <summary>是否为连携技。</summary>
        public static bool IsChainSkill(int skillId) => skillId == SkillId;

        /// <summary>目标处于可打连携的失衡窗。</summary>
        public static bool IsValidWindow(CombatEntity target)
        {
            if (target == null || target.IsDisposed || target.IsDead)
                return false;
            CombatMeterComponent meter = target.DazeMeter;
            return meter != null && meter.IsChainWindow;
        }

        /// <summary>锁敌优先，否则导演登记里最近的失衡窗敌人。</summary>
        public static bool TryResolveTarget(CombatEntity caster, out CombatEntity target)
        {
            target = null;
            if (caster == null || caster.IsDisposed)
                return false;

            CombatEntity locked = ACTGameEditor.LockSystem.Instance != null
                ? ACTGameEditor.LockSystem.Instance.LockedCombatEntity
                : null;
            if (IsValidWindow(locked) && InRange(caster, locked))
            {
                target = locked;
                return true;
            }

            CombatEncounterDirector director = CombatEncounterDirector.Instance;
            if (director != null && director.TryFindNearestChainWindow(caster, MaxRangeSq, out target))
                return true;

            target = null;
            return false;
        }

        /// <summary>轴已启动：扣一次连携次数、暂停掉条、播镜头与表现包。</summary>
        public static bool OnSessionStarted(CombatEntity caster, CombatEntity target)
        {
            if (!IsValidWindow(target) || !InRange(caster, target))
                return false;
            if (!target.DazeMeter.TryConsumeChain())
                return false;

            target.DazeMeter.SetDrainPaused(true);
            PlayPresentation(caster, target);
            return true;
        }

        /// <summary>连携轴结束或被销毁：恢复掉条。</summary>
        public static void OnSessionEnded(CombatEntity target)
        {
            target?.DazeMeter?.SetDrainPaused(false);
        }

        static bool InRange(CombatEntity caster, CombatEntity target)
        {
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
            CombatFxPackagePlayer.Play(CombatFxPackageId.ChainAttack, in fx);

            CameraManager cam = CameraManager.Instance;
            if (cam == null)
                return;

            Vector3 look = target.Position + Vector3.up * 1.2f;
            cam.SubmitIntent(new CameraIntent(
                CameraIntentSource.SkillCamera,
                look,
                3.6f,
                priority: 25,
                blendTime: 0.2f,
                duration: 1.1f));
#endif
        }
    }
}
