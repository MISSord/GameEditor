using EGamePlay;
using System;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Collections.Generic;

namespace ACTGameEditor
{
    // 1. 定义一个非泛型接口，用于通用操作（归还、清理）
    public interface IPool
    {
        void Return(object item);
        void Clear();
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
    /// 泛型对象池实现
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    public class ObjectPool<T> : IObjectPool<T>, IPool where T : class
    {
        // 主存储：栈（LIFO，利用缓存局部性）
        private readonly Stack<T> _stack;
        // 辅助存储：哈希集（用于 O(1) 快速检查对象是否已在池中，防止重复归还）
        private readonly HashSet<T> _inPoolCheck;
        //创建方法
        private readonly Func<T> _objectGenerator;
        //重置方法
        private readonly Action<T> _resetAction;
        //最大容量
        private int _maxSize;

        /// <summary>
        /// 创建对象池实例
        /// </summary>
        /// <param name="objectGenerator">对象创建函数</param>
        /// <param name="resetAction">对象重置操作</param>
        /// <param name="onGetAction">获取对象时的操作</param>
        /// <param name="initialSize">初始对象数量</param>
        /// <param name="maxSize">最大对象数量</param>
        public ObjectPool(Type _type, Action<T> resetAction = null, int initialSize = 10, int maxSize = 100)
        {
            //_objectGenerator = objectGenerator ?? throw new ArgumentNullException(nameof(objectGenerator));

            // 使用表达式树编译一个创建对象的委托
            // 相当于： () => new T();
            var newExpr = Expression.New(_type);
            var lambdaExpr = Expression.Lambda<Func<T>>(Expression.Convert(newExpr, typeof(T)));
            _objectGenerator = lambdaExpr.Compile();
            _resetAction = resetAction;
            _maxSize = maxSize;

            _stack = new Stack<T>(maxSize);
            _inPoolCheck = new HashSet<T>(maxSize); // 初始化 HashSet

            // 预创建对象
            for (int i = 0; i < initialSize; i++)
            {
                var item = _objectGenerator();
                _stack.Push(item);
                _inPoolCheck.Add(item); // 记录到 HashSet
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
                _inPoolCheck.Remove(item); // 从检查集中移除
            }
            else
            {
                // 池空了，创建新对象
                item = _objectGenerator();
            }

            return item;
        }

        /// <summary>
        /// 将对象返回池中
        /// </summary>
        public void Return(T item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));

            // 1.重置对象状态
            if (item is IResettable resettable)
            {
                resettable.Reset();
            }
            else
            {
                _resetAction?.Invoke(item);
            }

            // 如果池未满，将对象放回池中
            // 2.使用 HashSet 进行 O(1) 的快速去重检查
            // Add方法如果返回 false，说明元素已存在
            if (_inPoolCheck.Add(item))
            {
                _stack.Push(item);
            }
            else
            {
                UnityEngine.Debug.LogWarning($"[ObjectPool] 试图归还一个已经在池中的对象: {item.GetType().Name}");
            }
            // 否则，对象将被丢弃，由GC回收
        }

        // 显式实现接口，处理 object 类型的归还
        void IPool.Return(object item)
        {
            if (item is T tItem)
            {
                Return(tItem);
            }
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
        /// 池的最大容量
        /// </summary>
        public int MaxSize
        {
            get => _maxSize;
            set
            {
                if (value <= 0) throw new ArgumentException("MaxSize must be greater than 0");
                _maxSize = value;
            }
        }
    }
}