using System.Collections.Generic;
using ACTGameEditor.Combat;
using EGamePlay.Combat;

namespace ACTGameEditor
{
    /// <summary>
    /// 逻辑槽位 ID，崩坏3 风格。
    /// 同一槽位可随角色/武器放置不同技能。
    /// </summary>
    public enum SkillSlotId
    {
        NormalAttack = 0,
        Skill1 = 1,
        Skill2 = 2,
        Skill3 = 3,
        Ultimate = 4,
        Dodge = 5,
    }

    /// <summary>
    /// 槽位运行时：实体当前 SlotId → SkillId 映射。
    /// 换角色/武器时 LoadFromCharacter，或手动 SetSkillId。
    /// </summary>
    public class SkillSlotRuntime
    {
        private readonly Dictionary<SkillSlotId, int> _slotToSkill = new Dictionary<SkillSlotId, int>(16);
        private SkillSlotConfig _slotConfig;
        private CharacterSlotConfig _charConfig;

        /// <summary>当前槽位配置（用于取默认技能）</summary>
        public SkillSlotConfig SlotConfig => _slotConfig;

        /// <summary>当前角色槽位配置</summary>
        public CharacterSlotConfig CharacterConfig => _charConfig;

        /// <summary>
        /// 初始化：加载槽位配置与角色覆盖。
        /// </summary>
        public void Load(SkillSlotConfig slotConfig, CharacterSlotConfig charConfig = null)
        {
            _slotConfig = slotConfig;
            _charConfig = charConfig;
            _slotToSkill.Clear();

            if (slotConfig == null) return;

            foreach (var entry in slotConfig.Slots)
            {
                int skillId = 0;
                if (charConfig != null)
                    skillId = charConfig.GetSkillId(entry.SlotId);
                if (skillId <= 0)
                    skillId = entry.DefaultSkillId;
                if (skillId > 0)
                    _slotToSkill[entry.SlotId] = skillId;
            }
        }

        /// <summary>仅加载角色覆盖，保留已有槽位配置。</summary>
        public void LoadCharacter(CharacterSlotConfig charConfig)
        {
            _charConfig = charConfig;
            if (_slotConfig == null) return;

            foreach (var entry in _slotConfig.Slots)
            {
                int skillId = charConfig != null ? charConfig.GetSkillId(entry.SlotId) : 0;
                if (skillId <= 0) skillId = entry.DefaultSkillId;
                if (skillId > 0)
                    _slotToSkill[entry.SlotId] = skillId;
            }
        }

        /// <summary>获取指定槽位当前技能 ID，未配置则尝试默认值。</summary>
        public int GetSkillId(SkillSlotId slotId)
        {
            if (_slotToSkill.TryGetValue(slotId, out int id)) return id;
            var entry = _slotConfig?.FindBySlot(slotId);
            return entry?.DefaultSkillId ?? 0;
        }

        /// <summary>设置槽位技能（运行时替换/强化）。</summary>
        public void SetSkillId(SkillSlotId slotId, int skillId)
        {
            if (skillId > 0)
                _slotToSkill[slotId] = skillId;
            else
                _slotToSkill.Remove(slotId);
        }

        /// <summary>获取所有已配置槽位及其当前技能 ID。</summary>
        public IReadOnlyDictionary<SkillSlotId, int> GetAllSlots() => _slotToSkill;

        /// <summary>
        /// 获取需要 AttachAbility 的所有技能 ID（含技能链）。
        /// 角色有覆盖时优先用 CharacterSlotConfig 的 ComboSkillIds，否则用 SlotConfig 的。
        /// </summary>
        public void GetAllSkillIdsToAttach(HashSet<int> outIds)
        {
            outIds.Clear();
            if (_slotConfig == null) return;

            foreach (var entry in _slotConfig.Slots)
            {
                int entryId = GetSkillId(entry.SlotId);
                if (entryId <= 0) continue;

                outIds.Add(entryId);

                List<int> combo = null;
                if (_charConfig != null)
                    combo = _charConfig.GetComboSkillIds(entry.SlotId);
                if (combo == null && entry.ComboSkillIds != null && entry.ComboSkillIds.Count > 0)
                    combo = entry.ComboSkillIds;

                if (combo != null)
                {
                    for (int i = 0; i < combo.Count; i++)
                    {
                        if (combo[i] > 0) outIds.Add(combo[i]);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Idle 槽位 → 技能。形态 × 地面/空中优先，否则角色槽位表。
    /// </summary>
    public static class SkillResolver
    {
        /// <summary>
        /// 解析 Idle 时该槽位应对应的技能 ID。无法解析时返回 0。
        /// </summary>
        public static int ResolveIdle(CombatEntity actor, SkillSlotRuntime slots, SkillSlotId slotId)
        {
            if (actor != null && !actor.IsDisposed)
            {
                CombatFormComponent form = actor.FormComponent;
                SkillFormConfig config = form?.ActiveForm;
                if (config != null)
                {
                    int formSkillId = config.GetSkillId(slotId, actor.IsAirborne);
                    if (formSkillId > 0)
                        return formSkillId;
                }
            }

            return slots != null ? slots.GetSkillId(slotId) : 0;
        }
    }
}
