using System.Collections.Generic;

namespace EGamePlay.Combat
{
    /// <summary>段表查询辅助，不随 Luban 生成覆盖。</summary>
    public sealed partial class SkillDamageSetting
    {
        /// <summary>
        /// 取指定技能等级的伤害系数。等级小于 1 按 1；超过数组长度用最后一档。无配置返回 0。
        /// </summary>
        public float GetRatioAtLevel(int level)
        {
            List<float> list = RatioByLevel;
            if (list == null || list.Count == 0)
                return 0f;
            if (level < 1)
                level = 1;
            int idx = level - 1;
            if (idx >= list.Count)
                idx = list.Count - 1;
            return list[idx];
        }
    }
}
