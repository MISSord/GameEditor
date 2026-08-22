using EGamePlay;
using EGamePlay.Combat;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ACTGameEditor
{
    // 新版执行器：将所有 XCEvent 放入单列表统一调度
    public class XCNewEventsRunner : Entity
    {
        private const float InitialTimeOffset = -0.1f; // 提前一点，避免第 0 帧边界误差

        private readonly List<XCEvent> _events = new List<XCEvent>(64);
        private readonly Dictionary<int, BuffModifySetting> _effectSettingsById = new Dictionary<int, BuffModifySetting>(16);
        private readonly HashSet<HitKey> _resolvedHits = new HashSet<HitKey>();
        private ActSkillRunner _parentRunner;

        private RunnerState _state;
        private float _time = InitialTimeOffset;
        private int _frame;

        /// <summary>Obj 事件：生成物体（一般为子技能载体）。</summary>
        public XCObjEvent ObjEvent { get; set; }

        public RunnerState State { get => _state; set => _state = value; }
        public CombatEntity OwnerEntity => _parentRunner != null ? _parentRunner.OwnerEntity : null;

        //释放技能时记录角度 和 位置
        public Vector3 CastEuler { get; private set; }
        public Vector3 CastPos { get; private set; }
        /// <summary>运行速度。</summary>
        public float Speed { get; private set; } = 1f;

        public void InitData(SkillNewEventData SkillData, Vector3 CastEuler, Vector3 CastPos)
        {
            _parentRunner = GetParent<ActSkillRunner>();
            Speed = SkillData.Speed;
            State = RunnerState.Update;
            this.CastEuler = CastEuler;
            this.CastPos = CastPos;
            _resolvedHits.Clear();
            // 池化复用时必须重置时间，否则首帧会跳过整段时间轴
            _time = InitialTimeOffset;
            _frame = 0;
            if (_events.Count > 0)
            {
                for (int i = _events.Count - 1; i >= 0; i--)
                {
                    var ev = _events[i];
                    ev.OnReset();
                    PoolManager.Instance.Return(ev);
                }
                _events.Clear();
            }
            ObjEvent = null;
        }

        public override void Update(float deltaTime)
        {
            if (State == RunnerState.Finish || State == RunnerState.Stop)
                return;

            if (State == RunnerState.Update)
            {
                bool isSelfEnd = OnUpdate(deltaTime);
                if (isSelfEnd)
                {
                    State = RunnerState.StopEnd;
                }
            }
            else if (State == RunnerState.StopEnd || State == RunnerState.Break)
            {
                State = RunnerState.Finish;
                DestroyAll();
            }
        }

        private bool OnUpdate(float deltaTime)
        {
            if (State == RunnerState.StopEnd || State == RunnerState.Break)
            {
                Debug.LogError("有问题，这种状态不应该进来的！！！");
                return true;
            }

            _time += deltaTime * Speed; //按照速度进行加减速
            //帧数是用时间累加计算出来的 delta不是稳定的
            //当前的1帧,指的的是逻辑帧,即1/30s,而不是update的一帧
            _frame = Mathf.FloorToInt(_time * XCSetting.FrameRate);

            bool isFinish = true;
            for (int i = 0; i < _events.Count; i++)
            {
                var ev = _events[i];
                if (ev.HasFinished) 
                    continue;
                //还没开始时
                if (_frame < ev.Start)
                {
                    if (ev.HasTriggered)
                    {
                        Debug.LogError($"{ev.GetType().Name} 有问题，应该在销毁Runner时候就完成重置！！！");
                        ev.OnReset();
                    }
                }
                else if (_frame >= ev.Start && _frame <= ev.End)
                {
                    if (!ev.HasFinished)
                    {
                        if (!ev.HasTriggered)
                        {
                            ev.OnTrigger(_time - ev.StartTime);
                        }
                        ev.UpdateEvent(_frame, _time - ev.StartTime);

                        // 在结束帧精确结束事件
                        if (_frame >= ev.End && ev.HasTriggered && !ev.HasFinished)
                        {
                            FinishEvent(ev);
                        }
                    }
                }
                else
                {
                    if (!ev.HasTriggered)
                    {
                        Debug.LogError($"{ev.GetType().Name} 有问题，可能设置运行时间太短或者时间间隔太大了导致没触发就结束了，排查！！！");
                    }

                    //当 frame > end ,既已经完成 可以退出了
                    if (!ev.HasFinished && ev.HasTriggered)
                    {
                        ev.OnFinish();
                        ev.SetFinished();
                    }
                }
                //有一个没完成就不能结束
                if (!ev.HasFinished)
                {
                    isFinish = false;
                }
            }
            return isFinish;
        }

        public void AddXCEvent(XCEvent xcevent)
        {
            _events.Add(xcevent);
        }

        public void DestroyAll()
        {
            for (int i = _events.Count - 1; i >= 0; i--)
            {
                //没有调用过的直接调一次
                var ev = _events[i];
                if (!ev.HasFinished)
                {
                    ev.OnFinish();
                    ev.SetFinished();
                }
                ev.OnReset();
                PoolManager.Instance.Return(ev);
            }
            _events.Clear();
            _resolvedHits.Clear();
            _parentRunner = null;
            _frame = 0;
            _time = InitialTimeOffset;
            ObjEvent = null;
        }

        public Animator GetAnimator()
        {
            if (ObjEvent != null)
            {
                return ObjEvent.LoadObj.GetComponentInChildren<Animator>();
            }
            else
            {
                return OwnerEntity.GetComponent<AnimComponent>().animator;
            }
        }

        /// <summary>
        /// 命中过滤：指定目标、HitGroup 去重、子轴仍在 Update。
        /// 不扣血、不 Break；通过后由 Pipeline 再落地效果。
        /// </summary>
        public HitResultKind TryAcceptHit(CombatEntity defender, XCTriggerEvent triggerEvent)
        {
            if (IsDisposed || State != RunnerState.Update)
                return HitResultKind.Ignored;
            if (defender == null || defender.IsDisposed)
                return HitResultKind.Ignored;
            if (triggerEvent == null || triggerEvent.TriggerEventData == null)
                return HitResultKind.Ignored;

            CombatEntity designated = _parentRunner?.InputTarget;
            if (designated != null && defender != designated)
                return HitResultKind.Ignored;

            int group = triggerEvent.TriggerEventData.HitGroupId;
            if (group == 0)
                group = triggerEvent.HitInstanceId;

            if (!_resolvedHits.Add(new HitKey(group, defender.Id)))
                return HitResultKind.Ignored;

            return HitResultKind.Land;
        }

        /// <summary>对已通过过滤的命中执行效果列表。</summary>
        public void ApplyAcceptedHit(CombatEntity defender, XCTriggerEvent triggerEvent)
        {
            if (IsDisposed || defender == null || defender.IsDisposed)
                return;
            if (triggerEvent == null || triggerEvent.TriggerEventData == null)
                return;

            TriggerEffectList(triggerEvent.TriggerEventData.EffectIds, defender,
                triggerEvent.TriggerEventData.DamageSegmentIndex);
        }

        /// <summary>命中后处理：指定目标命中后结束该子轴。受击打断等后续在此扩展。</summary>
        public void PostAcceptedHit()
        {
            if (_parentRunner?.InputTarget != null)
                State = RunnerState.Break;
        }

        private readonly struct HitKey : IEquatable<HitKey>
        {
            public readonly int Group;
            public readonly long DefenderId;

            public HitKey(int group, long defenderId)
            {
                Group = group;
                DefenderId = defenderId;
            }

            public bool Equals(HitKey other) => Group == other.Group && DefenderId == other.DefenderId;

            public override bool Equals(object obj) => obj is HitKey other && Equals(other);

            public override int GetHashCode() => Group * 397 ^ DefenderId.GetHashCode();
        }

        /// <summary>
        /// 触发效果：按 BuffModifySetting.EffectModifyID 执行；空列表时触发全部。
        /// </summary>
        public void TriggerEffectList(List<int> effectIds, Entity target, int damageSegmentIndex = 0)
        {
            if (effectIds == null) return;
            var ability = _parentRunner?.AbilityEntity;
            if (ability == null || OwnerEntity == null || OwnerEntity.IsDisposed) return;

            var owner = OwnerEntity;
            var settings = ability.Definition?.EffectModifyEffects;
            if (settings == null || settings.Count == 0) return;
            BuildEffectLookupIfNeeded(settings);

            if (effectIds.Count == 0)
            {
                for (int i = 0; i < settings.Count; i++)
                    EffectApplier.ApplySkillInline(settings[i], owner, target, ability, damageSegmentIndex);
                return;
            }

            foreach (var effectId in effectIds)
            {
                if (effectId <= 0) continue;
                if (_effectSettingsById.TryGetValue(effectId, out var setting))
                {
                    EffectApplier.ApplySkillInline(setting, owner, target, ability, damageSegmentIndex);
                }
            }
        }

        /// <summary>
        /// 移除效果：按 BuffModifySetting.EffectModifyID 移除 SkillAddStatus；空列表时移除全部 SkillAddStatus。
        /// </summary>
        public void RemoveTriggerEffectList(List<int> effectIds, Entity target)
        {
            if (effectIds == null) return;
            if (target == null || !target.TryGet(out StatusComponent statusComp)) return;

            var ability = _parentRunner?.AbilityEntity;
            if (ability == null) return;

            var settings = ability.Definition?.EffectModifyEffects;
            if (settings == null || settings.Count == 0) return;
            BuildEffectLookupIfNeeded(settings);

            if (effectIds.Count == 0)
            {
                for (int i = 0; i < settings.Count; i++)
                {
                    if (settings[i].EffectModifyType != EffectModifyType.SkillAddStatus) continue;
                    var buffId = settings[i].ParamInt1;
                    if (buffId > 0 && statusComp.HasBuffId(buffId))
                        statusComp.RemoveStatus(buffId);
                }
                return;
            }

            foreach (var effectId in effectIds)
            {
                if (effectId <= 0) continue;
                if (_effectSettingsById.TryGetValue(effectId, out var setting))
                {
                    if (setting.EffectModifyType == EffectModifyType.SkillAddStatus)
                    {
                        var buffId = setting.ParamInt1;
                        if (buffId > 0 && statusComp.HasBuffId(buffId))
                            statusComp.RemoveStatus(buffId);
                    }
                }
            }
        }

        private static void FinishEvent(XCEvent ev)
        {
            ev.OnFinish();
            ev.SetFinished();
        }

        private void BuildEffectLookupIfNeeded(List<BuffModifySetting> settings)
        {
            if (_effectSettingsById.Count > 0)
                return;

            for (int i = 0; i < settings.Count; i++)
            {
                var s = settings[i];
                if (s == null || s.EffectModifyID <= 0) continue;
                _effectSettingsById[s.EffectModifyID] = s;
            }
        }
    }
}
