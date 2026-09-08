#if UNITY
namespace ACTGameEditor.Combat
{
    /// <summary>
    /// 镜头震动桥：Trauma 震屏 + FOV 冲击。
    /// 震屏自衰减，Stop 不打断（避免技能 Break 时震动瞬断的违和感）。
    /// 画质门控在 CameraShakeController.Play 内按开关分别处理。
    /// </summary>
    sealed class CameraShakeBridge : ICombatFxBridge
    {
        public bool CanPlay(in CombatFxSpec spec)
        {
            return !spec.RespectGraphicsGate
                || GraphicsFxService.Query(GraphicsFxId.ScreenShake)
                || GraphicsFxService.Query(GraphicsFxId.FovPunch);
        }

        public object Play(in CombatFxSpec spec)
        {
            CameraShakeController.Instance?.Play(
                spec.ShakeProfile,
                ignoreGate: !spec.RespectGraphicsGate,
                spec.KickDirectionWorld,
                spec.KickAmplitude,
                spec.KickDuration);
            return ShakeToken.Instance;
        }

        public void Stop(object backendToken, CombatFxKind kind)
        {
            // 无操作：让震动自然衰减完
        }

        sealed class ShakeToken
        {
            public static readonly ShakeToken Instance = new ShakeToken();
        }
    }
}
#endif
