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
            if (SkillAbility == null || SkillAbility.Definition == null)
                return true;

            var config = SkillAbility.Definition.Config;
            if (config == null)
                return true;

            // 未配置消耗（约定 CostAttrType<=0 时不做资源校验）
            if (config.CostAttrType <= 0)
                return true;

            var caster = Creator;
            if (caster == null || caster.IsDisposed)
                return false;

            var attrType = (AttributeType)config.CostAttrType;
            var formulaType = (ResourceFormulaType)config.CostFormulaType;
            float a = config.CostA;
            float b = config.CostB;

            // 计算本次需要消耗的资源值（正数表示需求量）
            int need;
            if (formulaType == ResourceFormulaType.Flat)
            {
                need = (int)a;
            }
            else
            {
                var vital = caster.CurrentVital;
                if (vital == null)
                    return false;

                var ctx = new ResourceFormulaContext
                {
                    Caster = caster,
                    Target = caster,
                    FormulaType = formulaType,
                    AttrOrVitalType = attrType,
                    A = a,
                    B = b,
                    CeilResult = true,
                };
                need = ResourceFormula.Calculate(ctx);
            }

            if (need <= 0)
                return true;

            // 检查是否有足够资源
            if (caster.CurrentVital == null)
                return false;
            float currentValue = caster.CurrentVital.GetVitalValue(attrType);
            if (currentValue < need)
            {
                // 资源不足：这里暂不触发额外事件，仅阻止施法
                return false;
            }

            // 通过 ResourceAction 扣除资源（负数表示消耗）
            if (caster.ResourceAbility != null &&
                caster.ResourceAbility.TryMakeAction(out var resourceAction))
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
                    TriggerSource = caster,
                    Target = caster,
                };

                resourceAction.Target = caster;
                resourceAction.TriggerContext = context;
                resourceAction.ApplyCure();
            }

            return true;
        }

        public void FinishAction()
        {
#if UNITY_EDITOR
            GameLog.CombatError($"FinishAction {ActSkillRunner.Id} {SkillAbility.SkillID}");
#endif
            SkillAbility = null;
            InputTarget = null;
            Creator = null;
            Target = null;
            _isPostProcess = false;
            Entity.Destroy(this);
        }

        public void SpellSkill(bool actionOccupy = true)
        {
            PreProcess();

            // 施法前资源校验与扣除，不满足则直接结束本次行动
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
            if (skillData == null)
            {
#if UNITY_EDITOR
                GameLog.CombatError($"[SpellAction] SkillData 为空 skillId={SkillAbility.SkillID}");
#endif
                FinishAction();
                return;
            }
            foreach (var subSkill in skillData.skillAllEventDatas)
            {
                XCNewEventsRunner subRunner = runner.AddChild<XCNewEventsRunner>();
                StartRuner(subRunner, Creator, subSkill, InputDirection, Creator.Position);
                runner.SubRuners.Add(subRunner);
            }
            runner.StartUpdate();
            //打断之前的 这里未来可以拓展，按照当前状态是否打断之前的技能
            if (Creator.SpellingExecution != null)
                Creator.SpellingExecution.BreakSkill();
            Creator.SpellingExecution = runner;
            Creator.CurState = PlayerStateEnum.PlayerSkill;
            ActSkillRunner = runner;
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

        //后置处理
        private void PostProcess()
        {
            _isPostProcess = true;
            Creator.TriggerActionPoint(ActionPointType.PostSpell, this);
            //如果是当前这个执行器，清空，如果不是，说明有其他技能正在跑，不移除
            if(Creator.SpellingExecution == ActSkillRunner)
            {
                Creator.SpellingExecution = null;
            }
            Creator.CurState = PlayerStateEnum.Idle;
        }
    }
}