using UnityEngine;

namespace EGamePlay.Unity
{
    /// <summary>
    /// 战斗实体 Unity 侧初始化数据（Animator、CC、装配器与角色属性）。
    /// </summary>
    public struct GameObjectData
    {
        public Animator animator;
        public CharacterController controller;
        public AgentTag agent;
        public PlayerMoveSettingSo playerSetting;

        /// <summary>InputMove 战斗侧装配；未设置则不绑定 Locomotion 依赖。</summary>
        public IInputMoveBinder inputMoveBinder;

        /// <summary>AnimDirector 使用的玩家层时间源。</summary>
        public IAnimTimeScaleSource animTimeScale;

        /// <summary>角色配置 ID，用于从 RoleAttriSetting 表读取属性。</summary>
        public int CharacterId;

        /// <summary>角色等级，用于属性计算：基础值 + 等级 * 增长值。</summary>
        public int Level;

        /// <summary>仅本地主控角色。人机即使 AgentTag 为 PlayerA 也必须为 false，否则 NetId 会与主控撞成 0。</summary>
        public bool isTruePlayer;
    }

    /// <summary>编辑器下 Entity 对应 GameObject 的挂载根。</summary>
    public sealed class GameObjectPool : Entity
    {
        public static GameObjectPool Instance;

        public override void Awake()
        {
            Instance = this;
        }
    }

    /// <summary>unity 编辑器下使用：为 Entity 建调试用 GameObject。</summary>
    public sealed class GameObjectComponent : Component
    {
        public UnityEngine.GameObject GameObject { get; private set; }

        public override void Awake()
        {
            GameObject = new UnityEngine.GameObject(Entity.GetType().Name);
            var view = GameObject.AddComponent<ComponentView>();
            view.Type = GameObject.name;
            view.Component = this;
            //先放在Pool对象下面，用到的时候再拿出来
            if (GameObjectPool.Instance != null)
            {
                GameObject.transform.SetParent(GameObjectPool.Instance.GetComponent<GameObjectComponent>().GameObject.transform);
            }
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            UnityEngine.Object.Destroy(GameObject);
        }

        public void OnNameChanged(string name)
        {
            GameObject.name = $"{Entity.GetType().Name}: {name}";
        }

        public void OnAddComponent(Component component)
        {
            var view = GameObject.AddComponent<ComponentView>();
            view.Type = component.GetType().Name;
            view.Component = component;
        }

        public void OnRemoveComponent(Component component)
        {
            var comps = GameObject.GetComponents<ComponentView>();
            foreach (var item in comps)
            {
                if (item.Component == component)
                {
                    UnityEngine.Object.Destroy(item);
                }
            }
        }

        public void OnAddChild(Entity child)
        {
            if (child.GetComponent<GameObjectComponent>() != null)
            {
                child.GetComponent<GameObjectComponent>().GameObject.transform.SetParent(GameObject.transform);
            }
        }
    }
}
