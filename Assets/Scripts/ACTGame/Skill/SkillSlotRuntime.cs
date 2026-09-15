using System.Collections.Generic;
using ACTGameEditor.Combat;
using EGamePlay;
using EGamePlay.Combat;

namespace ACTGameEditor
{
    /// <summary>
    /// 逻辑槽位 ID。输入缓冲仍按下标覆盖；技能 Id 来自 <c>CharacterSlot</c> 表，不再用 Chain/Parry 当战斗键。
    /// </summary>
    public enum SkillSlotId
    {
        NormalAttack = 0,
        Skill1 = 1,
        Skill2 = 2,
        Skill3 = 3,
        Ultimate = 4,
        Dodge = 5,
        /// <summary>已废弃：连携走 Kit.ChainSkillId + Z。</summary>
        Chain = 6,
        /// <summary>已废弃：招架走 Kit.DefensiveAssistSkillId + Z。</summary>
        Parry = 7,
    }

    /// <summary>
    /// 槽位运行时：输入绑定 + 角色表。Idle SkillId 在提交时解析，不在加载时写死。
    /// </summary>
    public class SkillSlotRuntime
    {
        readonly Dictionary<SkillSlotId, int> _clickOverrides = new Dictionary<SkillSlotId, int>(8);
        readonly HashSet<int> _attachIds = new HashSet<int>();
        SkillSlotConfig _slotConfig;
        int _characterId;

        /// <summary>当前输入绑定（不含 SkillId）。</summary>
        public SkillSlotConfig SlotConfig => _slotConfig;

        /// <summary>当前加载的角色 Id。</summary>
        public int CharacterId => _characterId;

        /// <summary>4 战斗键输入 → Luban 键。Chain/Parry 返回 false。</summary>
        public static bool TryMapSlotToButton(SkillSlotId slotId, out CombatButton button)
        {
            switch (slotId)
            {
                case SkillSlotId.NormalAttack:
                    button = CombatButton.Attack;
                    return true;
                case SkillSlotId.Skill1:
                    button = CombatButton.Skill;
                    return true;
                case SkillSlotId.Dodge:
                    button = CombatButton.Dodge;
                    return true;
                case SkillSlotId.Ultimate:
                    button = CombatButton.Ultimate;
                    return true;
                default:
                    button = default;
                    return false;
            }
        }

        /// <summary>运行时按法 → 表按法。</summary>
        public static CombatPress MapPress(PressType press)
        {
            return press == PressType.LongPress ? CombatPress.Hold : CombatPress.Click;
        }

        /// <summary>
        /// 按 CharacterId 收集 Kit / 槽位 / Empowered 以 Attach。
        /// 不缓存 Idle SkillId；提交时按行 + Empowered 预检解析。
        /// </summary>
        public void LoadFromTable(SkillSlotConfig slotConfig, int characterId)
        {
            _slotConfig = slotConfig;
            _characterId = characterId;
            _clickOverrides.Clear();
            _attachIds.Clear();

            SkillSettingMgr mgr = SkillSettingMgr.Instance;
            if (mgr == null)
                return;

            mgr.CollectCharacterOwnedSkillIds(characterId, _attachIds);
            SkillTimelineCombo.ExpandFromTimelines(_attachIds);
        }

        /// <summary>指定槽位点按的表内 SkillId（无 Tag、无 Empowered）。未配返回 0。</summary>
        public int GetSkillId(SkillSlotId slotId)
        {
            return GetSkillId(slotId, PressType.Click);
        }

        /// <summary>
        /// 指定槽位 × 按法的表内 SkillId（无 Tag、无 Empowered）。
        /// Hold 无行返回 0，不回退 Click。点按若有 <see cref="SetSkillId"/> 覆盖则返回覆盖值。
        /// </summary>
        public int GetSkillId(SkillSlotId slotId, PressType press)
        {
            if (press != PressType.LongPress
                && _clickOverrides.TryGetValue(slotId, out int forced)
                && forced > 0)
                return forced;

            if (!TryMapSlotToButton(slotId, out CombatButton button))
                return 0;

            SkillSettingMgr mgr = SkillSettingMgr.Instance;
            if (mgr == null)
                return 0;

            return mgr.ResolveCharacterSlotSkill(_characterId, button, MapPress(press), formId: 0);
        }

