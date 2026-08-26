using System;
using System.Collections.Generic;

namespace EGamePlay.Combat
{
    /// <summary>
    /// Buff 触发器注册数据：ActionPointType + Callback，用于 ListenActionPoint。
    /// </summary>
    public sealed class BuffTriggerRegistration
    {
        public ActionPointType ActionPointType { get; set; }
        public Action<Entity> Callback { get; set; }
    }

    /// <summary>
    /// Buff 触发器组件：持有 BuffTriggerRegistration[]，用 ListenActionPoint 注册，不创建 BuffTrigger 子 Entity。
    /// </summary>
    public class BuffTriggerComponent : Component, ILifecycleLogic
    {
        public override bool DefaultEnable { get; set; } = false;
        public List<BuffTriggerRegistration> Registrations { get; private set; } = new List<BuffTriggerRegistration>();

        public bool ShouldRemove { get; set; }

        public override void Awake()
        {
            Buff buff = Entity.As<Buff>();
            if (buff?.Setting == null) return;

            if (buff.Setting.BuffType.HasFlag(BuffType.TriggerBuff))
            {
                Registrations.Add(new BuffTriggerRegistration
                {
                    ActionPointType = buff.Setting.ActionPointType,
                    Callback = buff.OnEvent,
                });
            }

            if (buff.Setting.BuffType.HasFlag(BuffType.RemoveTriggerBuff))
            {
                Registrations.Add(new BuffTriggerRegistration
                {
                    ActionPointType = buff.Setting.RemoveActionPointType,
                    Callback = (Entity entity) =>
                    {
                        ShouldRemove = true;
                        buff.CheckIsCanRemove();
                    },
                });
            }
        }

        public override void OnEnable()
        {
            var owner = Entity.As<Buff>()?.OwnerEntity;
            if (owner == null || owner.IsDisposed) return;

            var actionPointComp = owner.Entity?.GetComponent<ActionPointComponent>();
            if (actionPointComp == null) return;

            foreach (var reg in Registrations)
            {
                if (reg.Callback != null)
                    actionPointComp.AddListener(reg.ActionPointType, reg.Callback);
            }
        }

        public bool OnUpdate(float deltaTime)
        {
            return ShouldRemove;
        }

        public override void OnDisable()
        {
            var owner = Entity.As<Buff>()?.OwnerEntity;
            if (owner != null && !owner.IsDisposed)
            {
                var actionPointComp = owner.Entity?.GetComponent<ActionPointComponent>();
                if (actionPointComp != null)
                {
                    foreach (var reg in Registrations)
                    {
                        if (reg.Callback != null)
                            actionPointComp.RemoveListener(reg.ActionPointType, reg.Callback);
                    }
                }
            }
        }

        public override void OnDestroy()
        {
            var owner = Entity.As<Buff>()?.OwnerEntity;
            if (owner != null && !owner.IsDisposed)
            {
                var actionPointComp = owner.Entity?.GetComponent<ActionPointComponent>();
                if (actionPointComp != null)
                {
                    foreach (var reg in Registrations)
                    {
                        if (reg.Callback != null)
                            actionPointComp.RemoveListener(reg.ActionPointType, reg.Callback);
                    }
                }
            }
            foreach (var reg in Registrations)
            {
                reg.Callback = null;
            }
            Registrations?.Clear();
        }
    }
}
