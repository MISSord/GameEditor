#if UNITY
using EGamePlay.Combat;

namespace ACTGameEditor.Combat
{
    /// <summary>镜头后处理：RadialBlur / CA。</summary>
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

        public void Stop(object backendToken, CombatFxKind kind)
        {
            // 脉冲型镜头效果按时长自结束，无需强制撤销。
        }

        sealed class CameraImpactToken
        {
            public static readonly CameraImpactToken Instance = new CameraImpactToken();
        }
    }
}
#endif
