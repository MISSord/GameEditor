using System.Collections.Generic;
using UnityEngine;
using ACTGameEditor;
using XiaoCao;

#if EGAMEPLAY_ET
using Unity.Mathematics;
using Vector3 = Unity.Mathematics.float3;
using Quaternion = Unity.Mathematics.quaternion;
using AO;
using AO.EventType;
using ET.EventType;
#else
using float3 = UnityEngine.Vector3;
#endif

namespace EGamePlay.Combat
{
    public class SpellActionAbility : Entity, IActionAbility
    {
        public CombatEntity OwnerEntity { get { return GetParent<CombatEntity>(); } set { } }
        public bool Enable { get; set; }

        public bool TryMakeAction(out SpellAction action)
        {
            if (Enable == false)
            {
                action = null;
            }
            else
            {
                action = (SpellAction)CombatContext.Instance.AddAction<SpellAction>();
                action.Creator = OwnerEntity;
            }
            return Enable;
        }
    }

    //由施法组件调用创建施法行动，目前设计是施法行动没结束前不能再次施法
    //AbilityExecution作为实际的执法类，

    /// <summary>
    /// 施法行动
    /// </summary>
    public class SpellAction : Entity, IActionExecute
    {
        public Ability SkillAbility { get; set; }
        public ActSkillRunner ActSkillRunner { get; set; }
        public CombatEntity InputTarget { get; set; }
        public Vector3 InputPoint { get; set; }
        public Vector3 InputDirection { get; set; }
        public int Sort { get; set; } = 0;
        // 行动实体
        public CombatEntity Creator { get; set; }
        // 目标对象
        public Entity Target { get; set; }

        private bool _isPostProcess = false;

        /// <summary>
        /// 在施法前根据 SkillDemoSetting 做资源校验与扣除。
        /// 返回 true 表示可以继续施法，false 表示资源不足应中断施法。
        /// </summary>
        private bool TryConsumeResourceBeforeSpell()
        {
            if (!AbilityActivationGate.TryGetResourceCost(Creator, SkillAbility, out int need, out var attrType))
                return false;
            if (need <= 0)
                return true;

            if (Creator.CurrentVital == null)
                return false;
            if (Creator.CurrentVital.GetVitalValue(attrType) < need)
                return false;

            if (Creator.ResourceAbility != null &&
                Creator.ResourceAbility.TryMakeAction(out var resourceAction))
            {
                var effect = new CureEffect
                {
                    AttributeType = attrType,
                    CureValueProperty = -need,
                };

                var context = new TriggerContext
                {
                    EffectConfig = effect,
                    SourceAbility = SkillAbility,
                    TriggerSource = Creator,
                    Target = Creator,
                };

                resourceAction.Target = Creator;
                resourceAction.TriggerContext = context;
                resourceAction.ApplyCure();
            }

            return true;
        }

        public void FinishAction()
        {
#if UNITY_EDITOR
            GameLog.CombatError($"FinishAction {ActSkillRunner?.Id} {SkillAbility?.SkillID}");
#endif
            // ParentFinish 后 SpellingExecution 会故意残留到后摇结束；此处必须清掉，
            // 否则 Runner 回池再被取出时，BreakSkill(SpellingExecution) 会打断「自己」。
            bool releasedAxis = false;
            if (Creator != null && !Creator.IsDisposed && ActSkillRunner != null
                && Creator.SpellingExecution == ActSkillRunner)
            {
                Creator.SpellingExecution = null;
                releasedAxis = true;
            }

            // 仅当本轴是最后占轴者时退出 Skill 槽；被新技能顶替时 source 不匹配会空操作
            if (Creator != null && !Creator.IsDisposed && ActSkillRunner != null)
            {
                // Tag 必须按本轴 Source 清；即使已被顶替（releasedAxis=false）
                Creator.PopTagsFrom(TagSource.Skill(ActSkillRunner.Id));
                if (releasedAxis)
                    Creator.StateDirector?.ExitSkill(ActSkillRunner.Id);
            }

            SkillAbility = null;
            InputTarget = null;
            Creator = null;
            Target = null;
            ActSkillRunner = null;
            _isPostProcess = false;
            Entity.Destroy(this);
        }

