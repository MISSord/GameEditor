using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace EGamePlay.Combat
{
    /// <summary>
    /// 高性能版本，使用预编译的委托
    /// </summary>
    public static class FastStaticExecutor
    {
        private static Dictionary<string, Delegate> _delegateCache = new Dictionary<string, Delegate>(StringComparer.OrdinalIgnoreCase);
        private static Dictionary<string, Type[]> _paramTypesCache = new Dictionary<string, Type[]>();

        /// <summary>
        /// 初始化并预编译所有方法
        /// </summary>
        public static void Initialize<T>() where T : class
        {
            var type = typeof(T);
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Static);

            foreach (var method in methods)
            {
                CompileMethod(method);
            }
        }

        /// <summary>
        /// 执行命令
        /// </summary>
        public static object Execute(string command)
        {
            if (command == string.Empty) return true;

            // 解析命令
            var parts = command.Split('#');
            string methodName = parts[0].Trim();
            string[] stringArgs = parts.Length > 1 ? parts.Skip(1).ToArray(): Array.Empty<string>();

            // 获取缓存的委托和参数类型
            if (!_delegateCache.TryGetValue(methodName, out var del) ||
                !_paramTypesCache.TryGetValue(methodName, out var paramTypes))
            {
                throw new MissingMethodException($"未找到方法: {methodName}");
            }

            // 转换参数
            object[] args = new object[paramTypes.Length];
            for (int i = 0; i < paramTypes.Length; i++)
            {
                if (i < stringArgs.Length)
                {
                    //这里会有装箱封箱情况，但考虑调用频率，暂时不处理
                    args[i] = Convert.ChangeType(stringArgs[i], paramTypes[i]);
                }
                else
                {
                    args[i] = GetDefaultValue(paramTypes[i]);
                }
            }

            // 调用委托（wrapper 内部用 Invoke，但避免重复查找方法）
            return ((Func<object[], object>)del)(args);
        }

        /// <summary>
        /// 预编译方法为委托。使用 Invoke 包装以兼容任意签名（Entity、float、bool 等），避免 CreateDelegate 对委托签名的严格匹配要求。
        /// </summary>
        private static void CompileMethod(MethodInfo method)
        {
            string methodName = method.Name;
            var parameters = method.GetParameters();
            Type[] paramTypes = parameters.Select(p => p.ParameterType).ToArray();

            _paramTypesCache[methodName] = paramTypes;

            // 统一使用 Func<object[], object> 包装，兼容任意参数/返回类型（bool、Entity 等）
            Func<object[], object> wrapper = args => method.Invoke(null, args);
            _delegateCache[methodName] = wrapper;
        }

        private static object GetDefaultValue(Type type)
        {
            return type.IsValueType ? Activator.CreateInstance(type) : null;
        }
    }
}
