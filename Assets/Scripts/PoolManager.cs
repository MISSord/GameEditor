using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 对象池管理器。战斗主线程单例，不用 ConcurrentDictionary。
/// </summary>
public class PoolManager : Singleton<PoolManager>
{
    private readonly Dictionary<Type, IPool> _pools = new Dictionary<Type, IPool>();

    /// <summary>
    /// 获取或创建指定类型的对象池
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="type">具体构造类型</param>
    /// <param name="resetAction">对象重置操作</param>
    /// <param name="initialSize">初始对象数量</param>
    /// <param name="maxSize">最大闲置数量</param>
    public IObjectPool<T> GetPool<T>(Type type, Action<T> resetAction = null, int initialSize = 10, int maxSize = 100) where T : class
    {
        if (_pools.TryGetValue(type, out IPool existing))
            return (IObjectPool<T>)existing;

        var created = new ObjectPool<T>(type, resetAction, initialSize, maxSize);
        _pools[type] = created;
        return created;
    }

    /// <summary>
    /// 获取指定类型的对象池（如果存在）
    /// </summary>
    public IObjectPool<T> GetPool<T>() where T : class
    {
        if (_pools.TryGetValue(typeof(T), out IPool pool))
            return (IObjectPool<T>)pool;
        return null;
    }

    /// <summary>从池取出；没有对应池时按初始 2 / 上限 100 创建。</summary>
    public T TryGet<T>() where T : class
    {
        var pool = GetPool<T>();
        if (pool == null)
            pool = GetPool<T>(typeof(T), null, 2, 100);
        return pool.Get();
    }

    /// <summary>
    /// 将对象返回对象池。无对应池时只打日志，不抛空引用。
    /// </summary>
    public void Return<T>(object item) where T : class
    {
        if (item == null)
            return;

        var pool = GetPool<T>();
        if (pool == null)
        {
            Debug.LogWarning($"[PoolManager] 没有为类型 {typeof(T).Name} 注册对象池");
            return;
        }

        T typed = item as T;
        if (typed == null)
        {
            Debug.LogWarning($"[PoolManager] 归还类型不匹配: {item.GetType().Name} -> {typeof(T).Name}");
            return;
        }

        pool.Return(typed);
    }

    /// <summary>
    /// 将对象返回对象池
    /// </summary>
    /// <param name="item">要归还的对象</param>
    public void Return(object item)
    {
        if (item == null)
            return;

        Type type = item.GetType();
        if (_pools.TryGetValue(type, out IPool pool))
        {
            pool.Return(item);
            return;
        }

        Debug.LogWarning($"[PoolManager] 没有为类型 {type.Name} 注册对象池");
    }

    /// <summary>
    /// 清除所有对象池
    /// </summary>
    public void ClearAll()
    {
        foreach (var pool in _pools.Values)
            pool.Clear();
        _pools.Clear();
    }

    /// <summary>
    /// 获取所有对象池的闲置数量
    /// </summary>
    public Dictionary<Type, int> GetPoolStats()
    {
        var stats = new Dictionary<Type, int>(_pools.Count);
        foreach (var kvp in _pools)
            stats[kvp.Key] = kvp.Value.Count;
        return stats;
    }
}
