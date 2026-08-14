//using System.Collections.Generic;
//using XiaoCao;

//namespace EGamePlay.Combat
//{
//    /// <summary>
//    /// Buff 被动触发组件：复用 TriggerConfig，让 Buff 像被动技能一样按 ActionPoint / 状态条件触发效果。
//    /// 约定 TriggerConfig.EffectIds 存的是 BuffModifySetting.EffectModifyID（表化效果行）。
//    /// 不创建子 Entity，触发时直接执行对应 BuffModifySetting。
//    /// </summary>
//    public class BuffAbilityTriggerComponent : Component
//    {
//        public override bool DefaultEnable { get; set; } = false;
//        public List<TriggerRegistration> Registrations { get; private set; } = new List<TriggerRegistration>();

//        public override void Awake(object initData)
//        {
//            var triggerConfigs = initData as List<TriggerConfig>;
//            if (triggerConfigs == null) return;

//            for (int i = 0; i < triggerConfigs.Count; i++)
//            {
//                var config = triggerConfigs[i];
//                if (config == null || !config.Enabled) continue;

//                var reg = new TriggerRegistration
//                {
//                    Config = config,
//                    StateChecks = TriggerStateCheckHelper.CompileStateChecks(config),
//                };
//                Registrations.Add(reg);
//            }
//        }

//        public override void OnEnable()
//        {
//            var buff = Entity as Buff;
//            if (buff == null || buff.IsDisposed) return;

//            var owner = buff.OwnerEntity;
//            if (owner == null || owner.IsDisposed) return;

//            var actionPointComp = owner.GetComponent<ActionPointComponent>();
//            if (actionPointComp == null) return;

//            for (int i = 0; i < Registrations.Count; i++)
//            {
//                var reg = Registrations[i];
//                if (reg.Config.AutoTriggerType == EffectAutoTriggerType.Instant)
//                {
//                    FireTrigger(reg, owner, buff, new TriggerContext { Target = owner });
//                    continue;
//                }

//                if (reg.Config.AutoTriggerType == EffectAutoTriggerType.Action)
//                {
//                    reg.Callback = (Entity source) =>
//                    {
//                        if (!buff.Enable || owner.IsDisposed) return;
//                        var ctx = new TriggerContext { TriggerSource = source, Target = source ?? owner };
//                        FireTrigger(reg, owner, buff, ctx);
//                    };
//                    actionPointComp.AddListener(reg.Config.ActionPointType, reg.Callback);
//                }
//            }
//        }

//        public override void OnDisable()
//        {
//            var buff = Entity as Buff;
//            if (buff == null || buff.IsDisposed) return;

//            var owner = buff.OwnerEntity;
//            if (owner == null || owner.IsDisposed) return;

//            var actionPointComp = owner.GetComponent<ActionPointComponent>();
//            if (actionPointComp == null) return;

//            for (int i = 0; i < Registrations.Count; i++)
//            {
//                var reg = Registrations[i];
//                if (reg.Config.AutoTriggerType == EffectAutoTriggerType.Action && reg.Callback != null)
//                {
//                    actionPointComp.RemoveListener(reg.Config.ActionPointType, reg.Callback);
//                    reg.Callback = null;
//                }
//            }
//        }

//        public override void OnDestroy()
//        {
//            var buff = Entity as Buff;
//            if (buff != null && !buff.IsDisposed)
//            {
//                var owner = buff.OwnerEntity;
//                if (owner != null && !owner.IsDisposed)
//                {
//                    var actionPointComp = owner.GetComponent<ActionPointComponent>();
//                    if (actionPointComp != null)
//                    {
//                        for (int i = 0; i < Registrations.Count; i++)
//                        {
//                            var reg = Registrations[i];
//                            if (reg.Callback != null)
//                            {
//                                actionPointComp.RemoveListener(reg.Config.ActionPointType, reg.Callback);
//                            }
//                        }
//                    }
//                }
//            }

//            for (int i = 0; i < Registrations.Count; i++)
//            {
//                var reg = Registrations[i];
//                reg.Config = null;
//                reg.StateChecks?.Clear();
//                reg.Callback = null;
//            }
//            Registrations.Clear();
//        }

//        private void FireTrigger(TriggerRegistration reg, CombatEntity owner, Buff buff, TriggerContext context)
//        {
//            var target = context.Target ?? context.TriggerSource ?? (Entity)owner;
//            if (!TriggerStateCheckHelper.CheckTargetState(reg.StateChecks, owner, target))
//                return;

//            if (owner.IsDisposed)
//                return;

//            var effectIds = reg.Config.EffectIds;

//            if (effectIds == null || effectIds.Count == 0)
//            {
//                // 空列表=不触发任何效果（避免误触发“全部效果”，因为 Buff 侧没有统一的效果集合）。
//                return;
//            }

//            for (int ei = 0; ei < effectIds.Count; ei++)
//            {
//                var effectId = effectIds[ei];
//                if (effectId <= 0) continue;
//                var setting = SkillSettingMgr.Instance.GetBuffModifySetting(effectId);
//                if (setting == null) continue;
//                ExecuteModifySetting(setting, owner, buff, target);
//            }
//        }

//        private void ExecuteModifySetting(BuffModifySetting setting, CombatEntity owner, Buff buff, Entity target)
//        {
//            // Buff 被动触发：目标由外部传入（target），效果类型按 setting.EffectModifyType 分发。
//            // 注意：这里不处理需要生命周期撤销的类型（如 PlayerModify/PlayerControll/ActionModify），避免产生不可控的永久效果。
//            if (setting.EffectModifyType == EffectModifyType.BuffHpDamage)
//            {
//                if (buff.Caster is CombatEntity caster)
//                    BuffModifyExecutionCore.ExecuteHpDamage(setting, caster, target, buff, DamageSource.Buff);
//                return;
//            }
//            if (setting.EffectModifyType == EffectModifyType.BuffResource)
//            {
//                if (buff.Caster is CombatEntity caster)
//                    BuffModifyExecutionCore.ExecuteResource(setting, caster, target, buff, DamageSource.Buff);
//                return;
//            }
//            if (setting.EffectModifyType == EffectModifyType.BuffAddStatus)
//            {
//                // 复用 BuffModifyProcessorTable 的实现（包含 AddStatusAction 复用）
//                BuffModifyProcessorTable.ApplyOnTrigger(new ModifyRegistration { Config = setting }, buff, target);
//                return;
//            }
//        }
//    }
//}

