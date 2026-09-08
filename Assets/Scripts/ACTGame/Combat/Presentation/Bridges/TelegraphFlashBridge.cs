#if UNITY
using UnityEngine;

namespace ACTGameEditor.Combat
{
    /// <summary>
    /// 预警闪光桥：敌人头顶十字指示（ZZZ 黄/红光）。
    /// 玩法可读性效果，不受 GraphicsFx 画质门控；Stop 触发淡出而非瞬消。
    /// </summary>
    sealed class TelegraphFlashBridge : ICombatFxBridge
    {
        public bool CanPlay(in CombatFxSpec spec)
        {
            return spec.Target != null && !spec.Target.IsDisposed && spec.TelegraphKind != 0;
        }

        public object Play(in CombatFxSpec spec)
        {
            float duration = spec.Duration > 0f ? spec.Duration : CombatTelegraph.DefaultSeconds;
            float height = spec.TelegraphHeight > 0f ? spec.TelegraphHeight : CombatTelegraph.DefaultHeight;
            Color color = CombatTelegraph.KindColor((TelegraphKind)spec.TelegraphKind);
            return TelegraphIndicatorController.Instance.Play(spec.Target, color, duration, height);
        }

        public void Stop(object backendToken, CombatFxKind kind)
        {
            if (backendToken is TelegraphIndicatorController.Token token)
                TelegraphIndicatorController.StopSafe(token);
        }
    }
}
#endif
