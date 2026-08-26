using EGamePlay;
using EGamePlay.Combat;
using UnityEngine;

namespace ACTGameEditor.Combat
{
    /// <summary>
    /// 单次施法 Session：资源/行动点/占轴/Runner Tick；替代原 SpellAction Entity。
    /// </summary>
    public sealed class ActSpellSession : Entity, ICombatSpellActionContext
    {
        CombatEntity _caster;
        Ability _ability;
        ActSkillRunner _runner;
        CombatEntity _inputTarget;
        Vector3 _inputPoint;
        Vector3 _inputDirection;
        bool _postProcessed;

        public ICombatUnit Caster => _caster;
        public ICombatUnit InputTarget => _inputTarget;
        public Vector3 InputPoint => _inputPoint;
        public Vector3 InputDirection => _inputDirection;

        /// <summary>创建并启动一次施法。</summary>
        public static ActSpellSession Start(CombatEntity caster, in CombatCastIntent intent, Ability ability)
        {
            if (caster == null || caster.IsDisposed || ability == null)
                return null;

            if (ability.RequiresTimeline && ActSkillTimelineLoader.GetOrLoad(ability.SkillID) == null)
            {
#if UNITY_EDITOR
                GameLog.CombatError($"[ActSpellSession] SkillData 为空 skillId={ability.SkillID}");
#endif
                return null;
            }

            var session = (ActSpellSession)CombatContext.Instance.AddAction<ActSpellSession>();
            session._caster = caster;
            session._ability = ability;
            session._inputTarget = intent.Target as CombatEntity;
            session._inputPoint = intent.Point;
            session._inputDirection = intent.Direction;

            session.PreProcess();
            if (!session.TryConsumeResource())
            {
                session.DestroySelf();
                return null;
            }

            session.LaunchRunner(intent.Sort);
            return session;
        }

        void PreProcess()
        {
            _postProcessed = false;
            _caster.TriggerActionPoint(ActionPointType.PreSpell, this);
        }

        bool TryConsumeResource()
        {
            if (!AbilityActivationGate.TryGetResourceCost(_caster, _ability, out int need, out var attrType))
                return false;
            if (need <= 0)
                return true;

            if (_caster.CurrentVital == null)
                return false;
            if (_caster.CurrentVital.GetVitalValue(attrType) < need)
                return false;

            if (_caster.ResourceAbility == null || !_caster.ResourceAbility.TryMakeAction(out var resourceAction))
                return true;

            var effect = new CureEffect
            {
                AttributeType = attrType,
                CureValueProperty = -need,
            };

            var context = new TriggerContext
            {
                EffectConfig = effect,
                SourceAbility = _ability,
                TriggerSource = _caster,
                Target = _caster,
            };

            resourceAction.Target = _caster;
            resourceAction.TriggerContext = context;
            resourceAction.ApplyCure();
            return true;
        }

        void LaunchRunner(int sort)
        {
            ISkillExecutionHandle previous = _caster.ActiveExecution;
            _runner = ActSpellSkillAssembler.Launch(this, _caster, _ability, _inputTarget, _inputDirection, sort);
            if (_runner == null)
            {
                DestroySelf();
                return;
            }

            if (previous != null && previous != _runner && !previous.IsDisposed)
                previous.BreakSkill();

            _caster.ActiveExecution = _runner;
            _caster.StateDirector?.EnterSkill(_runner.Id);

            if (CombatContext.Instance != null && CombatContext.Instance.UseAbilityGate)
            {
                var spellComp = _caster.GetComponent<ActSpellComponent>();
                spellComp?.CDTimer?.StartCooldown(_ability.SkillID);
            }
        }

        public override void Update(float deltaTime)
        {
            if (_runner == null || _runner.IsDisposed)
            {
                DestroySelf();
                return;
            }

            ISkillExecutionHandle handle = _runner;
            handle.Tick(deltaTime);

            if (_runner.IsMainFinish && !_postProcessed)
                PostProcess();

            if (handle.IsFinished)
                Finish();
        }

        void PostProcess()
        {
            _postProcessed = true;
            _caster?.TriggerActionPoint(ActionPointType.PostSpell, this);
        }

        void Finish()
        {
#if UNITY_EDITOR
            GameLog.CombatError($"FinishAction {_runner?.Id} {_ability?.SkillID}");
#endif
            bool releasedAxis = false;
            if (_caster != null && !_caster.IsDisposed && _runner != null
                && _caster.ActiveExecution == _runner)
            {
                _caster.ActiveExecution = null;
                releasedAxis = true;
            }

            if (_caster != null && !_caster.IsDisposed && _runner != null)
            {
                _caster.TagHost.PopTagsFrom(TagSource.Skill(_runner.Id));
                if (releasedAxis)
                    _caster.StateDirector?.ExitSkill(_runner.Id);
            }

            DestroySelf();
        }

        void DestroySelf()
        {
            _caster = null;
            _ability = null;
            _runner = null;
            _inputTarget = null;
            _postProcessed = false;
            Entity.Destroy(this);
        }
    }
}
