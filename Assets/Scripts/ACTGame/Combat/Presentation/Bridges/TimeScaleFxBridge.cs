using EGamePlay;

namespace ACTGameEditor.Combat
{
    /// <summary>全局时间流速：TimeScaleEffectManager。</summary>
    sealed class TimeScaleFxBridge : ICombatFxBridge
    {
        public bool CanPlay(in CombatFxSpec spec) => spec.Duration > 0f || spec.Kind == CombatFxKind.SkillTimeStop;

        public object Play(in CombatFxSpec spec)
        {
            switch (spec.Kind)
            {
                case CombatFxKind.SkillTimeStop:
                    return TimeScaleEffectManager.AddEffect(
                        TimeScaleEffectType.SkillTimescale,
                        0f,
                        spec.PlayerScale > 0f ? spec.PlayerScale : 1f,
                        spec.CameraScale > 0f ? spec.CameraScale : 1f,
                        spec.Duration,
                        spec.TimePriority > 0 ? spec.TimePriority : 20);

                case CombatFxKind.TimeFracture:
                    return TimeScaleEffectManager.AddTimeFracture(
                        spec.Duration,
                        spec.WorldScale > 0f ? spec.WorldScale : 0.3f);

                case CombatFxKind.HitStop:
                    return TimeScaleEffectManager.AddHitStop(
                        spec.Duration,
                        spec.WorldScale > 0f ? spec.WorldScale : 0.1f);

                default:
                    return null;
            }
        }

        public void Stop(object backendToken, CombatFxKind kind)
        {
            if (backendToken is TimeScaleEffect effect)
                TimeScaleEffectManager.RemoveEffect(effect);
        }
    }
}
