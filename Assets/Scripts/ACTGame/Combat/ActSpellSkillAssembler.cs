using EGamePlay.Combat;
using System.Collections.Generic;
using UnityEngine;

namespace ACTGameEditor.Combat
{
    /// <summary>将 SkillAllEventData 装配为 XC 子轴并挂到 ActSkillRunner。</summary>
    public static class ActSpellSkillAssembler
    {
        /// <summary>在 parent 下创建并启动技能 Runner。</summary>
        public static ActSkillRunner Launch(
            EGamePlay.Entity parent,
            CombatEntity caster,
            Ability ability,
            CombatEntity inputTarget,
            Vector3 inputDirection,
            int sort)
        {
            if (caster == null || caster.IsDisposed || ability == null)
                return null;

            SkillAllEventData skillData = ActSkillTimelineLoader.GetOrLoad(ability.SkillID);
            if (skillData == null)
                return null;

            var runner = parent.AddChild<ActSkillRunner>();
#if UNITY_EDITOR
            GameLog.CombatError($"ActSkillRunner {runner.Id} {ability.SkillID}");
#endif
            runner.OwnerEntity = caster;
            runner.InputTarget = inputTarget;
            runner.AbilityEntity = ability;
            runner.Sort = sort;

            for (int i = 0; i < skillData.skillAllEventDatas.Count; i++)
            {
                SkillNewEventData subSkill = skillData.skillAllEventDatas[i];
                XCNewEventsRunner subRunner = runner.AddChild<XCNewEventsRunner>();
                StartRunner(subRunner, caster, subSkill, inputDirection, caster.Position);
                runner.SubRuners.Add(subRunner);
            }

            runner.StartUpdate();
            return runner;
        }

        static void StartRunner(
            XCNewEventsRunner runner,
            CombatEntity skillOwner,
            SkillNewEventData skillData,
            Vector3 castEuler,
            Vector3 castPos)
        {
            runner.InitData(skillData, castEuler, castPos);

            if (skillData.HasObjEvent)
                AddTrackToRunner<XCObjEvent>(runner, skillOwner, new List<XCEventData> { skillData.ObjEvent });

            AddTrackToRunner<XCTriggerEvent>(runner, skillOwner, skillData.TriggerEvents.ToXCEventList());
            AddTrackToRunner<XCAnimEvent>(runner, skillOwner, skillData.AnimEvents.ToXCEventList());
            AddTrackToRunner<XCMoveEvent>(runner, skillOwner, skillData.MoveEvents.ToXCEventList());
            AddTrackToRunner<XCScaleEvent>(runner, skillOwner, skillData.ScaleEvents.ToXCEventList());
            AddTrackToRunner<XCRotateEvent>(runner, skillOwner, skillData.RotateEvents.ToXCEventList());
            AddTrackToRunner<XCMsgEvent>(runner, skillOwner, skillData.MsgEvents.ToXCEventList());
            AddTrackToRunner<XCSwitchEvent>(runner, skillOwner, skillData.SwitchEvents.ToXCEventList());
            AddTrackToRunner<XCSkillInputEvent>(runner, skillOwner, skillData.SkillInputEvents.ToXCEventList());
            AddTrackToRunner<XCEffectEvent>(runner, skillOwner, skillData.EffectEvents.ToXCEventList());
        }

        static void AddTrackToRunner<T>(XCNewEventsRunner runner, CombatEntity owner, List<XCEventData> xcevents)
            where T : XCEvent
        {
            int length = xcevents.Count;
            if (length == 0)
                return;

            for (int i = 0; i < length; i++)
            {
                if (IsRemoveLocalTrue(owner.isTruePlayer, xcevents[i]))
                    continue;

                XCEvent runn = PoolManager.Instance.TryGet<T>();
                runn.EventData = xcevents[i];
                runn.Range = xcevents[i].Range;
                runn.Init(owner, runner);
                runner.AddXCEvent(runn);
            }
        }

        static bool IsRemoveLocalTrue<T>(bool isLocalTruePlayer, T xcevent) where T : XCEventData
        {
            return xcevent.IsLocalTrueOnly && !isLocalTruePlayer;
        }
    }
}
