using System;
using System.Collections.Generic;

namespace EGamePlay
{
    public sealed class EventComponent : Component
    {
        public override bool DefaultEnable { get; set; } = false;
        readonly Dictionary<Type, List<Delegate>> _typeEvent2ActionLists = new Dictionary<Type, List<Delegate>>();
        readonly List<List<Delegate>> _scratchPool = new List<List<Delegate>>(2);

        /// <summary>广播。先拷到 scratch 再调，允许回调里退订；嵌套广播从池里再租一块。</summary>
        public T Publish<T>(T TEvent) where T : class
        {
            if (!_typeEvent2ActionLists.TryGetValue(typeof(T), out var actionList) || actionList.Count == 0)
                return TEvent;

            List<Delegate> scratch = RentScratch();
            for (int i = 0; i < actionList.Count; i++)
                scratch.Add(actionList[i]);

            try
            {
                int n = scratch.Count;
                for (int i = 0; i < n; i++)
                {
                    if (scratch[i] is Action<T> action)
                        action.Invoke(TEvent);
                }
            }
            finally
            {
                ReturnScratch(scratch);
            }

            return TEvent;
        }

        public void Subscribe<T>(Action<T> action) where T : class
        {
            if (action == null)
                return;
            var type = typeof(T);
            if (!_typeEvent2ActionLists.TryGetValue(type, out var actionList))
            {
                actionList = new List<Delegate>(4);
                _typeEvent2ActionLists.Add(type, actionList);
            }
            actionList.Add(action);
        }

        public void UnSubscribe<T>(Action<T> action) where T : class
        {
            if (action == null)
                return;
            if (_typeEvent2ActionLists.TryGetValue(typeof(T), out var actionList))
                actionList.Remove(action);
        }

        public override void OnDestroy()
        {
            ClearAllSubscriptions();
        }

        void ClearAllSubscriptions()
        {
            foreach (var list in _typeEvent2ActionLists.Values)
                list?.Clear();
            _typeEvent2ActionLists.Clear();
            for (int i = 0; i < _scratchPool.Count; i++)
                _scratchPool[i].Clear();
        }

        List<Delegate> RentScratch()
        {
            int last = _scratchPool.Count - 1;
            if (last >= 0)
            {
                List<Delegate> scratch = _scratchPool[last];
                _scratchPool.RemoveAt(last);
                return scratch;
            }
            return new List<Delegate>(8);
        }

        void ReturnScratch(List<Delegate> scratch)
        {
            scratch.Clear();
            _scratchPool.Add(scratch);
        }
    }
}
