using System;
using System.Collections.Generic;

namespace EGamePlay
{
	/// <summary>
	/// 定时器槽位。不是 Entity，由 <see cref="ETTimerManager"/> 内部池化。
	/// </summary>
	public sealed class TimerSlot
	{
		public long Id { get; internal set; }
		public bool IsRepeated { get; internal set; }

		internal bool Alive;
		internal long StartTime;
		internal long RepeatedTime;
		internal long TillTime;
		internal int Count;
		internal Action OnceCallback;
		internal Action<bool> RepeatCallback;

		internal void Reset()
		{
			Id = 0;
			IsRepeated = false;
			Alive = false;
			StartTime = 0;
			RepeatedTime = 0;
			TillTime = 0;
			Count = 0;
			OnceCallback = null;
			RepeatCallback = null;
		}
	}

	/// <summary>
	/// 集中调度一次性/周期定时器。时间轴走战斗世界钟（<see cref="GameTimeManager.WorldDelta"/>），
	/// 暂停与时空断裂会冻结或拉长；不乘实体 TimeScale，单体减速/冻结不影响到期。
	/// </summary>
	public class ETTimerManager : Entity
	{
		public static ETTimerManager Instance { get; set; }

		const int InitialPoolSize = 32;

		readonly Dictionary<long, TimerSlot> _timers = new Dictionary<long, TimerSlot>(64);
		readonly Stack<TimerSlot> _slotPool = new Stack<TimerSlot>(InitialPoolSize);
		readonly MultiMap<long, long> _timeId = new MultiMap<long, long>();
		readonly Queue<long> _timeOutTime = new Queue<long>();
		readonly Queue<long> _timeOutTimerIds = new Queue<long>();
		readonly List<long> _removeScratch = new List<long>(16);

		/// <summary>累计世界毫秒（double 避免每帧截断）。</summary>
		double _nowMs;
		long _minTime = long.MaxValue;

		/// <summary>当前战斗世界钟（毫秒）。无实例时为 0。</summary>
		public static long NowMs => Instance != null ? (long)Instance._nowMs : 0L;

		/// <summary>当前战斗世界钟（毫秒）。</summary>
		public long Now => (long)_nowMs;

		public override void Awake()
		{
			Instance = this;
			_nowMs = 0d;
			_minTime = long.MaxValue;
			for (int i = 0; i < InitialPoolSize; i++)
				_slotPool.Push(new TimerSlot());
		}

		public override void OnDestroy()
		{
			if (Instance == this)
				Instance = null;

			_removeScratch.Clear();
			foreach (var id in _timers.Keys)
				_removeScratch.Add(id);
			for (int i = 0; i < _removeScratch.Count; i++)
				Remove(_removeScratch[i]);
			_removeScratch.Clear();
			_timers.Clear();
			_timeId.Clear();
			_timeOutTime.Clear();
			_timeOutTimerIds.Clear();
			_slotPool.Clear();
		}

		/// <summary>
		/// 用本帧世界 delta 推进时钟并触发到期定时器。delta≤0（暂停）时只冻结，不回调。
		/// </summary>
		public new void Update(float worldDelta)
		{
			if (worldDelta > 0f)
				_nowMs += (double)worldDelta * 1000.0;

			if (_timeId.Count == 0)
			{
				_minTime = long.MaxValue;
				return;
			}

			long timeNow = (long)_nowMs;
			if (timeNow < _minTime)
				return;

			foreach (KeyValuePair<long, List<long>> kv in _timeId.GetDictionary())
			{
				long k = kv.Key;
				if (k > timeNow)
				{
					_minTime = k;
					break;
				}
				_timeOutTime.Enqueue(k);
			}

			while (_timeOutTime.Count > 0)
			{
				long time = _timeOutTime.Dequeue();
				List<long> ids = _timeId[time];
				if (ids != null)
				{
					for (int i = 0; i < ids.Count; i++)
						_timeOutTimerIds.Enqueue(ids[i]);
				}
				_timeId.Remove(time);
			}

			while (_timeOutTimerIds.Count > 0)
			{
				long timerId = _timeOutTimerIds.Dequeue();
				if (!_timers.TryGetValue(timerId, out TimerSlot slot) || !slot.Alive)
					continue;

				if (slot.IsRepeated)
					FireRepeated(slot);
				else
					FireOnce(slot);
			}
		}