        /// <summary>无 actor 时：表里是否有无 Tag 的 Hold 行。按下是否延迟点按应走 <see cref="SkillResolver.HasHoldSkill"/>。</summary>
        public bool HasHoldSkill(SkillSlotId slotId)
        {
            return GetSkillId(slotId, PressType.LongPress) > 0;
        }

        /// <summary>点按覆盖。skillId≤0 清空覆盖，仍读表。</summary>
        public void SetSkillId(SkillSlotId slotId, int skillId)
        {
            if (skillId > 0)
            {
                _clickOverrides[slotId] = skillId;
                _attachIds.Add(skillId);
            }
            else
            {
                _clickOverrides.Remove(slotId);
            }
        }

        /// <summary>点按覆盖（不含表内默认行）。</summary>
        public IReadOnlyDictionary<SkillSlotId, int> GetAllSlots() => _clickOverrides;

        /// <summary>点按是否被 <see cref="SetSkillId"/> 钉死（不再走表 / Empowered）。</summary>
        public bool TryGetClickOverride(SkillSlotId slotId, out int skillId)
        {
            return _clickOverrides.TryGetValue(slotId, out skillId) && skillId > 0;
        }

        /// <summary>需要 AttachAbility 的技能（槽位入口 + Empowered + Kit 列 + 轴连招）。</summary>
        public void GetAllSkillIdsToAttach(HashSet<int> outIds)
        {
            outIds.Clear();
            foreach (int id in _attachIds)
            {
                if (id > 0)
                    outIds.Add(id);
            }
        }
    }

    /// <summary>
    /// Idle 槽位 → 技能。形态 SO × 地面/空中优先，否则角色槽位行 + Empowered 预检。
    /// </summary>
    public static class SkillResolver
    {
        /// <summary>
        /// 解析 Idle 时该槽位点按应对应的技能 ID。无法解析时返回 0。
        /// </summary>
        public static int ResolveIdle(CombatEntity actor, SkillSlotRuntime slots, SkillSlotId slotId)
        {
            return ResolveIdle(actor, slots, slotId, PressType.Click);
        }

        /// <summary>
        /// 按按法解析 Idle 入口。无匹配行返回 0，Hold 不回退 Click。
        /// 命中行后：Empowered 预检通过则打 Empowered，否则打该行 SkillId。
        /// </summary>
        public static int ResolveIdle(
            CombatEntity actor,
            SkillSlotRuntime slots,
            SkillSlotId slotId,
            PressType press)
        {
            if (press != PressType.LongPress
                && slots != null
                && slots.TryGetClickOverride(slotId, out int forced))
                return forced;

            if (press == PressType.Click && actor != null && !actor.IsDisposed)
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

            CharacterSlotSetting row = GetSlotRow(actor, slots, slotId, press);
            return ResolveFromRow(actor, row);
        }

        /// <summary>
        /// 当前是否有可匹配的 Hold 行（含 Tag / Form）。
        /// 没有则按下立刻点按，不要等长按阈值。
        /// </summary>
        public static bool HasHoldSkill(CombatEntity actor, SkillSlotRuntime slots, SkillSlotId slotId)
        {
            return GetSlotRow(actor, slots, slotId, PressType.LongPress) != null;
        }

        /// <summary>
        /// 同行 Empowered 改写：预检通过用 Empowered，否则用 SkillId。
        /// 跨按法不回退。
        /// </summary>
        public static int ResolveFromRow(ICombatUnit actor, CharacterSlotSetting row)
        {
            if (row == null || row.SkillId <= 0)
                return 0;

            int empowered = row.EmpoweredSkillId;
            if (empowered > 0 && AbilityActivationGate.PassesEmpoweredPreview(actor, empowered))
                return empowered;

            return row.SkillId;
        }

        static CharacterSlotSetting GetSlotRow(
            CombatEntity actor,
            SkillSlotRuntime slots,
            SkillSlotId slotId,
            PressType press)
        {
            if (slots == null || !SkillSlotRuntime.TryMapSlotToButton(slotId, out CombatButton button))
                return null;

            SkillSettingMgr mgr = SkillSettingMgr.Instance;
            if (mgr == null)
                return null;

            int formId = 0;
            if (actor != null && !actor.IsDisposed)
                formId = actor.FormComponent != null ? actor.FormComponent.ActiveFormId : 0;

            return mgr.GetCharacterSlotRow(
                slots.CharacterId,
                button,
                SkillSlotRuntime.MapPress(press),
                formId,
                actor);
        }
    }
}
