using System;
using EGamePlay;
using EGamePlay.Combat;
using EGamePlay.Unity;

namespace ACTGameEditor.Combat
{
    /// <summary>全局时间流速：TimeScaleEffectManager；HitStop 另写实体钟。</summary>
    sealed class TimeScaleFxBridge : ICombatFxBridge
    {
        public bool CanPlay(in CombatFxSpec spec) => spec.Duration > 0f || spec.Kind == CombatFxKind.SkillTimeStop;

        public object Play(in CombatFxSpec spec)
        {
            switch (spec.Kind)
            {
                case CombatFxKind.SkillTimeStop:
                {
                    TimeScaleEffect stop = TimeScaleEffectManager.AddEffect(
                        TimeScaleEffectType.SkillTimescale,
                        0f,
                        spec.PlayerScale > 0f ? spec.PlayerScale : 1f,
                        spec.CameraScale > 0f ? spec.CameraScale : 1f,
                        spec.Duration,
                        spec.TimePriority > 0 ? spec.TimePriority : 20);
                    if (stop == null)
                        return null;
                    BindSkillTimeStopClockHold(spec.ClockHoldUnit, stop);
                    return stop;
                }

                case CombatFxKind.TimeFracture:
                    return TimeScaleEffectManager.AddTimeFracture(
                        spec.Duration,
                        spec.WorldScale > 0f ? spec.WorldScale : 0.3f);

                case CombatFxKind.HitStop:
                {
                    int priority = spec.TimePriority > 0 ? spec.TimePriority : 10;
                    TimeScaleEffect pulse = TimeScaleEffectManager.AddHitStop(spec.Duration, priority);
                    if (pulse == null)
                        return null;
                    float entityScale = spec.WorldScale > 0f ? spec.WorldScale : 0.08f;
                    BindHitStopUnits(spec.HitStopAttacker, spec.Target, entityScale, pulse);
                    return pulse;
                }

                default:
                    return null;
            }
        }

        public void Stop(object backendToken, CombatFxKind kind)
        {
            if (backendToken is TimeScaleEffect effect)
                TimeScaleEffectManager.RemoveEffect(effect);
        }

        static void BindSkillTimeStopClockHold(ICombatUnit unit, TimeScaleEffect effect)
        {
            if (effect == null || unit is not CombatEntity owner || owner.IsDisposed)
                return;

            owner.AddPlayerCombatClockHold();
            owner.GetComponent<AnimComponent>()?.Director?.RefreshSpeedFromOwner();
            Action previous = effect.OnRemoved;
            effect.OnRemoved = () =>
            {
                if (!owner.IsDisposed)
                {
                    owner.RemovePlayerCombatClockHold();
                    owner.GetComponent<AnimComponent>()?.Director?.RefreshSpeedFromOwner();
                }
                previous?.Invoke();
            };
        }

        static void BindHitStopUnits(ICombatUnit attacker, ICombatUnit defender, float entityScale, TimeScaleEffect effect)
        {
            Apply(attacker, entityScale);
            if (defender != null && !ReferenceEquals(defender, attacker))
                Apply(defender, entityScale);

            Action previous = effect.OnRemoved;
            effect.OnRemoved = () =>
            {
                Clear(attacker);
                if (defender != null && !ReferenceEquals(defender, attacker))
                    Clear(defender);
                previous?.Invoke();
            };
        }

        static void Apply(ICombatUnit unit, float scale)
        {
            if (unit is CombatEntity entity && !entity.IsDisposed)
                entity.ApplyHitStopTimeScale(scale);
        }

        static void Clear(ICombatUnit unit)
        {
            if (unit is CombatEntity entity && !entity.IsDisposed)
                entity.ClearHitStopTimeScale();
        }
    }
}
