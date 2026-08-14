using System;
using System.Collections.Generic;
using System.Threading;
using EGamePlay;

namespace ET
{
	public interface ITimer
	{
		void Run(bool isTimeout);
	}

	public class OnceWaitTimer: Entity, ITimer
	{
		public ETTaskCompletionSource<bool> Callback { get; set; }

        public override void Awake(object initData)
        {
			Callback = initData as ETTaskCompletionSource<bool>;
		}

        public void Run(bool isTimeout)
		{
			ETTaskCompletionSource<bool> tcs = this.Callback;
			this.GetParent<ETTimerManager>().Remove(this.Id);
			tcs.SetResult(isTimeout);
		}
	}

	public class OnceTimer: Entity, ITimer
	{
		public Action<bool> Callback { get; set; }

		public override void Awake(object initData)
		{
			Callback = initData as Action<bool>;
		}

        public void Run(bool isTimeout)
        {
            try
            {
                this.Callback.Invoke(isTimeout);
            }
            catch (Exception e)
            {
                GameLog.Error(e);
            }
        }
	}

	public class RepeatedTimerAwakeData
	{
		public long RepeatedTime;
		public Action<bool> Callback;
    }

	public class RepeatedTimer: Entity, ITimer
	{
		public override void Awake(object initData)
		{
			var awakeData = initData as RepeatedTimerAwakeData;
			this.StartTime = TimeHelper.ClientNow();
			this.RepeatedTime = awakeData.RepeatedTime;
			this.Callback = awakeData.Callback;
			this.Count = 1;
		}

		private long StartTime { get; set; }
		
		private long RepeatedTime { get; set; }

		// 下次一是第几次触发
		private int Count { get; set; }
		
		public Action<bool> Callback { private get; set; }
		
        public void Run(bool isTimeout)
        {
            ++this.Count;
            ETTimerManager timerComponent = this.GetParent<ETTimerManager>();
            long tillTime = this.StartTime + this.RepeatedTime * this.Count;
            timerComponent.AddToTimeId(tillTime, this.Id);

            try
            {
                this.Callback?.Invoke(isTimeout);
            }
            catch (Exception e)
            {
                GameLog.Error(e);
            }
        }

		public override void OnDestroy()
		{
			if (this.IsDisposed)
			{
				return;
			}
			
			long id = this.Id;

            if (id == 0)
            {
                GameLog.Error("RepeatedTimer可能多次释放了");
                return;
            }
			
			//base.Dispose();

			this.StartTime = 0;
			this.RepeatedTime = 0;
			this.Callback = null;
			this.Count = 0;
		}
	}

	public class ETTimerManager : Entity
	{
		public static ETTimerManager Instance { get; set; }
		
		private readonly Dictionary<long, ITimer> _timers = new Dictionary<long, ITimer>();

		/// <summary>
		/// key: time, value: timer id
		/// </summary>
		public readonly MultiMap<long, long> TimeId = new MultiMap<long, long>();

		private readonly Queue<long> _timeOutTime = new Queue<long>();
		
		private readonly Queue<long> _timeOutTimerIds = new Queue<long>();

		// 记录最小时间，不用每次都去MultiMap取第一个值
		private long _minTime;

        public override void Awake()
        {
			Instance = this;
        }

        public new void Update(float fixDeltaTime)
		{
			if (this.TimeId.Count == 0)
			{
				return;
			}

			long timeNow = TimeHelper.ClientNow();

			if (timeNow < this._minTime)
			{
				return;
			}
			
			foreach (KeyValuePair<long, List<long>> kv in this.TimeId.GetDictionary())
			{
				long k = kv.Key;
				if (k > timeNow)
				{
					_minTime = k;
					break;
				}
				this._timeOutTime.Enqueue(k);
			}

			while(this._timeOutTime.Count > 0)
			{
				long time = this._timeOutTime.Dequeue();
				foreach(long timerId in this.TimeId[time])
				{
					this._timeOutTimerIds.Enqueue(timerId);	
				}
				this.TimeId.Remove(time);
			}

			while(this._timeOutTimerIds.Count > 0)
			{
				long timerId = this._timeOutTimerIds.Dequeue();
				ITimer timer;
				if (!this._timers.TryGetValue(timerId, out timer))
				{
					continue;
				}
				
				timer.Run(true);
			}
		}

