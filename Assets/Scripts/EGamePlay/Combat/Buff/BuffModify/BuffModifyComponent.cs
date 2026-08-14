using System;
using System.Collections.Generic;
using XiaoCao;

namespace EGamePlay.Combat
{
    /// <summary>
    /// BuffModify 注册数据：配置 + 运行时状态（用于 OnDisable 时撤销）。
    /// </summary>
    public sealed class ModifyRegistration
    {
        public BuffModifySetting Config { get; set; }
        public FloatModifier AttributeModifier { get; set; }
        public bool AttributeApplied { get; set; }
        public bool ControlApplied { get; set; }
        public Action<Entity> ActionCallback { get; set; }
    }

    /// <summary>
    /// Buff 修饰组件：持有 ModifyRegistration[]，按 Processor 表执行，不创建 BuffModify 子 Entity。
    /// </summary>
    public class BuffModifyComponent : Component
    {
        public List<ModifyRegistration> Registrations { get; private set; } = new List<ModifyRegistration>();

        public override void Awake()
        {
            var buff = GetEntity<Buff>();
            if (buff?.Setting?.BuffModifyList == null) return;

            BuffModifySetting controllModAdded = null;
            foreach (int modId in buff.Setting.BuffModifyList)
            {
                var setting = SkillSettingMgr.Instance.GetBuffModifySetting(modId);
                if (setting == null) continue;

                if (setting.EffectModifyType == EffectModifyType.PlayerControll && controllModAdded != null)
                    continue;

                var reg = new ModifyRegistration { Config = setting };
                Registrations.Add(reg);

                if (setting.EffectModifyType == EffectModifyType.PlayerControll)
                    controllModAdded = setting;
            }
        }

        public override void OnEnable()
        {
            var buff = GetEntity<Buff>();
            if (buff == null || buff.IsDisposed) return;

            foreach (var reg in Registrations)
            {
                //if (reg.Config.EffectModifyType == EffectModifyType.ActionModify)
                //    BuffModifyProcessorTable.RegisterActionModify(reg, buff);
            }
        }

        public override void OnDisable()
        {
            var buff = GetEntity<Buff>();
            if (buff == null) return;

            foreach (var reg in Registrations)
            {
                //if (reg.Config.EffectModifyType == EffectModifyType.ActionModify)
                //    BuffModifyProcessorTable.UnregisterActionModify(reg, buff);
                BuffModifyProcessorTable.RevertOnDisable(reg, buff);
            }
        }

        public void OnTriggerModify(Entity target)
        {
            var buff = GetEntity<Buff>();
            if (buff == null || buff.IsDisposed) return;

            foreach (var reg in Registrations)
            {
                if (reg.Config.EffectModifyType == EffectModifyType.PlayerControll ||
                    reg.Config.EffectModifyType == EffectModifyType.PlayerModify ||
                    reg.Config.EffectModifyType == EffectModifyType.BuffHpDamage ||
                    reg.Config.EffectModifyType == EffectModifyType.BuffResource ||
                    reg.Config.EffectModifyType == EffectModifyType.BuffAddStatus ||
                    reg.Config.EffectModifyType == EffectModifyType.DamageEffect)
                    BuffModifyProcessorTable.ApplyOnTrigger(reg, buff, target);
            }
        }

        public override void OnDestroy()
        {
            var buff = GetEntity<Buff>();
            if (buff != null)
            {
                foreach (var reg in Registrations)
                {
                    //if (reg.Config?.EffectModifyType == EffectModifyType.ActionModify && reg.ActionCallback != null)
                    //    BuffModifyProcessorTable.UnregisterActionModify(reg, buff);
                    BuffModifyProcessorTable.RevertOnDisable(reg, buff);
                }
            }
            foreach (var reg in Registrations)
            {
                reg.Config = null;
                reg.AttributeModifier = null;
                reg.ActionCallback = null;
            }
            Registrations?.Clear();
        }
    }
}
