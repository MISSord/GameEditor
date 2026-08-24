using System;

namespace EGamePlay
{
    public abstract class Component : IResettable
    {
        public Entity Entity { get; set; }
        public bool IsDisposed { get; set; }
        public virtual bool DefaultEnable { get; set; } = true;
        public virtual bool IsNeedUpdate { get; protected set; } = false;
        public virtual bool IsNeedFixedUpdate { get; protected set; } = false;

        private bool _enable = false;

        public bool Enable
        {
            set
            {
                if (_enable == value) return;
                _enable = value;
                if (_enable) OnEnable();
                else OnDisable();
            }
            get
            {
                return _enable;
            }
        }

        public T GetEntity<T>() where T : Entity
        {
            return Entity as T;
        }

        #region 复写方法
        public virtual void Awake()
        {

        }

        //其实这里也不是很好，会有装箱拆箱的问题，不过为了泛用性算了，而且加组件的频率也不会很高
        public virtual void Awake(object initData)
        {

        }

        public virtual void OnEnable()
        {

        }

        public virtual void OnDisable()
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

        /// <summary>
        /// 重置数据方法 理想情况下应该都写在OnDestroy，这里在做一层保险
        /// </summary>
        public virtual void OnReset()
        {

        }

        #endregion

        private void Dispose()
        {
            if (Entity.EnableLog) GameLog.Debug($"{GetType().Name}->Dispose");
            Entity = null;
            Enable = false;
            IsDisposed = true;
        }

        public static void Destroy<T>(T entity) where T : Component
        {
            try
            {
                entity.OnDestroy();
            }
            catch (Exception e)
            {
                GameLog.Error(e);
            }
            entity.Dispose();
            PoolManager.Instance.Return(entity);
        }

        public void Reset()
        {
            this.OnReset();
        }

        //事件广播 //Entity自带，还是少用这里的
        //public T Publish<T>(T TEvent) where T : class
        //{
        //    Entity.Publish(TEvent);
        //    return TEvent;
        //}

        //public void Subscribe<T>(Action<T> action) where T : class
        //{
        //    Entity.Subscribe(action);
        //}

        //public void UnSubscribe<T>(Action<T> action) where T : class
        //{
        //    Entity.UnSubscribe(action);
        //}
    }
}