using System;
using EGamePlay.Combat;
using UnityEngine;

namespace ACTGameEditor.Combat
{
    /// <summary>
    /// 敌人出手预警门面：Brain 兜底与时间轴 AiTelegraph 消息共用同一出口。
    /// 视觉走 <see cref="TelegraphIndicatorController"/> 头顶十字闪光（美术资产占位）。
    /// 音频接入点：订阅 <see cref="Shown"/>，决策层与表现层都不关心谁听。
    /// </summary>
    public static class CombatTelegraph
    {
        /// <summary>预警默认时长（秒，unscaled）。招表 TelegraphSeconds ≤0 时用。</summary>
        public const float DefaultSeconds = 0.5f;

        /// <summary>指示器在宿主头顶的高度偏移（米，按 1.8m 身高）。</summary>
        public const float DefaultHeight = 2.2f;

        /// <summary>
        /// 预警展示事件（类型 + 世界坐标）。
        /// 音频资产到位前无人订阅、零开销；接入提示音时在音频系统里订阅本事件即可。
        /// </summary>
        public static event Action<TelegraphKind, Vector3> Shown;

        /// <summary>
        /// Brain 兜底：借用的玩家轴上没有 AiTelegraph 消息，承诺出招（Enqueue 成功）时播固定时长预警。
        /// <paramref name="seconds"/> &lt; 0 视为轴上已挂消息，不播。源绑实体：死亡经 StopByEntity 自动收尾。
        /// </summary>
        public static CombatFxHandle PlayFromBrain(CombatEntity owner, TelegraphKind kind, float seconds)
        {
            if (owner == null || owner.IsDisposed || seconds < 0f)
                return CombatFxHandle.Invalid;
            return Play(owner, kind, seconds, CombatFxSource.Entity(owner.Id));
        }

        /// <summary>
        /// 时间轴 AiTelegraph 消息（§9.1 主路径）：StrMsg=类型名，FloatMsg=时长秒（0=默认）。
        /// 源跟技能走：轴 Break / Finish 时经 StopBySource 自动淡出。
        /// </summary>
        public static CombatFxHandle PlayFromTimeline(CombatEntity owner, string kindName, float seconds, TagSource? timelineSource)
        {
            if (owner == null || owner.IsDisposed)
                return CombatFxHandle.Invalid;
            TelegraphKind kind = ParseKind(kindName);
            CombatFxSource source = timelineSource.HasValue
                ? CombatFxSource.From(timelineSource.Value)
                : CombatFxSource.Entity(owner.Id);
            return Play(owner, kind, seconds, source);
        }

        /// <summary>类型配色：Parry=黄（绝区零可招架）、Unblockable=红、Jump=青、Dodge=暖白。</summary>
        public static Color KindColor(TelegraphKind kind)
        {
            switch (kind)
            {
                case TelegraphKind.Parry: return new Color(1f, 0.85f, 0.2f);
                case TelegraphKind.Unblockable: return new Color(1f, 0.16f, 0.12f);
                case TelegraphKind.Jump: return new Color(0.35f, 0.9f, 1f);
                default: return new Color(1f, 0.95f, 0.55f);
            }
        }

        static CombatFxHandle Play(CombatEntity owner, TelegraphKind kind, float seconds, CombatFxSource source)
        {
            if (kind == TelegraphKind.None)
                return CombatFxHandle.Invalid;
            if (seconds <= 0f)
                seconds = DefaultSeconds;

            CombatFxHandle handle = CombatPresentationDirector.Play(
                CombatFxSpec.Telegraph(source, owner, (byte)kind, seconds, DefaultHeight));
            if (handle.IsValid)
                Shown?.Invoke(kind, owner.Position + Vector3.up * DefaultHeight);
            return handle;
        }

        /// <summary>按枚举名（大小写不敏感）或数字解析；无法识别视为 None 不播。</summary>
        static TelegraphKind ParseKind(string name)
        {
            if (!string.IsNullOrEmpty(name) && Enum.TryParse(name, true, out TelegraphKind kind))
                return kind;
            return TelegraphKind.None;
        }
    }
}