		/// <summary>
		/// 创建一个周期定时器。间隔必须 ≥ 30ms（世界毫秒）。
		/// </summary>
		public long NewRepeatedTimer(long time, Action<bool> action)
		{
			if (time < 30)
				throw new Exception("repeated time < 30");

			long startTime = Now;
			long tillTime = startTime + time;
			TimerSlot slot = Rent();
			slot.IsRepeated = true;
			slot.RepeatCallback = action;
			slot.StartTime = startTime;
			slot.RepeatedTime = time;
			slot.Count = 1;
			slot.TillTime = tillTime;
			_timers[slot.Id] = slot;
			AddToTimeId(tillTime, slot.Id);
			return slot.Id;
		}

		/// <summary>按 Id 取周期定时器；不存在或不是周期则返回 null。</summary>
		public TimerSlot GetRepeatedTimer(long id)
		{
			if (_timers.TryGetValue(id, out TimerSlot slot) && slot.Alive && slot.IsRepeated)
				return slot;
			return null;
		}

		/// <summary>移除定时器并归还槽位。已到期被回收的 Id 再调是空操作。</summary>
		public void Remove(long id)
		{
			if (id == 0)
				return;
			if (!_timers.TryGetValue(id, out TimerSlot slot) || !slot.Alive)
				return;

			long tillTime = slot.TillTime;
			Recycle(slot);
			if (tillTime != 0)
				_timeId.Remove(tillTime, id);
		}

		/// <summary>
		/// 创建一个一次性定时器，在 tillTime（世界毫秒戳）触发。
		/// </summary>
		public long NewOnceTimer(long tillTime, Action action)
		{
			TimerSlot slot = Rent();
			slot.IsRepeated = false;
			slot.OnceCallback = action;
			slot.TillTime = tillTime;
			_timers[slot.Id] = slot;
			AddToTimeId(tillTime, slot.Id);
			return slot.Id;
		}

		/// <summary>
		/// 创建一个一次性定时器，在 delayMs 世界毫秒后触发。
		/// </summary>
		public long NewOnceTimerAfter(long delayMs, Action action)
		{
			if (delayMs < 0L)
				delayMs = 0L;
			return NewOnceTimer(Now + delayMs, action);
		}

		/// <summary>按 Id 取一次性定时器；不存在或不是一次性则返回 null。</summary>
		public TimerSlot GetOnceTimer(long id)
		{
			if (_timers.TryGetValue(id, out TimerSlot slot) && slot.Alive && !slot.IsRepeated)
				return slot;
			return null;
		}

		/// <summary>将定时器挂到指定世界毫秒戳。业务侧请用 <see cref="Now"/> 或 <see cref="NewOnceTimerAfter"/>，不要用墙钟。</summary>
		public void AddToTimeId(long tillTime, long id)
		{
			_timeId.Add(tillTime, id);
			if (tillTime < _minTime)
				_minTime = tillTime;
		}

		TimerSlot Rent()
		{
			TimerSlot slot = _slotPool.Count > 0 ? _slotPool.Pop() : new TimerSlot();
			slot.Alive = true;
			slot.Id = IdFactory.NewInstanceId();
			return slot;
		}

		void Recycle(TimerSlot slot)
		{
			if (slot == null || !slot.Alive)
				return;
			slot.Alive = false;
			_timers.Remove(slot.Id);
			slot.Reset();
			_slotPool.Push(slot);
		}

		void FireOnce(TimerSlot slot)
		{
			long id = slot.Id;
			Action callback = slot.OnceCallback;
			try
			{
				callback?.Invoke();
			}
			catch (Exception e)
			{
				GameLog.Error(e);
			}

			if (slot.Alive && slot.Id == id)
				Recycle(slot);
		}

		void FireRepeated(TimerSlot slot)
		{
			long id = slot.Id;
			try
			{
				slot.RepeatCallback?.Invoke(true);
			}
			catch (Exception e)
			{
				GameLog.Error(e);
			}

			if (!slot.Alive || slot.Id != id)
				return;

			slot.Count++;
			long tillTime = slot.StartTime + slot.RepeatedTime * slot.Count;
			slot.TillTime = tillTime;
			AddToTimeId(tillTime, slot.Id);
		}
	}
}
