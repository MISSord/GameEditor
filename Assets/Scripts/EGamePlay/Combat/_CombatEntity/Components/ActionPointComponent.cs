using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using Sirenix.OdinInspector;

namespace EGamePlay.Combat
{
    /// <summary>
    /// 行动点，一次战斗行动<see cref="IActionExecute"/>会触发战斗实体一系列的行动点<see cref="ActionPoint"/>
    /// </summary>
    public sealed class ActionPoint
    {
        public List<Action<Entity>> Listeners { get; set; } = new List<Action<Entity>>();

        public void AddListener(Action<Entity> action)
        {
            Listeners.Add(action);
        }

        public void RemoveListener(Action<Entity> action)
        {
            Listeners.Remove(action);
        }

        public void TriggerAllListeners(Entity actionExecution)
        {
            if (Listeners.Count > 0)
            {
                for (int i = Listeners.Count - 1; i >= 0; i--)
                {
                    Listeners[i].Invoke(actionExecution);
                }
            }
        }
    }

    //[Flags]
    //[LabelText("行动点类型")]
    //public enum ActionPointType
    //{
    //    [LabelText("（空）")]
    //    None = 0,

    //    [LabelText("造成伤害前")]
    //    PreCauseDamage = 1 << 1,
    //    [LabelText("承受伤害前")]
    //    PreReceiveDamage = 1 << 2,

    //    [LabelText("造成伤害后")]
    //    PostCauseDamage = 1 << 3,
    //    [LabelText("承受伤害后")]
    //    PostReceiveDamage = 1 << 4,

    //    [LabelText("给予治疗后")]
    //    PostGiveCure = 1 << 5,
    //    [LabelText("接受治疗后")]
    //    PostReceiveCure = 1 << 6,

    //    [LabelText("赋给状态前")]
    //    PreGiveStatus = 1 << 7,
    //    [LabelText("承受状态前")]
    //    PreReceiveStatus = 1 << 8,

    //    [LabelText("赋加状态后")]
    //    PostGiveStatus = 1 << 9,
    //    [LabelText("承受状态后")]
    //    PostReceiveStatus = 1 << 10,

    //    [LabelText("创建效果后")]
    //    PostAssignEffect = 1 << 11,

    //    //[LabelText("给予普攻前")]
    //    //PreGiveAttack = 1 << 11,
    //    //[LabelText("给予普攻后")]
    //    //PostGiveAttack = 1 << 12,

    //    //[LabelText("遭受普攻前")]
    //    //PreReceiveAttack = 1 << 13,
    //    //[LabelText("遭受普攻后")]
    //    //PostReceiveAttack = 1 << 14,

    //    [LabelText("起跳前")]
    //    PreJumpTo = 1 << 15,
    //    [LabelText("起跳后")]
    //    PostJumpTo = 1 << 16,

    //    [LabelText("施法前")]
    //    PreSpell = 1 << 17,
    //    [LabelText("施法后")]
    //    PostSpell = 1 << 18,

    //    //[LabelText("赋给普攻效果前")]
    //    //PreGiveAttackEffect = 1 << 19,
    //    //[LabelText("赋给普攻效果后")]
    //    //PostGiveAttackEffect = 1 << 20,
    //    //[LabelText("承受普攻效果前")]
    //    //PreReceiveAttackEffect = 1 << 21,
    //    //[LabelText("承受普攻效果后")]
    //    //PostReceiveAttackEffect = 1 << 22,
    //    [LabelText("碰撞事件发生前")]
    //    CollisionBeforeEffect = 1 << 23,
    //    [LabelText("碰撞事件发生后")]
    //    CollisionAfterEffect = 1 << 24,

    //    Max,
    //}

    //Listener和Observer最大的不同是目标不一样，Observier是面向订阅组件的，传入订阅的组件，Listener是面向其他方面的，传入订阅事件
    //因此订阅的时候要留意接受订阅对象是什么，来决定应该Listener还是Observer，并且进行广播的时候是先Listener后Observer的

    /// <summary>
    /// 行动点管理器，在这里管理一个战斗实体所有行动点的添加监听、移除监听、触发流程
    /// </summary>
    public sealed class ActionPointComponent : Component
    {
        private Dictionary<ActionPointType, ActionPoint> ActionPoints { get; set; } = new Dictionary<ActionPointType, ActionPoint>();


        /// <summary>注册行动点监听。</summary>
        public void ListenActionPoint(ActionPointType actionPointType, Action<Entity> action) =>
            AddListener(actionPointType, action);

        /// <summary>移除行动点监听。</summary>
        public void UnListenActionPoint(ActionPointType actionPointType, Action<Entity> action) =>
            RemoveListener(actionPointType, action);

        /// <summary>
        /// 添加监听者
        /// </summary>
        /// <param name="actionPointType"></param>
        /// <param name="action"></param>
        public void AddListener(ActionPointType actionPointType, Action<Entity> action)
        {
            if (!ActionPoints.ContainsKey(actionPointType))
            {
                ActionPoints.Add(actionPointType, new ActionPoint());
            }
            ActionPoints[actionPointType].AddListener(action);
        }

        public void RemoveListener(ActionPointType actionPointType, Action<Entity> action)
        {
            if (ActionPoints.ContainsKey(actionPointType))
            {
                ActionPoints[actionPointType].RemoveListener(action);
            }
        }

        /// <summary>
        /// 触发行动节点
        /// </summary>
        public void TriggerActionPoint(ActionPointType actionPointType, Entity actionExecution)
        {
            if (ActionPoints.TryGetValue(actionPointType, out ActionPoint actionPoint))
                actionPoint.TriggerAllListeners(actionExecution);
        }

        public override void OnDestroy()
        {
            foreach (var ap in ActionPoints.Values)
                ap?.Listeners?.Clear();
            ActionPoints.Clear();
        }

        public override void OnReset()
        {
            OnDestroy();
        }
    }
}