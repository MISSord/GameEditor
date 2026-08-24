using UnityEngine;

namespace ACTGameEditor
{
    /// <summary>战斗层伤害飘字展示接口，由 UI 层实现并注册。</summary>
    public interface IDamageTextPresenter
    {
        /// <summary>在指定世界坐标展示伤害数值。</summary>
        void ShowDamage(float damageValue, Vector3 worldPosition);
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
