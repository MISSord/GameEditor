using System.Collections.Generic;
using EGamePlay;

namespace EGamePlay.Combat
{
    public class AbilityDefinition
    {
        public int SkillID { get; private set; }
        /// <summary>
        /// 技能效果行（表化）：由 SkillDemoSetting.EffectIds 解析为 BuffModifySetting 列表。
        /// 约定：XCEventData.EffectIds 存 BuffModifySetting.EffectModifyID（空列表表示触发全部）。
        /// </summary>
        public List<BuffModifySetting> EffectModifyEffects { get; private set; } = new List<BuffModifySetting>();
        public SkillDemoSetting Config { get; private set; }
        public string Name { get; private set; }

        /// <summary>是否需要 ACT 层时间轴资产（主动技能）。</summary>
        public bool RequiresTimeline { get; private set; }

        public static AbilityDefinition Load(int skillId)
        {
            var config = SkillSettingMgr.Instance.GetSkillDemoSetting(skillId);
            if (config == null) return null;

            var definition = new AbilityDefinition
            {
                SkillID = skillId,
                Config = config,
                Name = config.Name,
                RequiresTimeline = config.Type == AbilityType.ActiveSkill.ToString(),
            };

            definition.EffectModifyEffects.Clear();
            var effectIds = config.EffectIds;
            if (effectIds != null && effectIds.Count > 0)
            {
                for (int i = 0; i < effectIds.Count; i++)
                {
                    var effectId = effectIds[i];
                    if (effectId <= 0) continue;

                    var setting = SkillSettingMgr.Instance.GetBuffModifySetting(effectId);
                    if (setting != null)
                        definition.EffectModifyEffects.Add(setting);
                }
            }
            else if (SkillSettingMgr.Instance.HasSkillDamageConfig(skillId))
            {
                // 技能表未配 EffectIds 但伤害表有段配置时，回退到默认 SkillHpDamage 行。
                var fallback = SkillSettingMgr.Instance.GetBuffModifySetting(SkillSettingMgr.DefaultSkillHpDamageEffectId);
                if (fallback != null)
                    definition.EffectModifyEffects.Add(fallback);
            }

            return definition;
        }
    }
}
