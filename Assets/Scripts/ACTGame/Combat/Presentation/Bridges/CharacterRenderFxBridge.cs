#if UNITY
using EGamePlay.Combat;

namespace ACTGameEditor.Combat
{
    sealed class CharacterRenderFxBridge : ICombatFxBridge
    {
        public bool CanPlay(in CombatFxSpec spec)
        {
            if (!spec.RespectGraphicsGate)
                return true;

            return spec.Kind switch
            {
                CombatFxKind.HitFlash => GraphicsFxService.Query(GraphicsFxId.HitFlash),
                CombatFxKind.DeathDissolve => GraphicsFxService.Query(GraphicsFxId.Dissolve),
                _ => true,
            };
        }

        public object Play(in CombatFxSpec spec)
        {
            CharacterRenderFX renderFx = ResolveRenderFx(spec.Target);
            if (renderFx == null)
                return null;

            switch (spec.Kind)
            {
                case CombatFxKind.HitFlash:
                    float flashDuration = spec.Duration > 0f ? spec.Duration : 0.12f;
                    renderFx.Flash(flashDuration);
                    return renderFx;

                case CombatFxKind.DeathDissolve:
                    float dissolveDuration = spec.Duration > 0f ? spec.Duration : 1.2f;
                    renderFx.PlayDissolve(dissolveDuration, spec.OnComplete);
                    return renderFx;

                default:
                    return null;
            }
        }

        public void Stop(object backendToken, CombatFxKind kind)
        {
            if (backendToken is not CharacterRenderFX renderFx)
                return;

            if (kind == CombatFxKind.DeathDissolve || kind == CombatFxKind.HitFlash)
                renderFx.ResetFX();
        }

        static CharacterRenderFX ResolveRenderFx(ICombatUnit target)
        {
            if (target?.Entity is not CombatEntity combat)
                return null;

            ActPlayer actPlayer = combat.AttackPlayer;
            if (actPlayer == null)
                return null;

            return actPlayer.GetComponent<CharacterRenderFX>();
        }
    }
}
#endif