        public void SpellSkill(bool actionOccupy = true)
        {
            if (Creator == null || Creator.IsDisposed)
            {
                FinishAction();
                return;
            }

            if (SkillAbility?.SkillData == null)
            {
#if UNITY_EDITOR
                GameLog.CombatError($"[SpellAction] SkillData 为空 skillId={SkillAbility?.SkillID}");
#endif
                FinishAction();
                return;
            }

            PreProcess();

            if (!TryConsumeResourceBeforeSpell())
            {
                FinishAction();
                return;
            }

            var runner = AddChild<ActSkillRunner>();
#if UNITY_EDITOR
            GameLog.CombatError($"ActSkillRunner {runner.Id} {SkillAbility.SkillID}");
#endif
            runner.OwnerEntity = Creator;
            runner.InputTarget = InputTarget;
            runner.AbilityEntity = SkillAbility;
            runner.Sort = Sort;
            var skillData = SkillAbility.SkillData;
            foreach (var subSkill in skillData.skillAllEventDatas)
            {
                XCNewEventsRunner subRunner = runner.AddChild<XCNewEventsRunner>();
                StartRuner(subRunner, Creator, subSkill, InputDirection, Creator.Position);
                runner.SubRuners.Add(subRunner);
            }
            runner.StartUpdate();

            ActSkillRunner previous = Creator.SpellingExecution;
            // 必须排除 previous == runner（池化复用同一实例时），否则会 Break 掉刚 Start 的自己
            if (previous != null && previous != runner && !previous.IsDisposed)
                previous.BreakSkill();

            Creator.SpellingExecution = runner;
            Creator.StateDirector?.EnterSkill(runner.Id);
            ActSkillRunner = runner;

            if (CombatContext.Instance != null && CombatContext.Instance.UseAbilityGate)
                Creator.GetComponent<SpellComponent>()?.CDTimer?.StartCooldown(SkillAbility.SkillID);
        }

        public void StartRuner(XCNewEventsRunner runner, CombatEntity skillOwner, SkillNewEventData skillData, Vector3 castEuler, Vector3 castPos)
        {
            runner.InitData(skillData, castEuler, castPos);

            if (skillData.HasObjEvent)
            {
                AddTrackToRunner<XCObjEvent>(runner, skillOwner, new List<XCEventData>() { skillData.ObjEvent });
            }

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

        private void AddTrackToRunner<T>(XCNewEventsRunner runner, CombatEntity owner, List<XCEventData> xcevents) where T : XCEvent
        {
            int length = xcevents.Count;
            if (length == 0) return;

            for (int i = 0; i < length; i++)
            {
                //移除本地事件
                bool isRemove = IsRemoveLocalTrue(owner.isTruePlayer, xcevents[i]);
                if (isRemove == false)
                {
                    XCEvent runn = PoolManager.Instance.TryGet<T>();
                    runn.EventData = xcevents[i];
                    runn.Range = xcevents[i].Range;
                    runn.Init(owner, runner);
                    runner.AddXCEvent(runn);
                }
            }

            //XCEventsTrack track = PoolManager.Instance.TryGet<XCEventsTrack>();
            //track.SelfRunner = runner;
            //track.Init(xcevents, owner);
            //runner.AddTrack(track);
        }

        private bool IsRemoveLocalTrue<T>(bool IsLocalTruePlayer, T xcevent) where T : XCEventData
        {
            return xcevent.IsLocalTrueOnly && !IsLocalTruePlayer;
        }

        public override void Update(float deltaTime)
        {
            if (ActSkillRunner != null)
            {
                ActSkillRunner.Update(deltaTime);
                //销毁与施法后调分离
                if(ActSkillRunner.IsMainFinish == true && _isPostProcess == false)
                {
                    PostProcess();
                }
                if (ActSkillRunner.State == RunnerState.Finish)
                {
                    FinishAction();
                }
            }
        }

        //前置处理
        private void PreProcess()
        {
            _isPostProcess = false;
            Creator.TriggerActionPoint(ActionPointType.PreSpell, this);
        }

        //后置处理（ParentFinish：逻辑上可接招/可切人，时间轴可继续播后摇）
        private void PostProcess()
        {
            _isPostProcess = true;
            Creator.TriggerActionPoint(ActionPointType.PostSpell, this);
            // 不清 SpellingExecution、不改 CurState 为 Idle：
            // - SpellingExecution 残留供下一段 Break，并锁 IsCanMove
            // - CurState 保持 PlayerSkill，避免后摇阶段又能走
            // 真正结束时由 FinishAction 清空轴并回到 Idle
        }
    }
}