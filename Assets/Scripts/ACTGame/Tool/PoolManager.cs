using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;

namespace ACTGameEditor
{
    /// <summary>
    /// 对象池管理器
    /// </summary>
    public class PoolManager : Singleton<PoolManager>
    {
        private readonly ConcurrentDictionary<Type, IPool> _pools = new ConcurrentDictionary<Type, IPool>();

        /// <summary>
        /// 获取或创建指定类型的对象池
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="factory">对象创建函数</param>
        /// <param name="resetAction">对象重置操作</param>
        /// <param name="initialSize">初始对象数量</param>
        /// <param name="maxSize">最大对象数量</param>
        public IObjectPool<T> GetPool<T>(Type type, Action<T> resetAction = null, int initialSize = 10, int maxSize = 100) where T : class
        {

            return (IObjectPool<T>)_pools.GetOrAdd(type, _ => new ObjectPool<T>(type, resetAction, initialSize, maxSize));
        }

        /// <summary>
        /// 获取指定类型的对象池（如果存在）
        /// </summary>
        public IObjectPool<T> GetPool<T>() where T : class
        {
            Type type = typeof(T);
            if (_pools.TryGetValue(type, out IPool pool))
            {
                return (IObjectPool<T>)pool;
            }

            return null;
        }

        ///// <summary>
        ///// 从对象池中获取对象
        ///// </summary>
        //public T Get<T>() where T : class
        //{
        //    var pool = GetPool<T>();
        //    if (pool == null)
        //    {
        //        throw new InvalidOperationException($"No pool registered for type {typeof(T).Name}");
        //    }

        //    return pool.Get();
        //}

        public T TryGet<T>() where T : class
        {
            var pool = GetPool<T>();
            if (pool == null)
            {
                pool = GetPool<T>(typeof(T), null, 2, 100);
            }
            return pool.Get();
        }

        /// <summary>
        /// 将对象返回对象池
        /// </summary>
        public void Return<T>(object item) where T : class
        {
            var pool = GetPool<T>();
            if (pool == null)
            {
                Console.WriteLine($"Warning: No pool registered for type {typeof(T).Name}");
            }

            pool.Return(item as T);
        }

        /// <summary>
        /// 将对象返回对象池
        /// </summary>
        /// <param name="item">要归还的对象</param>
        public void Return(object item)
        {
            if (item == null) return;

            Type type = item.GetType();
            if (_pools.TryGetValue(type, out IPool pool))
            {
                pool.Return(item);
            }
            else
            {
                Console.WriteLine($"Warning: No pool registered for type {type.Name}");
            }
        }

        /// <summary>
        /// 清除所有对象池
        /// </summary>
        public void ClearAll()
        {
            _pools.Clear();
        }

        /// <summary>
        /// 获取所有对象池的统计信息
        /// </summary>
        public Dictionary<Type, int> GetPoolStats()
        {
            var stats = new Dictionary<Type, int>();

            foreach (var kvp in _pools)
            {
                var pool = kvp.Value as IObjectPool<object>;
                if (pool != null)
                {
                    stats[kvp.Key] = pool.Count;
                }
            }

            return stats;
        }
    }
}