using EGamePlay.Combat;
using UnityEngine;

namespace ACTGameEditor
{
    /// <summary>飘字来源，用于错开叠字与区分样式。</summary>
    public enum DamageTextKind : byte
    {
        /// <summary>技能 / 普攻命中。</summary>
        Skill = 0,
        /// <summary>Buff / 点燃等持续伤害。</summary>
        Buff = 1,
    }

    /// <summary>单次伤害飘字请求；命中时构造，UI 层只读。</summary>
    public readonly struct DamageTextRequest
    {
        public readonly float Value;
        public readonly Vector3 WorldPosition;
        public readonly DamageTextKind Kind;
        public readonly long TargetId;
        public readonly bool IsCritical;
        public readonly DamageType DamageType;
        /// <summary>true 表示本地玩家挨打，用受击红字。</summary>
        public readonly bool Incoming;

        public DamageTextRequest(
            float value,
            Vector3 worldPosition,
            DamageTextKind kind,
            long targetId,
            bool isCritical,
            DamageType damageType,
            bool incoming)
        {
            Value = value;
            WorldPosition = worldPosition;
            Kind = kind;
            TargetId = targetId;
            IsCritical = isCritical;
            DamageType = damageType;
            Incoming = incoming;
        }
    }

    /// <summary>战斗层伤害飘字展示接口，由 UI 层实现并注册。</summary>
    public interface IDamageTextPresenter
    {
        /// <summary>在指定世界坐标展示伤害数值。</summary>
        void ShowDamage(float damageValue, Vector3 worldPosition);

        /// <summary>在指定世界坐标展示伤害数值，并按来源与目标错开叠字。</summary>
        void ShowDamage(float damageValue, Vector3 worldPosition, DamageTextKind kind, long targetId);

        /// <summary>按完整请求展示飘字（暴击、属性色、受击红字）。</summary>
        void ShowDamage(in DamageTextRequest request);
    }

    /// <summary>伤害飘字 presenter 注册表；ACTGame 通过此入口调用，不依赖 XiaoCao UI。</summary>
    public static class DamageTextPresenter
    {
        /// <summary>当前激活的 presenter，未注册时为 null。</summary>
        public static IDamageTextPresenter Active { get; private set; }

        /// <summary>注册 UI 实现（通常在 UIMrg.Awake 中调用）。</summary>
        public static void Register(IDamageTextPresenter presenter)
        {
            Active = presenter;
        }

        /// <summary>注销 UI 实现（通常在 UIMrg.OnDestroy 中调用）。</summary>
        public static void Unregister(IDamageTextPresenter presenter)
        {
            if (Active == presenter)
                Active = null;
        }
    }
}
