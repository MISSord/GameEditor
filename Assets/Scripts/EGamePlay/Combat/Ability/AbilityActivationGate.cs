using ACTGameEditor;

namespace EGamePlay.Combat
{
        /// <summary>施法 Gate 失败原因。SortBlocked 表示本帧保留队列，其余为丢弃。</summary>
        public enum ActivateFail : byte
        {
            None = 0,
            NoAbility = 1,
            Cooldown = 2,
            Resource = 3,
            SortBlocked = 4,
            /// <summary>硬直 / 禁技能 / 受击 / 死亡。</summary>
            State = 5,
            /// <summary>技能表 RequiredTags / BlockedTags 不满足。</summary>
            Tag = 6,
            /// <summary>TriggerFormula 不通过。</summary>
            Formula = 7,
        }

    /// <summary>
    /// 施法前只读裁决。不扣资源、不转 CD、不改战斗状态。
    /// </summary>
    public static class AbilityActivationGate
    {
        /// <summary>
        /// 裁决一次出手意图。提交时复检硬直/标签；checkCostAndCooldown 为 false 时跳过 CD/资源。
        /// </summary>
        public static ActivateFail Evaluate(
            CombatEntity actor,
            int skillId,
            int incomingSort,
            SkillCDTimer cdTimer,
            bool checkCostAndCooldown)
        {
            if (actor == null || actor.IsDisposed)
                return ActivateFail.NoAbility;

            var abilityComp = actor.GetComponent<AbilityComponent>();
            if (abilityComp == null || !abilityComp.IdAbilities.TryGetValue(skillId, out var ability) || ability == null)
                return ActivateFail.NoAbility;

            // ParentFinish 后残留后摇不算占轴，避免挡住同级普攻循环，并让新技能能 Break 旧 Runner
            var current = actor.SpellingExecution;
            if (current != null && current.IsMainFinish)
                current = null;

            bool hardInterrupt = current != null
                && SkillCancelService.IsHardInterrupt(current.Sort, incomingSort);

            // 硬打断不看 UnStopped（普攻霸体不应挡住闪避）；同级连招仍走 IsCanSpellSkill。
            if (hardInterrupt)
            {
                if (!actor.IsCanSelfCancelSkill)
                    return ActivateFail.State;
            }
            else if (!actor.IsCanSpellSkill)
            {
                return ActivateFail.State;
            }

            var config = ability.Definition?.Config;
            if (config != null && !actor.CanSpellSkillWithTagLists(config.RequiredTags, config.BlockedTags))
                return ActivateFail.Tag;

            if (!PassesTriggerFormula(config))
                return ActivateFail.Formula;

            if (checkCostAndCooldown)
            {
                if (cdTimer != null && !cdTimer.IsCDEnd(skillId))
                    return ActivateFail.Cooldown;

                if (!CanAfford(actor, ability))
                    return ActivateFail.Resource;
            }

            if (current != null && !SkillCancelService.ShouldReplace(current.Sort, incomingSort))
                return ActivateFail.SortBlocked;

            return ActivateFail.None;
        }

        /// <summary>资源是否足够支付配置消耗。未配置消耗视为足够。</summary>
        public static bool CanAfford(CombatEntity caster, Ability ability)
        {
            if (!TryGetResourceCost(caster, ability, out int need, out var attrType))
                return false;
            if (need <= 0)
                return true;
            if (caster.CurrentVital == null)
                return false;
            return caster.CurrentVital.GetVitalValue(attrType) >= need;
        }

        /// <summary>
        /// 计算本次消耗。未配置消耗时 need=0 且返回 true。
        /// caster/config 非法时返回 false。
        /// </summary>
        public static bool TryGetResourceCost(CombatEntity caster, Ability ability, out int need, out AttributeType attrType)
        {
            need = 0;
            attrType = AttributeType.None;

            if (caster == null || caster.IsDisposed)
                return false;
            if (ability?.Definition?.Config == null)
                return true;

            var config = ability.Definition.Config;
            if (config.CostAttrType <= 0)
                return true;

            attrType = (AttributeType)config.CostAttrType;
            var formulaType = (ResourceFormulaType)config.CostFormulaType;
            float a = config.CostA;
            float b = config.CostB;

            if (formulaType == ResourceFormulaType.Flat)
            {
                need = (int)a;
                return true;
            }

            if (caster.CurrentVital == null)
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
            return true;
        }

        /// <summary>表驱动 TriggerFormula。空公式视为通过。</summary>
        public static bool PassesTriggerFormula(SkillDemoSetting config)
        {
            if (config == null || string.IsNullOrEmpty(config.TriggerFormula))
                return true;
            object result = FastStaticExecutor.Execute(config.TriggerFormula);
            return result is bool ok && ok;
        }
    }
}
