using System.Collections.Generic;
using ACTGameEditor;
using EGamePlay;

namespace EGamePlay.Combat
{
    public class AbilityDefinition
    {
        public int SkillID { get; private set; }
        /// <summary>
        /// 技能效果行（表化）：由 SkillDemoSetting.EffectIds / InlineBuffEffectIds 解析为 BuffModifySetting 列表。
        /// 约定：XCEventData.EffectIds 存 BuffModifySetting.EffectModifyID（空列表表示触发全部）。
        /// </summary>
        public List<BuffModifySetting> EffectModifyEffects { get; private set; } = new List<BuffModifySetting>();
        public SkillDemoSetting Config { get; private set; }
        public SkillAllEventData SkillData { get; private set; }
        public string Name { get; private set; }

        public static AbilityDefinition Load(int skillId)
        {
            var config = SkillSettingMgr.Instance.GetSkillDemoSetting(skillId);
            if (config == null) return null;

            SkillAllEventData skillData = null;
            if (config.Type == AbilityType.ActiveSkill.ToString())
            {
                skillData = AssetBundleManager.Instance.LoadAssetSync<SkillAllEventData>(
                    PrefabPath.GetSkillDataScriObjPath(false), config.SkillId.ToString());
            }

            var definition = new AbilityDefinition
            {
                SkillID = skillId,
                Config = config,
                SkillData = skillData,
                Name = config.Name,
            };

            // 预构建技能效果行（表化）：EffectIds 直接引用 BuffModifySetting.EffectModifyID。
            definition.EffectModifyEffects.Clear();
            var effectIds = config.EffectIds;
            if (effectIds != null)
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

            //var inlineIds = config.InlineBuffEffectIds;
            //if (inlineIds != null && inlineIds.Count > 0)
            //{
            //    for (int i = 0; i < inlineIds.Count; i++)
            //    {
            //        var id = inlineIds[i];
            //        if (id <= 0) continue;
            //        var setting = SkillSettingMgr.Instance.GetBuffModifySetting(id);
            //        if (setting != null)
            //        {
            //            // 去重：避免同一 EffectModifyID 被重复加入
            //            bool exists = false;
            //            for (int j = 0; j < definition.EffectModifyEffects.Count; j++)
            //            {
            //                if (definition.EffectModifyEffects[j].EffectModifyID == setting.EffectModifyID)
            //                {
            //                    exists = true;
            //                    break;
            //                }
            //            }
            //            if (!exists)
            //                definition.EffectModifyEffects.Add(setting);
            //        }
            //    }
            //}

            return definition;
        }
    }
}
