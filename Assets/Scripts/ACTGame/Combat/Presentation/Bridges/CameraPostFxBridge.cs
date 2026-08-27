#if UNITY
using EGamePlay.Combat;

namespace ACTGameEditor.Combat
{
    sealed class CameraPostFxBridge : ICombatFxBridge
    {
        public bool CanPlay(in CombatFxSpec spec)
        {
            if (!spec.RespectGraphicsGate)
                return true;
            return GraphicsFxService.Query(GraphicsFxId.RadialBlur)
                || GraphicsFxService.Query(GraphicsFxId.ChromaticAberration);
        }

        public object Play(in CombatFxSpec spec)
        {
            if (spec.Kind == CombatFxKind.RadialBlurImpact || spec.PlayCameraImpact)
            {
                CameraPostFxController.TryPlayHitStopImpact(spec.Duration);
                return CameraImpactToken.Instance;
            }

            return null;
        }

        public void Stop(object backendToken, CombatFxKind kind) { }

        sealed class CameraImpactToken
        {
            public static readonly CameraImpactToken Instance = new CameraImpactToken();
        }
    }
}
#endif
