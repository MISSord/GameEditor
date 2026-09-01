using UnityEngine;

namespace EGamePlay.Combat
{
    /// <summary>命中裁决结果。格挡/霸体在 HitPipeline.FilterDefend 扩展时再增加枚举值。</summary>
    public enum HitResultKind : byte
    {
        Land = 0,
        Ignored = 1,
    }

    /// <summary>
    /// 盒体申报的命中请求。只携带引用，不拷贝效果列表，避免物理回调分配。
    /// </summary>
    public struct HitRequest
    {
        public ICombatUnit Attacker;
        public ICombatUnit Defender;
        public IHitSubRunner Runner;
        public object TriggerEvent;
        /// <summary>攻击盒与受击体最近点；无碰撞采样时为 false。</summary>
        public bool HasHitWorldPosition;
        public Vector3 HitWorldPosition;
    }
}
