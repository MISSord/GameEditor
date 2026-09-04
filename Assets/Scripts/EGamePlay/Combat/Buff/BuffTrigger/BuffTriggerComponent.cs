using System;
using System.Collections.Generic;

namespace EGamePlay.Combat
{
    /// <summary>
    /// Buff 触发器注册数据：供编辑/调试查看关心的行动点。开火走 StatusComponent.Dispatch。
    /// </summary>
    public sealed class BuffTriggerRegistration
    {
        public ActionPointType ActionPointType { get; set; }
        public Action<Entity> Callback { get; set; }
    }

    /// <summary>
    /// Buff 触发器组件：记录 Trigger / RemoveTrigger 配置。
    /// 实际开火由 <see cref="StatusComponent.Dispatch"/> 按优先级遍历，不再向 ActionPoint 注册监听。
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
                    Callback = null,
                });
            }
        }

        public bool OnUpdate(float deltaTime)
        {
            return ShouldRemove;
        }

        public override void OnDestroy()
        {
            foreach (var reg in Registrations)
                reg.Callback = null;
            Registrations?.Clear();
            ShouldRemove = false;
        }
    }
}