		public async ETTask<bool> WaitTillAsync(long tillTime, ETCancellationToken cancellationToken)
		{
			if (TimeHelper.ClientNow() > tillTime)
			{
				return true;
			}
			ETTaskCompletionSource<bool> tcs = new ETTaskCompletionSource<bool>();
			OnceWaitTimer timer = this.AddChild<OnceWaitTimer>(tcs);
			this._timers[timer.Id] = timer;
			AddToTimeId(tillTime, timer.Id);
			
			long instanceId = timer.InstanceId;
			cancellationToken.Register(() =>
			{
				if (instanceId != timer.InstanceId)
				{
					return;
				}
				
				timer.Run(false);
				
				this.Remove(timer.Id);
			});
			return await tcs.Task;
		}

		public async ETTask<bool> WaitTillAsync(long tillTime)
		{
			if (TimeHelper.ClientNow() > tillTime)
			{
				return true;
			}
			ETTaskCompletionSource<bool> tcs = new ETTaskCompletionSource<bool>();
			OnceWaitTimer timer = this.AddChild<OnceWaitTimer>(tcs);
			this._timers[timer.Id] = timer;
			AddToTimeId(tillTime, timer.Id);
			return await tcs.Task;
		}

		public async ETTask<bool> WaitAsync(long time, ETCancellationToken cancellationToken)
		{
			long tillTime = TimeHelper.ClientNow() + time;

            if (TimeHelper.ClientNow() > tillTime)
            {
                return true;
            }

            ETTaskCompletionSource<bool> tcs = new ETTaskCompletionSource<bool>();
			OnceWaitTimer timer = this.AddChild<OnceWaitTimer>(tcs);
			this._timers[timer.Id] = timer;
			AddToTimeId(tillTime, timer.Id);
			long instanceId = timer.InstanceId;
			cancellationToken.Register(() =>
			{
				if (instanceId != timer.InstanceId)
				{
					return;
				}
				
				timer.Run(false);
				
				this.Remove(timer.Id);
			});
			return await tcs.Task;
		}

		public async ETTask<bool> WaitAsync(long time)
		{
			long tillTime = TimeHelper.ClientNow() + time;
			ETTaskCompletionSource<bool> tcs = new ETTaskCompletionSource<bool>();
			OnceWaitTimer timer = this.AddChild<OnceWaitTimer>(tcs);
			this._timers[timer.Id] = timer;
			AddToTimeId(tillTime, timer.Id);
			return await tcs.Task;
		}

		/// <summary>
		/// 创建一个RepeatedTimer
		/// </summary>
		/// <param name="time"></param>
		/// <param name="action"></param>
		/// <returns></returns>
		public long NewRepeatedTimer(long time, Action<bool> action)
		{
			if (time < 30)
			{
				throw new Exception($"repeated time < 30");
			}
			long tillTime = TimeHelper.ClientNow() + time;
			RepeatedTimer timer = this.AddChild<RepeatedTimer>(new RepeatedTimerAwakeData() { Callback = action, RepeatedTime = time });
			this._timers[timer.Id] = timer;
			AddToTimeId(tillTime, timer.Id);
			return timer.Id;
		}
		
		public RepeatedTimer GetRepeatedTimer(long id)
		{
			if (!this._timers.TryGetValue(id, out ITimer timer))
			{
				return null;
			}
			return timer as RepeatedTimer;
		}
		
		public void Remove(long id)
		{
			if (id == 0)
			{
				return;
			}
			ITimer timer;
			if (!this._timers.TryGetValue(id, out timer))
			{
				return;
			}
			this._timers.Remove(id);
			
			(timer as IDisposable)?.Dispose();
		}
		
		public long NewOnceTimer(long tillTime, Action action)
		{
			OnceTimer timer = this.AddChild<OnceTimer>(action);
			this._timers[timer.Id] = timer;
			AddToTimeId(tillTime, timer.Id);
			return timer.Id;
		}
		
		public OnceTimer GetOnceTimer(long id)
		{
			if (!this._timers.TryGetValue(id, out ITimer timer))
			{
				return null;
			}
			return timer as OnceTimer;
		}

		public void AddToTimeId(long tillTime, long id)
		{
			this.TimeId.Add(tillTime, id);
			if (tillTime < this._minTime)
			{
				this._minTime = tillTime;
			}
		}
	}
}