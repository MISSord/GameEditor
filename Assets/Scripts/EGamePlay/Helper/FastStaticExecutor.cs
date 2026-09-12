using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;

namespace EGamePlay.Combat
{
    /// <summary>
    /// 表驱动静态方法：启动时编译表达式委托，命令字符串只解析一次。
    /// </summary>
    public static class FastStaticExecutor
    {
        sealed class CompiledMethod
        {
            public Func<object[], object> Invoke;
            public ParameterInfo[] Parameters;
        }

        sealed class BoundCommand
        {
            public CompiledMethod Method;
            public object[] ArgsScratch;
            public int ContextArgIndex;
        }

        static readonly Dictionary<string, CompiledMethod> Methods =
            new Dictionary<string, CompiledMethod>(StringComparer.OrdinalIgnoreCase);

        static readonly Dictionary<string, BoundCommand> Commands =
            new Dictionary<string, BoundCommand>(StringComparer.Ordinal);

        /// <summary>
        /// 预编译类型上所有 public static 方法。
        /// </summary>
        public static void Initialize<T>() where T : class
        {
            MethodInfo[] methods = typeof(T).GetMethods(BindingFlags.Public | BindingFlags.Static);
            for (int i = 0; i < methods.Length; i++)
                CompileMethod(methods[i]);
        }

        /// <summary>
        /// 执行命令。格式 <c>Method#arg1#arg2</c>。第一个非 string 的引用类型参数填 <paramref name="context"/>（通常是施法者 Entity）。
        /// </summary>
        public static object Execute(string command, object context = null)
        {
            if (string.IsNullOrEmpty(command))
                return true;

            if (!Commands.TryGetValue(command, out BoundCommand bound))
            {
                bound = BindCommand(command);
                Commands[command] = bound;
            }

            object[] args = bound.ArgsScratch;
            int ctxIndex = bound.ContextArgIndex;
            if (ctxIndex >= 0)
                args[ctxIndex] = context;

            object result = bound.Method.Invoke(args);

            if (ctxIndex >= 0)
                args[ctxIndex] = null;

            return result;
        }

        static BoundCommand BindCommand(string command)
        {
            int hash = command.IndexOf('#');
            string methodName = hash < 0 ? command.Trim() : command.Substring(0, hash).Trim();
            if (!Methods.TryGetValue(methodName, out CompiledMethod method))
                throw new MissingMethodException($"未找到方法: {methodName}");

            ParameterInfo[] parameters = method.Parameters;
            object[] args = new object[parameters.Length];
            int contextIndex = -1;
            int start = hash < 0 ? command.Length : hash + 1;

            for (int i = 0; i < parameters.Length; i++)
            {
                Type paramType = parameters[i].ParameterType;
                if (contextIndex < 0 && IsContextParameter(paramType))
                {
                    contextIndex = i;
                    args[i] = null;
                    continue;
                }

                if (!TryReadArg(command, ref start, out string raw))
                {
                    args[i] = paramType.IsValueType ? Activator.CreateInstance(paramType) : null;
                    continue;
                }

                args[i] = ConvertArg(raw, paramType);
            }

            return new BoundCommand
            {
                Method = method,
                ArgsScratch = args,
                ContextArgIndex = contextIndex,
            };
        }

        static bool IsContextParameter(Type paramType)
        {
            return !paramType.IsValueType && paramType != typeof(string);
        }

        static bool TryReadArg(string command, ref int start, out string raw)
        {
            raw = null;
            if (start >= command.Length)
                return false;
            int end = command.IndexOf('#', start);
            if (end < 0)
                end = command.Length;
            raw = command.Substring(start, end - start).Trim();
            start = end + 1;
            return true;
        }

        static object ConvertArg(string raw, Type type)
        {
            if (type == typeof(string))
                return raw;
            if (type.IsEnum)
                return Enum.Parse(type, raw, true);
            return Convert.ChangeType(raw, type, CultureInfo.InvariantCulture);
        }

        static void CompileMethod(MethodInfo method)
        {
            ParameterInfo[] parameters = method.GetParameters();
            ParameterExpression argsParam = Expression.Parameter(typeof(object[]), "args");
            Expression[] callArgs = new Expression[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                Expression indexed = Expression.ArrayIndex(argsParam, Expression.Constant(i));
                callArgs[i] = Expression.Convert(indexed, parameters[i].ParameterType);
            }

            Expression call = Expression.Call(method, callArgs);
            Expression body;
            if (method.ReturnType == typeof(void))
                body = Expression.Block(call, Expression.Constant(null, typeof(object)));
            else if (method.ReturnType.IsValueType)
                body = Expression.Convert(call, typeof(object));
            else
                body = call;

            Func<object[], object> invoke = Expression.Lambda<Func<object[], object>>(body, argsParam).Compile();
            Methods[method.Name] = new CompiledMethod
            {
                Invoke = invoke,
                Parameters = parameters,
            };
        }
    }
}
