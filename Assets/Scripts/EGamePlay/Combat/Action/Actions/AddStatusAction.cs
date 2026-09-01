using System;
using System.Collections.Generic;
using EGamePlay;

namespace EGamePlay.Combat
{
    public class AddStatusActionAbility : Entity, IActionAbility
    {
        public ICombatUnit OwnerEntity => GetParent<Entity>() as ICombatUnit;
        public bool Enable { get; set; }

        public bool TryMakeAction(out AddStatusAction action)
        {
            if (!Enable)
            {
                action = null;
                return false;
            }

            action = (AddStatusAction)CombatContext.Instance.AddAction<AddStatusAction>();
            action.Creator = OwnerEntity;
            return true;
        }
    }

    /// <summary>
    /// 施加 Buff 行动
    /// </summary>
    public class AddStatusAction : Entity, IActionExecute
    {
        public TriggerContext TriggerContext { get; set; }
        /// <summary>释放者。</summary>
        public ICombatUnit Creator { get; set; }
        /// <summary>目标实体。</summary>
        public ICombatUnit Target { get; set; }

        public void FinishAction() => Entity.Destroy(this);

        void PreProcess() { }

        void HandleReApplyExistingBuff(Buff existingBuff, BuffDemoSetting config)
        {
            if (existingBuff == null || config == null)
                return;

            var rule = config.ReApplyRule;
            existingBuff.TryGet(out BuffTimeComponent timeComponent);

            if (rule == BuffReApplyRule.AddDuration)
            {
                if (timeComponent != null && config.BaseDuration > 0f)
                    timeComponent.ExtendDuration(config.BaseDuration);
            }
            else if (rule == BuffReApplyRule.RefreshDuration)
            {
                if (timeComponent != null && config.BaseDuration > 0f)
                    timeComponent.ResetDuration(config.BaseDuration);
            }
            else if (rule == BuffReApplyRule.AddStackAndRefresh)
            {
                if (existingBuff.IsCanStack)
                {
                    var attrs = existingBuff.GetComponent<BuffAttributesComponent>();
                    if (attrs != null)
                    {
                        var stackProperty = attrs.GetNumeric(AttributeType.BuffMaxStacks);
                        if (stackProperty != null)
                            stackProperty.CurrentValue = stackProperty.CurrentValue + 1f;
                    }
                }

                if (timeComponent != null && config.BaseDuration > 0f)
                    timeComponent.ResetDuration(config.BaseDuration);
            }
            else if (rule == BuffReApplyRule.Exclusive)
            {
            }
            else if (timeComponent != null && config.BaseDuration > 0f)
            {
                timeComponent.ResetDuration(config.BaseDuration);
            }
        }

        public void ApplyAddStatusBySetting(int statusId, List<string> paramString1)
        {
            PreProcess();

            if (statusId <= 0 || Target?.Entity == null || !Target.Entity.TryGet(out StatusComponent statusComp))
            {
                FinishAction();
                return;
            }

            var buffConfig = SkillSettingMgr.Instance.GetBuffDemoSetting(statusId);

            if (statusComp.HasBuffId(statusId))
            {
                var existingBuff = statusComp.GetBuffById(statusId);
                if (existingBuff != null && !existingBuff.Enable)
                    existingBuff.ActivateBuff();
                else
                    HandleReApplyExistingBuff(existingBuff, buffConfig);
                PostProcess();
                FinishAction();
                return;
            }

            Buff buffAbility = statusComp.AttachStatus(statusId);
            buffAbility.Caster = Creator?.Entity;
            ProcessInputKVParams(buffAbility, paramString1);
            buffAbility.ActivateBuff();

            PostProcess();
            FinishAction();
        }

        void PostProcess()
        {
            Creator?.TriggerActionPoint(ActionPointType.PostGiveStatus, this);
            Target?.TriggerActionPoint(ActionPointType.PostReceiveStatus, this);
        }

        void ProcessInputKVParams(Buff ability, Dictionary<string, string> Params)
        {
            foreach (var keyValue in Params)
            {
                if (Enum.IsDefined(typeof(AttributeType), int.Parse(keyValue.Key)))
                {
                    ability.AddBuffAttribute(
                        (AttributeType)int.Parse(keyValue.Key),
                        ModifyType.SetBase,
                        null,
                        int.Parse(keyValue.Value),
                        true);
                }
            }
        }

        void ProcessInputKVParams(Buff ability, List<string> paramPairs)
        {
            if (paramPairs == null || paramPairs.Count == 0)
                return;

            for (int i = 0; i < paramPairs.Count; i++)
            {
                string s = paramPairs[i];
                if (string.IsNullOrEmpty(s))
                    continue;
                int eq = s.IndexOf('=');
                if (eq <= 0 || eq >= s.Length - 1)
                    continue;

                if (!int.TryParse(s.Substring(0, eq), out var keyInt))
                    continue;
                if (!int.TryParse(s.Substring(eq + 1), out var valInt))
                    continue;

                if (Enum.IsDefined(typeof(AttributeType), keyInt))
                {
                    ability.AddBuffAttribute((AttributeType)keyInt, ModifyType.SetBase, null, valInt, true);
                }
            }
        }
    }
}
