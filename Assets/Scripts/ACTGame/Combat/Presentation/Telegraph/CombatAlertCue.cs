using EGamePlay.Combat;
using UnityEngine;

namespace ACTGameEditor.Combat
{
    /// <summary>
    /// Alert 警戒占位：头顶暖橙十字，时长跟首次/再接敌延迟。正式感叹号/音效到位前用这条，不走攻击 Telegraph。
    /// </summary>
    public static class CombatAlertCue
    {
        static readonly Color Color = new(1f, 0.72f, 0.12f);
        const float Height = 2.55f;

        /// <summary>在宿主头顶亮一次；seconds≤0 或宿主无效则忽略。</summary>
        public static void Play(CombatEntity owner, float seconds)
        {
            if (owner == null || owner.IsDisposed || seconds <= 0f)
                return;

            TelegraphIndicatorController.Instance.Play(owner, Color, seconds, Height);
        }
    }
}
