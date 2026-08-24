using EGamePlay.Unity;
using System;
using System.Collections.Generic;

namespace EGamePlay
{
    public abstract partial class Entity : IResettable
    {
        //个体属性部分
        public long Id { get; set; }
        private string _name;
        public string Name
        {
            get => _name;
            set
            {
                _name = value;
            }
        }
        public long InstanceId { get; set; }
        public bool IsDisposed { get { return InstanceId == 0; } }

        //父子管理
        private Entity _parent;
        public Entity Parent { get { return _parent; } }
        public List<Entity> Children { get; private set; } = new List<Entity>();

        //暂时关闭，暂无需求，提高性能
        //public Dictionary<long, Entity> Id2Children { get; private set; } = new Dictionary<long, Entity>();
        //public Dictionary<Type, List<Entity>> Type2Children { get; private set; } = new Dictionary<Type, List<Entity>>();

        //组件管理
        public Dictionary<Type, Component> Components { get; set; } = new Dictionary<Type, Component>();

        //Update部分
        public List<Component> UpdateComponents { get; private set; } = new List<Component>();
        public List<Component> FixedUpdateComponents { get; private set; } = new List<Component>();
        public bool IsNeedUpdate => UpdateComponents.Count > 0;
        public bool IsNeedFixUpdate => FixedUpdateComponents.Count > 0;

        public Entity()
        {
#if !NOT_UNITY
            if (this is ECSNode) { }
            //else AddComponent<GameObjectComponent>();
#endif
        }

        #region 复写部分
        public virtual void Awake()
        {

        }

        public virtual void Awake(object initData)
        {

        }

        //调整父子关系后的回调
        public virtual void OnSetParent(Entity preParent, Entity nowParent)
        {

        }

        public virtual void Update(float deltaTime)
        {

        }

        public virtual void FixedUpdate(float fixDeltaTime)
        {

        }

        /// <summary>
        /// 尽量都把要清除的数据都放在这里
        /// </summary>
        public virtual void OnDestroy()
        {

        }

        #endregion

        private void Dispose()
        {
            if (EnableLog) GameLog.Debug($"{GetType().Name}->Dispose");

            if (Children.Count > 0)
            {
                for (int i = Children.Count - 1; i >= 0; i--)
                {
                    Entity.Destroy(Children[i]);
                }
                Children.Clear();
                //Type2Children.Clear();
            }
            Parent?.RemoveChild(this);
            _parent = null;

            foreach (var component in Components.Values)
            {
                if(component.GetType() != typeof(GameObjectComponent))
                {
                    component.Enable = false;
                    Component.Destroy(component);
                }
            }
            Components.Clear();
            UpdateComponents.Clear();
            FixedUpdateComponents.Clear();

            InstanceId = 0;
            if (ECSNode.Entities.ContainsKey(GetType()))
            {
                ECSNode.Entities[GetType()].Remove(this);
            }
        }

        #region 组件

        public T GetParent<T>() where T : Entity
        {
            return _parent as T;
        }

        public T As<T>() where T : class
        {
            return this as T;
        }

        //public bool As<T>(out T entity) where T : Entity
        //{
        //    entity = this as T;
        //    return entity != null;
        //}

        public T AddComponent<T>() where T : Component
        {
            var component = PoolManager.Instance.TryGet<T>();
            component.Entity = this;
            component.IsDisposed = false;
            Components.Add(typeof(T), component);
            if (component.IsNeedFixedUpdate == true) FixedUpdateComponents.Add(component);
            if (component.IsNeedUpdate == true) UpdateComponents.Add(component);
            if (EnableLog) GameLog.Debug($"{GetType().Name}->AddComponent, {typeof(T).Name}");
            component.Awake();
//#if !NOT_UNITY
//            if(typeof(T) != typeof(GameObjectComponent))
//            {
//                GetComponent<GameObjectComponent>().OnAddComponent(component);
//            }
//#endif
            component.Enable = component.DefaultEnable;
            return component;
        }

