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

        //组件管理
        public Dictionary<Type, Component> Components { get; set; } = new Dictionary<Type, Component>();

        //Update部分
        public List<Component> UpdateComponents { get; private set; } = new List<Component>();
        public List<Component> FixedUpdateComponents { get; private set; } = new List<Component>();
        public bool IsNeedUpdate => UpdateComponents.Count > 0;
        public bool IsNeedFixUpdate => FixedUpdateComponents.Count > 0;

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

        /// <summary>对象池归还前重置，子类需清字典/事件，避免 Awake 二次初始化冲突。</summary>
        public virtual void OnReset()
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
            }
            Parent?.RemoveChild(this);
            _parent = null;

            foreach (var component in Components.Values)
            {
                component.Enable = false;
                Component.Destroy(component);
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

        #endregion

        #region 子实体
        private void SetParent(Entity parent)
        {
            var preParent = Parent;
            preParent?.RemoveChild(this);
            this._parent = parent;
            OnSetParent(preParent, parent);
        }

        public void SetChild(Entity child)
        {
            Children.Add(child);
            child.SetParent(this);
        }

        public void RemoveChild(Entity child)
        {
            Children.Remove(child);
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

        //重置方法
        public void Reset()
        {
            OnReset();
            Name = null;
        }
        #endregion
    }
}