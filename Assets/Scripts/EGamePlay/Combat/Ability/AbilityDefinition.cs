using System.Collections.Generic;

namespace EGamePlay.Combat
{
    public class AbilityDefinition
    {
        public int SkillID { get; private set; }
        /// <summary>
        /// 命中额外效果（上 Buff、回能等）。由 SkillDemoSetting.EffectIds 解析。
        /// 主动技伤害走段表，不在这里挂 SkillHpDamage。盒上 EffectIds 为空只出该段伤害。
        /// </summary>
        public List<BuffModifySetting> EffectModifyEffects { get; private set; } = new List<BuffModifySetting>();
        public SkillDemoSetting Config { get; private set; }
        public string Name { get; private set; }

        /// <summary>是否需要 ACT 层时间轴资产（主动技能）。</summary>
        public bool RequiresTimeline { get; private set; }

        public static AbilityDefinition Load(int skillId)
        {
            var config = SkillSettingMgr.Instance.GetSkillDemoSettingOrNull(skillId);
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
            if (effectIds != null)
            {
                for (int i = 0; i < effectIds.Count; i++)
                {
                    var effectId = effectIds[i];
                    if (effectId <= 0)
                        continue;

                    var setting = SkillSettingMgr.Instance.GetBuffModifySettingOrNull(effectId);
                    if (setting != null)
                        definition.EffectModifyEffects.Add(setting);
                }
            }

            return definition;
        }
    }

    /// <summary>
    /// 技能定义管理器：按 skillId 加载并缓存 AbilityDefinition，全局单例。
    /// 同一技能只加载一次，多个 Entity 释放相同技能时共用定义。
    /// </summary>
    public class AbilityDefinitionManager
    {
        private static AbilityDefinitionManager _instance;
        public static AbilityDefinitionManager Instance => _instance ??= new AbilityDefinitionManager();
        private readonly Dictionary<int, AbilityDefinition> _definitions = new Dictionary<int, AbilityDefinition>(64);
        private AbilityDefinitionManager() { }

        /// <summary>获取或加载指定技能的定义，失败返回 null。</summary>
        public AbilityDefinition GetOrLoad(int skillId)
        {
            if (_definitions.TryGetValue(skillId, out var def)) return def;

            def = AbilityDefinition.Load(skillId);
            if (def != null)
                _definitions[skillId] = def;
            return def;
        }

        /// <summary>是否已缓存指定技能。</summary>
        public bool HasDefinition(int skillId) => _definitions.ContainsKey(skillId);

        /// <summary>预加载若干技能，常用于角色初始化。</summary>
        public void Preload(params int[] skillIds)
        {
            foreach (var id in skillIds)
            {
                if (id <= 0) continue;
                if (!_definitions.ContainsKey(id))
                    GetOrLoad(id);
            }
        }

        /// <summary>清除缓存（场景切换等）。</summary>
        public void Clear()
        {
            _definitions.Clear();
        }

        /// <summary>销毁单例（场景切换等）。</summary>
        public static void DestroyInstance()
        {
            if (_instance != null)
            {
                _instance.Clear();
                _instance = null;
            }
        }
    }
}