        public T AddComponent<T>(object initData) where T : Component
        {
            var component = PoolManager.Instance.TryGet<T>();
            component.Entity = this;
            component.IsDisposed = false;
            Components.Add(typeof(T), component);
            if (component.IsNeedFixedUpdate == true) FixedUpdateComponents.Add(component);
            if (component.IsNeedUpdate == true) UpdateComponents.Add(component);
            if (EnableLog) GameLog.Debug($"{GetType().Name}->AddComponent, {typeof(T).Name} initData={initData}");
            component.Awake(initData);
//#if !NOT_UNITY
//            if (typeof(T) != typeof(GameObjectComponent))
//            {
//                GetComponent<GameObjectComponent>().OnAddComponent(component);
//            }
//#endif
            component.Enable = component.DefaultEnable;
            return component;
        }

        public void RemoveComponent<T>() where T : Component
        {
            var component = Components[typeof(T)];
            if (component.Enable) component.Enable = false;
            Component.Destroy(component);
            Components.Remove(typeof(T));
            FixedUpdateComponents.Remove(component);
            UpdateComponents.Remove(component);
//#if !NOT_UNITY
//            GetComponent<GameObjectComponent>().OnRemoveComponent(component);
//#endif
        }

        public T GetComponent<T>() where T : Component
        {
            if (Components.TryGetValue(typeof(T), out var component))
            {
                return component as T;
            }
            return null;
        }

        public bool HasComponent<T>() where T : Component
        {
            return Components.ContainsKey(typeof(T));
        }

        public bool TryGet<T>(out T component) where T : Component
        {
            if (Components.TryGetValue(typeof(T), out var c))
            {
                component = c as T;
                return true;
            }
            component = null;
            return false;
        }

        //public bool TryGet<T, T1>(out T component, out T1 component1) where T : Component  where T1 : Component
        //{
        //    component = null;
        //    component1 = null;
        //    if (Components.TryGetValue(typeof(T), out var c)) component = c as T;
        //    if (Components.TryGetValue(typeof(T1), out var c1)) component1 = c1 as T1;
        //    if (component != null && component1 != null) return true;
        //    return false;
        //}

        //public bool TryGet<T, T1, T2>(out T component, out T1 component1, out T2 component2) where T : Component where T1 : Component where T2 : Component
        //{
        //    component = null;
        //    component1 = null;
        //    component2 = null;
        //    if (Components.TryGetValue(typeof(T), out var c)) component = c as T;
        //    if (Components.TryGetValue(typeof(T1), out var c1)) component1 = c1 as T1;
        //    if (Components.TryGetValue(typeof(T2), out var c2)) component2 = c2 as T2;
        //    if (component != null && component1 != null && component2 != null) return true;
        //    return false;
        //}
        #endregion

        #region 子实体
        private void SetParent(Entity parent)
        {
            var preParent = Parent;
            preParent?.RemoveChild(this);
            this._parent = parent;
//#if !NOT_UNITY
//            if (parent.HasComponent<GameObjectComponent>() == false)
//            {
//                UnityEngine.Debug.LogError(parent.GetType().Name);
//            }
//            parent.GetComponent<GameObjectComponent>().OnAddChild(this);
//#endif
            OnSetParent(preParent, parent);
        }

        public void SetChild(Entity child)
        {
            Children.Add(child);
            //Id2Children.Add(child.Id, child);
            //if (!Type2Children.ContainsKey(child.GetType())) Type2Children.Add(child.GetType(), new List<Entity>());
            //Type2Children[child.GetType()].Add(child);
            child.SetParent(this);
        }

        public void RemoveChild(Entity child)
        {
            Children.Remove(child);
//            Id2Children.Remove(child.Id);
//            if (Type2Children.ContainsKey(child.GetType()))
//            {
//                Type2Children[child.GetType()].Remove(child);
//            }
//            else
//            {
//#if UNITY
//                UnityEngine.Debug.LogError("BigBug，为啥没有这个类型还尝试去移除");
//#endif
//            }
        }

        //非对象池里新增
        public T AddChildNoPool<T>() where T : Entity
        {
            var entity = NewEntity<T>(false);
            if (EnableLog) GameLog.Debug($"AddChild {this.GetType().Name}, {typeof(T).Name}={entity.Id}");
            SetupEntity(entity, this);
            return entity as T;
        }

        public T AddChild<T>() where T : Entity
        {
            var entity = NewEntity<T>();
            if (EnableLog) GameLog.Debug($"AddChild {this.GetType().Name}, {typeof(T).Name}={entity.Id}");
            SetupEntity(entity, this);
            return entity as T;
        }

