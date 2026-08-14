using System.Collections.Generic;

namespace EGamePlay.Combat
{
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
