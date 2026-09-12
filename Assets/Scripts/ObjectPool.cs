using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

/// <summary>
/// 非泛型池操作（归还、清理、计数）。
/// </summary>
public interface IPool
{
    void Return(object item);
    void Clear();
    int Count { get; }
}

/// <summary>
/// 对象池接口
/// </summary>
/// <typeparam name="T">对象类型</typeparam>
public interface IObjectPool<T> where T : class
{
    /// <summary>
    /// 从池中获取对象
    /// </summary>
    T Get();

    /// <summary>
    /// 将对象返回池中
    /// </summary>
    void Return(T item);

    /// <summary>
    /// 池中当前可用的对象数量
    /// </summary>
    int Count { get; }

    /// <summary>
    /// 池的最大容量
    /// </summary>
    int MaxSize { get; set; }
}

/// <summary>
/// 可重置对象接口
/// </summary>
public interface IResettable
{
    /// <summary>
    /// 重置对象状态
    /// </summary>
    void Reset();
}

/// <summary>
/// 泛型对象池。LIFO 栈 + HashSet 去重；满员时丢弃归还物给 GC。
/// </summary>
/// <typeparam name="T">对象类型</typeparam>
public class ObjectPool<T> : IObjectPool<T>, IPool where T : class
{
    private readonly Stack<T> _stack;
    private readonly HashSet<T> _inPoolCheck;
    private readonly Func<T> _objectGenerator;
    private readonly Action<T> _resetAction;
    private readonly Action<T> _clearAction;
    private int _maxSize;

    /// <summary>
    /// 创建对象池实例
    /// </summary>
    /// <param name="type">具体构造类型，需有无参构造</param>
    /// <param name="resetAction">非 <see cref="IResettable"/> 时的重置</param>
    /// <param name="initialSize">预创建数量，会被钳到 MaxSize</param>
    /// <param name="maxSize">池内最大闲置数，超出的归还物丢弃</param>
    public ObjectPool(Type type, Action<T> resetAction = null, int initialSize = 10, int maxSize = 100)
    {
        if (type == null)
            throw new ArgumentNullException(nameof(type));
        if (maxSize <= 0)
            throw new ArgumentException("MaxSize must be greater than 0", nameof(maxSize));
        if (initialSize < 0)
            initialSize = 0;
        if (initialSize > maxSize)
            initialSize = maxSize;

        var newExpr = Expression.New(type);
        var lambdaExpr = Expression.Lambda<Func<T>>(Expression.Convert(newExpr, typeof(T)));
        _objectGenerator = lambdaExpr.Compile();
        _resetAction = resetAction;
        _clearAction = TryCompileClear(type);
        _maxSize = maxSize;

        _stack = new Stack<T>(maxSize);
        _inPoolCheck = new HashSet<T>(maxSize);

        for (int i = 0; i < initialSize; i++)
        {
            var item = _objectGenerator();
            _stack.Push(item);
            _inPoolCheck.Add(item);
        }
    }

    /// <summary>
    /// 从池中获取对象
    /// </summary>
    public T Get()
    {
        T item;
        if (_stack.Count > 0)
        {
            item = _stack.Pop();
            _inPoolCheck.Remove(item);
        }
        else
        {
            item = _objectGenerator();
        }

        return item;
    }

    /// <summary>
    /// 将对象返回池中。重复归还忽略；满员则 Reset 后丢弃。
    /// </summary>
    public void Return(T item)
    {
        if (item == null) throw new ArgumentNullException(nameof(item));

        if (_inPoolCheck.Contains(item))
        {
            UnityEngine.Debug.LogWarning($"[ObjectPool] 试图归还一个已经在池中的对象: {item.GetType().Name}");
            return;
        }

        if (item is IResettable resettable)
            resettable.Reset();
        else
            _resetAction?.Invoke(item);
        _clearAction?.Invoke(item);

        if (_stack.Count >= _maxSize)
            return;

        _inPoolCheck.Add(item);
        _stack.Push(item);
    }

    void IPool.Return(object item)
    {
        if (item is T tItem)
            Return(tItem);
    }

    public void Clear()
    {
        _stack.Clear();
        _inPoolCheck.Clear();
    }

    /// <summary>
    /// 池中当前可用的对象数量
    /// </summary>
    public int Count => _stack.Count;

    /// <summary>
    /// 池的最大闲置容量。下调时丢掉多余闲置对象。
    /// </summary>
    public int MaxSize
    {
        get => _maxSize;
        set
        {
            if (value <= 0) throw new ArgumentException("MaxSize must be greater than 0");
            _maxSize = value;
            while (_stack.Count > _maxSize)
            {
                T dropped = _stack.Pop();
                _inPoolCheck.Remove(dropped);
            }
        }
    }

    static Action<T> TryCompileClear(Type type)
    {
        if (typeof(IResettable).IsAssignableFrom(typeof(T)))
            return null;

        MethodInfo clear = type.GetMethod("Clear", Type.EmptyTypes);
        if (clear == null || clear.ReturnType != typeof(void) || clear.IsStatic)
            return null;

        ParameterExpression param = Expression.Parameter(typeof(T), "item");
        return Expression.Lambda<Action<T>>(Expression.Call(param, clear), param).Compile();
    }
}