        public T AddChild<T>(object initData) where T : Entity
        {
            var entity = NewEntity<T>();
            if (EnableLog) GameLog.Debug($"AddChild {this.GetType().Name}, {typeof(T).Name}={entity.Id}");
            SetupEntity(entity, this, initData);
            return entity as T;
        }

        //public T AddChild<T>() where T : Entity
        //{
        //    return AddChild<T>() as T;
        //}

        //public T AddChild<T>(object initData) where T : Entity
        //{
        //    return AddChild<T>(initData) as T;
        //}

        //public T AddIdChild<T>(long id) where T : Entity
        //{
        //    var entityType = typeof(T);
        //    var entity = NewEntity(entityType, id);
        //    if (EnableLog) Log.Debug($"AddChild {this.GetType().Name}, {entityType.Name}={entity.Id}");
        //    SetupEntity(entity, this);
        //    return entity as T;
        //}

        //public Entity GetIdChild(long id)
        //{
        //    Id2Children.TryGetValue(id, out var entity);
        //    return entity;
        //}

        //public T GetIdChild<T>(long id) where T : Entity
        //{
        //    Id2Children.TryGetValue(id, out var entity);
        //    return entity as T;
        //}

        //public T GetChild<T>(int index = 0) where T : Entity
        //{
        //    if (Type2Children.ContainsKey(typeof(T)) == false)
        //    {
        //        return null;
        //    }
        //    if (Type2Children[typeof(T)].Count <= index)
        //    {
        //        return null;
        //    }
        //    return Type2Children[typeof(T)][index] as T;
        //}

        //public Entity[] GetChildren()
        //{
        //    return Children.ToArray();
        //}

        //public T[] GetTypeChildren<T>() where T : Entity
        //{
        //    return Type2Children[typeof(T)].ConvertAll(x => x.As<T>()).ToArray();
        //}

        //public Entity Find(string name)
        //{
        //    foreach (var item in Children)
        //    {
        //        if (item.name == name) return item;
        //    }
        //    return null;
        //}

        //public T Find<T>(string name) where T : Entity
        //{
        //    if (Type2Children.TryGetValue(typeof(T), out var chidren))
        //    {
        //        foreach (var item in chidren)
        //        {
        //            if (item.name == name) return item as T;
        //        }
        //    }
        //    return null;
        //}
        #endregion

        #region 事件广播部分

        public T Publish<T>(T TEvent) where T : class
        {
            var eventComponent = GetComponent<EventComponent>();
            if (eventComponent == null)
            {
                return TEvent;
            }
            eventComponent.Publish(TEvent);
            return TEvent;
        }

        public void Subscribe<T>(Action<T> action) where T : class
        {
            var eventComponent = GetComponent<EventComponent>();
            if (eventComponent == null)
            {
                eventComponent = AddComponent<EventComponent>();
            }
            eventComponent.Subscribe(action);
        }

        public void UnSubscribe<T>(Action<T> action) where T : class
        {
            var eventComponent = GetComponent<EventComponent>();
            if (eventComponent != null)
            {
                eventComponent.UnSubscribe(action);
            }
        }

        //暂时关闭，暂无需求
        //public void FireEvent(string eventType)
        //{
        //    FireEvent(eventType, this);
        //}

        //public void FireEvent(string eventType, Entity entity)
        //{
        //    var eventComponent = GetComponent<EventComponent>();
        //    if (eventComponent != null)
        //    {
        //        eventComponent.FireEvent(eventType, entity);
        //    }
        //}

        //public void AddEventListener(string eventType, Action<Entity> action)
        //{
        //    var eventComponent = GetComponent<EventComponent>();
        //    if (eventComponent == null)
        //    {
        //        eventComponent = AddComponent<EventComponent>();
        //    }
        //    eventComponent.AddEventListener(eventType, action);
        //}

        //public void RemoveEventListener(string eventType, Action<Entity> action)
        //{
        //    var eventComponent = GetComponent<EventComponent>();
        //    if (eventComponent != null)
        //    {
        //        eventComponent.RemoveEventListener(eventType, action);
        //    }
        //}
        
        //重置方法
        public void Reset()
        {
        }
        #endregion
    }
}