#if UNITY
using EGamePlay.Combat;

namespace ACTGameEditor.Combat
{
    /// <summary>镜头后处理：RadialBlur / CA / 灰屏。</summary>
    sealed class CameraPostFxBridge : ICombatFxBridge
    {
        public bool CanPlay(in CombatFxSpec spec)
        {
            if (!spec.RespectGraphicsGate)
                return true;

            if (spec.Kind == CombatFxKind.ScreenDesaturate)
                return true;

            return GraphicsFxService.Query(GraphicsFxId.RadialBlur)
                || GraphicsFxService.Query(GraphicsFxId.ChromaticAberration);
        }

        public object Play(in CombatFxSpec spec)
        {
            if (spec.Kind == CombatFxKind.ScreenDesaturate)
            {
                float duration = spec.Duration > 0f ? spec.Duration : 0.5f;
                CameraPostFxController.TryPlayDesaturate(duration);
                return CameraImpactToken.Instance;
            }

            if (spec.Kind == CombatFxKind.RadialBlurImpact || spec.PlayCameraImpact)
            {
                CameraPostFxController.TryPlayHitStopImpact(spec.Duration);
                return CameraImpactToken.Instance;
            }

            return null;
        }

        public void Stop(object backendToken, CombatFxKind kind)
        {
            if (kind == CombatFxKind.ScreenDesaturate)
                CameraPostFxController.TryStopDesaturate();
        }

        sealed class CameraImpactToken
        {
            public static readonly CameraImpactToken Instance = new CameraImpactToken();
        }
    }
}
#endif
