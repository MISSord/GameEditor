using EGamePlay;

namespace EGamePlay.Unity
{
    //unity编辑器下使用
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
            if(GameObjectPool.Instance != null)
            {
                GameObject.transform.SetParent(GameObjectPool.Instance.GetComponent<GameObjectComponent>().GameObject.transform);
            }
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            UnityEngine.GameObject.Destroy(GameObject);
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
                    UnityEngine.GameObject.Destroy(item);
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