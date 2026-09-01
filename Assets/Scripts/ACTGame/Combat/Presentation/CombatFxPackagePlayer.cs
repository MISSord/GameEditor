using EGamePlay;
using EGamePlay.Combat;

namespace ACTGameEditor.Combat
{
    /// <summary>将 Package 展开为 <see cref="CombatFxSpec"/> 并交给 Director。</summary>
    public static class CombatFxPackagePlayer
    {
        /// <summary>播放目录中的 Package；至少一条成功返回 true。</summary>
        public static bool TryPlay(CombatFxPackageId packageId, in CombatFxPlayContext context)
        {
            if (packageId == CombatFxPackageId.None)
                return false;

            CombatFxPackageCatalog catalog = CombatFxPackageCatalog.Active;
            if (!catalog.TryGetPackage(packageId, out CombatFxPackageDefinition package))
                return false;

            return TryPlayDefinition(package, in context);
        }

        /// <summary>播放目录中的 Package。</summary>
        public static void Play(CombatFxPackageId packageId, in CombatFxPlayContext context) =>
            TryPlay(packageId, in context);

        /// <summary>展开 Package 内全部 Entry；至少一条成功返回 true。</summary>
        public static bool TryPlayDefinition(CombatFxPackageDefinition package, in CombatFxPlayContext context)
        {
            if (package?.Entries == null || package.Entries.Count == 0)
                return false;

            bool any = false;
            for (int i = 0; i < package.Entries.Count; i++)
            {
                if (TryPlayEntry(package.Entries[i], in context))
                    any = true;
            }

            return any;
        }

        public static void PlayDefinition(CombatFxPackageDefinition package, in CombatFxPlayContext context) =>
            TryPlayDefinition(package, in context);

        /// <summary>ActionPoint 规则：判条件 + 播包。</summary>
        public static bool TryPlayActionPointRule(
            CombatFxTriggerRuleDefinition rule,
            CombatEntity owner,
            Entity action,
            in CombatFxPlayContext context)
        {
            if (rule == null || rule.TriggerKind != CombatFxTriggerKind.ActionPoint)
                return false;

            if (action is DamageAction damage && !PassDamageFlags(damage, rule.Flags))
                return false;

            if (rule.Flags.HasFlag(CombatFxTriggerFlags.LocalTruePlayerOnly)
                && (owner == null || !owner.isTruePlayer))
            {
                return false;
            }

            Play(rule.PackageId, in context);
            return true;
        }

        public static bool PassDamageFlags(DamageAction damage, CombatFxTriggerFlags flags)
        {
            if (flags.HasFlag(CombatFxTriggerFlags.RequirePositiveDamage) && damage.DamageValue <= 0)
                return false;
            if (flags.HasFlag(CombatFxTriggerFlags.SkipOnDodge)
                && damage.DamageActionEffect.HasFlag(DamageActionEffect.Dodge))
                return false;
            if (flags.HasFlag(CombatFxTriggerFlags.SkipOnImmunity)
                && damage.DamageActionEffect.HasFlag(DamageActionEffect.Immunity))
                return false;
            if (flags.HasFlag(CombatFxTriggerFlags.SkipOnInterrupt)
                && damage.DamageActionEffect.HasFlag(DamageActionEffect.Interrupt))
                return false;
            return true;
        }

        static float ResolveDuration(in CombatFxPackageEntry entry, in CombatFxPlayContext context, float entryDefault)
        {
            if (context.DurationOverride > 0f)
                return context.DurationOverride;
            return entry.Duration > 0f ? entry.Duration : entryDefault;
        }

        static bool TryPlayEntry(in CombatFxPackageEntry entry, in CombatFxPlayContext context)
        {
#if UNITY
            if (!TryBuildSpec(in entry, in context, out CombatFxSpec spec))
                return false;
            return CombatPresentationDirector.Play(spec).IsValid;
#else
            return false;
#endif
        }

#if UNITY
        internal static bool TryBuildSpec(
            in CombatFxPackageEntry entry,
            in CombatFxPlayContext context,
            out CombatFxSpec spec)
        {
            spec = default;
            CombatFxPreset preset = CombatFxPreset.Active;

            switch (entry.Kind)
            {
                case CombatFxKind.HitFlash:
                    spec = CombatFxSpec.HitFlash(
                        context.Source,
                        ResolveTarget(entry.TargetMode, in context),
                        ResolveDuration(in entry, in context, preset.HitFlashDuration));
                    spec.RespectGraphicsGate = entry.RespectGraphicsGate;
                    return spec.Target != null;

                case CombatFxKind.HitStop:
                    spec = CombatFxSpec.HitStop(
                        context.Source,
                        ResolveDuration(in entry, in context, preset.HitStopDuration),
                        entry.WorldScale > 0f ? entry.WorldScale : preset.HitStopWorldScale,
                        entry.PlayCameraImpact);
                    spec.RespectGraphicsGate = entry.RespectGraphicsGate;
                    return true;

                case CombatFxKind.TimeFracture:
                    spec = CombatFxSpec.TimeFracture(
                        context.Source,
                        ResolveDuration(in entry, in context, 0.5f),
                        entry.WorldScale > 0f ? entry.WorldScale : preset.TimeFractureWorldScale);
                    spec.RespectGraphicsGate = entry.RespectGraphicsGate;
                    return true;

                case CombatFxKind.SkillTimeStop:
                    spec = CombatFxSpec.SkillTimeStop(
                        context.Source,
                        ResolveDuration(in entry, in context, 1f));
                    spec.RespectGraphicsGate = entry.RespectGraphicsGate;
                    return true;

                case CombatFxKind.DeathDissolve:
                    ICombatUnit dissolveTarget = ResolveTarget(entry.TargetMode, in context);
                    if (dissolveTarget == null)
                        return false;
                    spec = new CombatFxSpec
                    {
                        Kind = CombatFxKind.DeathDissolve,
                        Source = context.Source,
                        Target = dissolveTarget,
                        Duration = ResolveDuration(in entry, in context, 1.2f),
                        OnComplete = context.OnComplete,
                        RespectGraphicsGate = entry.RespectGraphicsGate,
                    };
                    return true;

                case CombatFxKind.Afterimage:
                case CombatFxKind.ScreenDesaturate:
                case CombatFxKind.HitParticle:
                case CombatFxKind.HitAudio:
                case CombatFxKind.RadialBlurImpact:
                    return false;

                default:
                    return false;
            }
        }

        static ICombatUnit ResolveTarget(CombatFxTargetMode mode, in CombatFxPlayContext context)
        {
            return mode switch
            {
                CombatFxTargetMode.Owner => context.Owner,
                CombatFxTargetMode.ActionTarget => context.ActionTarget,
                CombatFxTargetMode.ActionCreator => context.ActionCreator,
                CombatFxTargetMode.Explicit => context.ExplicitTarget,
                _ => null,
            };
        }
#endif
    }
}
