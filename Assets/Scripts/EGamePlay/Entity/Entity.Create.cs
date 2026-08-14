using System;
using System.Collections.Generic;
using ACTGameEditor;

namespace EGamePlay
{
    public abstract partial class Entity
    {
        public static ECSNode ECSNode => ECSNode.Instance;
        public static bool EnableLog { get; set; } = false;

        public static Entity NewEntity<T>(bool isPool = true) where T : Entity
        { 
            var entity = isPool ? PoolManager.Instance.TryGet<T>() : Activator.CreateInstance<T>();
            entity.InstanceId = IdFactory.NewInstanceId();
            entity.Id = entity.InstanceId;
            Type entityType = typeof(T);
            if (!ECSNode.Entities.ContainsKey(entityType))
            {
                ECSNode.Entities.Add(entityType, new List<Entity>());
            }
            ECSNode.Entities[entityType].Add(entity);
            return entity;
        }

        private static void SetupEntity(Entity entity, Entity parent)
        {
            parent.SetChild(entity);
            entity.Awake();
        }

        private static void SetupEntity(Entity entity, Entity parent, object initData)
        {
            parent.SetChild(entity);
            entity.Awake(initData);
        }

        public static void Destroy<T>(T entity) where T : Entity
        {
            if (entity == null)
                return;
            if (entity.IsDisposed)
                return;
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
    }
}


//public static T CreateWithId<T>(long id) where T : Entity
//{
//    return CreateWithId(typeof(T), id) as T;
//}

//public static T CreateWithId<T>(long id, object initData) where T : Entity
//{
//    return CreateWithId(typeof(T), id, initData) as T;
//}

//private static T CreateWithParent<T>(Entity parent) where T : Entity
//{
//    return CreateWithParent(typeof(T), parent) as T;
//}

//private static T CreateWithParent<T>(Entity parent, object initData) where T : Entity
//{
//    return CreateWithParent(typeof(T), parent, initData) as T;
//}

//private static T CreateWithParentAndId<T>(Entity parent, long id) where T : Entity
//{
//    return CreateWithParentAndId(typeof(T), parent, id) as T;
//}

//private static T CreateWithParentAndId<T>(Entity parent, long id, object initData) where T : Entity
//{
//    return CreateWithParentAndId(typeof(T), parent, id, initData) as T;
//}


//public static Entity CreateWithId(Type entityType, long id)
//{
//    var entity = NewEntity(entityType);
//    entity.Id = id;
//    if (EnableLog) Log.Debug($"Create {entityType.Name}={entity.Id}");
//    SetupEntity(entity, Master);
//    return entity;
//}

//public static Entity CreateWithId(Type entityType, long id, object initData)
//{
//    var entity = NewEntity(entityType);
//    entity.Id = id;
//    if (EnableLog) Log.Debug($"Create {entityType.Name}={entity.Id}, {initData}");
//    SetupEntity(entity, Master, initData);
//    return entity;
//}

//private static Entity CreateWithParent(Type entityType, Entity parent)
//{
//    var entity = NewEntity(entityType);
//    if (EnableLog) Log.Debug($"CreateWithParent {parent.GetType().Name}, {entityType.Name}={entity.Id}");
//    SetupEntity(entity, parent);
//    return entity;
//}

//private static Entity CreateWithParentAndId(Type entityType, Entity parent, long id)
//{
//    var entity = NewEntity(entityType);
//    entity.Id = id;
//    if (EnableLog) Log.Debug($"CreateWithParent {parent.GetType().Name}, {entityType.Name}={entity.Id}");
//    SetupEntity(entity, parent);
//    return entity;
//}

//private static Entity CreateWithParent(Type entityType, Entity parent, object initData)
//{
//    var entity = NewEntity(entityType);
//    if (EnableLog) Log.Debug($"CreateWithParent {parent.GetType().Name}, {entityType.Name}={entity.Id}");
//    SetupEntity(entity, parent, initData);
//    return entity;
//}

//private static Entity CreateWithParentAndId(Type entityType, Entity parent, long id, object initData)
//{
//    var entity = NewEntity(entityType);
//    entity.Id = id;
//    if (EnableLog) Log.Debug($"CreateWithParent {parent.GetType().Name}, {entityType.Name}={entity.Id}");
//    SetupEntity(entity, parent, initData);
//    return entity;
//}
