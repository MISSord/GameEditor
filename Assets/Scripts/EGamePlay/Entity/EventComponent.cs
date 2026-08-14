using System;
using System.Collections.Generic;

namespace EGamePlay
{
    public sealed class EventComponent : Component
    {
        public override bool DefaultEnable { get; set; } = false;
        private Dictionary<Type, List<object>> TypeEvent2ActionLists = new Dictionary<Type, List<object>>();
        //private Dictionary<string, List<object>> FireEvent2ActionLists = new Dictionary<string, List<object>>(); //这个暂时关闭

        //这个更多是只关心某种类型的，自带参数类型转换，只能传递一个变量
        //这里传递更多的是非Entity类型
        public T Publish<T>(T TEvent) where T : class
        {
            if (TypeEvent2ActionLists.TryGetValue(typeof(T), out var actionList))
            {
                var tempList = actionList.ToArray();
                foreach (Action<T> action in tempList)
                {
                    action.Invoke(TEvent);
                }
            }
            return TEvent;
        }

        public void Subscribe<T>(Action<T> action) where T : class
        {
            var type = typeof(T);
            if (!TypeEvent2ActionLists.TryGetValue(type, out var actionList))
            {
                actionList = new List<object>();
                TypeEvent2ActionLists.Add(type, actionList);
            }
            actionList.Add(action);
        }

        public void UnSubscribe<T>(Action<T> action) where T : class
        {
            if (TypeEvent2ActionLists.TryGetValue(typeof(T), out var actionList))
            {
                actionList.Remove(action);
            }
        }
    }

}