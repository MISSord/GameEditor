using System.Collections.Generic;
using System.Linq;

namespace ET
{
	public class MultiMap<T, K>
	{
		private readonly SortedDictionary<T, List<K>> _dictionary = new SortedDictionary<T, List<K>>();

		// 重用list
		private readonly Queue<List<K>> _queue = new Queue<List<K>>();

		public SortedDictionary<T, List<K>> GetDictionary()
		{
			return this._dictionary;
		}

		public void Add(T t, K k)
		{
			List<K> list;
			this._dictionary.TryGetValue(t, out list);
			if (list == null)
			{
				list = this.FetchList();
				this._dictionary[t] = list;
			}
			list.Add(k);
		}

		public KeyValuePair<T, List<K>> First()
		{
			return this._dictionary.First();
		}

		public T FirstKey()
		{
			return this._dictionary.Keys.First();
		}

		public int Count
		{
			get
			{
				return this._dictionary.Count;
			}
		}

		private List<K> FetchList()
		{
			if (this._queue.Count > 0)
			{
				List<K> list = this._queue.Dequeue();
				list.Clear();
				return list;
			}
			return new List<K>();
		}

		private void RecycleList(List<K> list)
		{
			// 防止暴涨
			if (this._queue.Count > 100)
			{
				return;
			}
			list.Clear();
			this._queue.Enqueue(list);
		}

		public bool Remove(T t, K k)
		{
			List<K> list;
			this._dictionary.TryGetValue(t, out list);
			if (list == null)
			{
				return false;
			}
			if (!list.Remove(k))
			{
				return false;
			}
			if (list.Count == 0)
			{
				this.RecycleList(list);
				this._dictionary.Remove(t);
			}
			return true;
		}

		public bool Remove(T t)
		{
			List<K> list = null;
			this._dictionary.TryGetValue(t, out list);
			if (list != null)
			{
				this.RecycleList(list);
			}
			return this._dictionary.Remove(t);
		}

		/// <summary>
		/// 不返回内部的list,copy一份出来
		/// </summary>
		/// <param name="t"></param>
		/// <returns></returns>
		public K[] GetAll(T t)
		{
			List<K> list;
			this._dictionary.TryGetValue(t, out list);
			if (list == null)
			{
				return new K[0];
			}
			return list.ToArray();
		}

		/// <summary>
		/// 返回内部的list
		/// </summary>
		/// <param name="t"></param>
		/// <returns></returns>
		public List<K> this[T t]
		{
			get
			{
				List<K> list;
				this._dictionary.TryGetValue(t, out list);
				return list;
			}
		}

		public K GetOne(T t)
		{
			List<K> list;
			this._dictionary.TryGetValue(t, out list);
			if (list != null && list.Count > 0)
			{
				return list[0];
			}
			return default(K);
		}

		public bool Contains(T t, K k)
		{
			List<K> list;
			this._dictionary.TryGetValue(t, out list);
			if (list == null)
			{
				return false;
			}
			return list.Contains(k);
		}

		public bool ContainsKey(T t)
		{
			return this._dictionary.ContainsKey(t);
		}

		public void Clear()
		{
			_dictionary.Clear();
		}
	}
}