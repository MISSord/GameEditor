using System.Collections.Generic;
using EGamePlay.Combat;
using SimpleJSON;
using UnityEngine;

namespace EGamePlay
{
    public class SkillSettingMgr : MonoSingleton<SkillSettingMgr>
    {
        //是否是Json格式
        public bool isJson = true;

        private Tables _currentTable;

        public Tables CurrentTable
        {
            get
            {
                if (_currentTable == null)
                {
                    _currentTable = new Tables(SkillLoader);
                }
                return _currentTable;
            }
        }

        //这里是提供一个加载器，让Table可以一次性批量加载全部配置表然后去解析
        //目前用的是最新版的Luban 应该是4.5版本
        private JSONNode SkillLoader(string file)
        {
            string path = $"Config/Luban/{file}";
            TextAsset text = Resources.Load<TextAsset>(path);
            return JSON.Parse(text.text);
        }

        //技能读取
        public SkillDemoSetting GetSkillDemoSetting(int id)
        {
            SkillDemoSetting res = CurrentTable.SkillDemoReader.GetOrDefault(id);
            if (res == null)
            {
                res = CurrentTable.SkillDemoReader.DataList[0];
                Debug.Log($"yns GetDefaut skillSetting {id}");
            }
            return res;
        }

        //Buff配置读取
        public BuffDemoSetting GetBuffDemoSetting(int id)
        {
            BuffDemoSetting res = CurrentTable.BuffDemoReader.GetOrDefault(id);
            if (res == null)
            {
                res = CurrentTable.BuffDemoReader.DataList[0];
                Debug.Log($"yns GetDefaut skillSetting ");
            }
            return res;
        }

        //Buff修饰器读取
        public BuffModifySetting GetBuffModifySetting(int id)
        {
            BuffModifySetting res = GetBuffModifySettingOrNull(id);
            if (res == null)
            {
                res = CurrentTable.BuffModifyReader.DataList[0];
                Debug.Log($"yns GetDefaut skillSetting ");
            }
            return res;
        }

        /// <summary>按 Id 取 BuffModify；没有则返回 null，不回退到表内第一行。</summary>
        public BuffModifySetting GetBuffModifySettingOrNull(int id)
        {
            if (id <= 0)
                return null;
            return CurrentTable.BuffModifyReader.GetOrDefault(id);
        }

        /// <summary>按 Id 取技能身份行；没有则返回 null，不回退到表内第一行。</summary>
        public SkillDemoSetting GetSkillDemoSettingOrNull(int id)
        {
            if (id <= 0)
                return null;
            return CurrentTable.SkillDemoReader.GetOrDefault(id);
        }

        /// <summary>技能当前等级：读施法者 <c>SkillLevels</c>；无组件则为 1。</summary>
        public int GetSkillLevel(ICombatUnit caster, int skillId)
        {
            if (skillId <= 0)
                return 1;
            var levels = caster?.SkillLevels;
            return levels != null ? levels.GetLevel(skillId) : 1;
        }

        /// <summary>命中热路径：用 Ability 上已缓存的组 Id / MaxLevel。</summary>
        public int GetSkillLevel(ICombatUnit caster, Ability ability)
        {
            if (ability == null)
                return 1;
            var levels = caster?.SkillLevels;
            return levels != null ? levels.GetLevel(ability) : 1;
        }

        /// <summary>升级组 Id；表里为 0 或找不到行时用 SkillId 自身。</summary>
        public int ResolveSkillGroupId(int skillId)
        {
            if (skillId <= 0)
                return 0;
            var config = GetSkillDemoSettingOrNull(skillId);
            return config != null ? config.ResolvedGroupId : skillId;
        }

        /// <summary>该 SkillId 的等级上限；无行时 10。</summary>
        public int ResolveSkillMaxLevel(int skillId)
        {
            var config = GetSkillDemoSettingOrNull(skillId);
            return config != null ? config.ResolvedMaxLevel : 10;
        }

        /// <summary>升级组内各技能 MaxLevel 的最大者；组内无行时 10。</summary>
        public int ResolveMaxLevelForGroup(int groupId)
        {
            if (groupId <= 0)
                return 10;
            var list = CurrentTable.SkillDemoReader.DataList;
            int max = 0;
            for (int i = 0; i < list.Count; i++)
            {
                var row = list[i];
                if (row.ResolvedGroupId != groupId)
                    continue;
                int rowMax = row.ResolvedMaxLevel;
                if (rowMax > max)
                    max = rowMax;
            }
            return max > 0 ? max : 10;
        }

        /// <summary>小队全局行（Id=1）。缺表返回 null，不回退第一行。</summary>
        public SquadSetting GetSquadSettingOrNull()
        {
            return CurrentTable.SquadSettingReader.GetOrDefault(1);
        }

        /// <summary>失衡档位表。缺 Id 返回 null，不回退第一行。</summary>
        public DazeSetting GetDazeSetting(int id)
        {
            if (id <= 0)
                return null;
            DazeSetting row = CurrentTable.DazeSettingReader.GetOrDefault(id);
            if (row == null || row.Id != id)
                return null;
            return row;
        }

        /// <summary>获取技能某一段伤害配置；未找到则返回 null。</summary>
        public SkillDamageSetting GetSkillDamageSetting(int skillId, int segmentIndex)
        {
            if (skillId <= 0 || segmentIndex <= 0)
                return null;
            return CurrentTable.SkillDamageReader.Get(skillId, segmentIndex);
        }

        /// <summary>技能是否在伤害表中有任意段配置。</summary>
        public bool HasSkillDamageConfig(int skillId)
        {
            if (skillId <= 0) return false;
            var list = CurrentTable.SkillDamageReader.DataList;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].SkillId == skillId)
                    return true;
            }
            return false;
        }

        /// <summary>BuffModify SkillHpDamage 行，仅给非段表临时伤害。主动技命中不再回退到这一行。</summary>
        public const int DefaultSkillHpDamageEffectId = 8;

        /// <summary>当前写死的角色等级，用于属性计算（基础值 + 等级 * 增长值）。</summary>
        public const int DefaultRoleLevel = 1;

        /// <summary>根据角色ID获取角色属性表配置；未找到时返回表内第一条并打日志。</summary>
        public RoleAttriSetting GetRoleAttriSetting(int characterId)
        {
            RoleAttriSetting res = CurrentTable.RoleAttriReader.GetOrDefault(characterId);
            if (res == null)
            {
                res = CurrentTable.RoleAttriReader.DataList[0];
                Debug.Log($"yns GetDefault RoleAttriSetting for characterId {characterId}");
            }
            return res;
        }

        /// <summary>获取指定等级下的角色属性（基础值 + 等级 * 增长值，无 buff 影响）。</summary>
        public RoleAttriAtLevel GetRoleAttriAtLevel(int characterId, int level)
        {
            RoleAttriSetting setting = GetRoleAttriSetting(characterId);
            return new RoleAttriAtLevel(setting, level);
        }

        /// <summary>使用默认等级获取角色当前属性。</summary>
        public RoleAttriAtLevel GetRoleAttriAtDefaultLevel(int characterId)
        {
            return GetRoleAttriAtLevel(characterId, DefaultRoleLevel);
        }

        Dictionary<int, CharacterSlotSetting[]> _slotsByCharacter;

        /// <summary>角色招式包。缺行返回 null，不回退 CharacterId=0 或表第一行。</summary>
        public CharacterKitSetting GetCharacterKitOrNull(int characterId)
        {
            if (characterId <= 0)
                return null;
            return CurrentTable.CharacterKitReader.GetOrDefault(characterId);
        }

        /// <summary>技能分类。缺行返回 <see cref="SkillCategory.None"/>。</summary>
        public SkillCategory GetSkillCategory(int skillId)
        {
            SkillDemoSetting config = GetSkillDemoSettingOrNull(skillId);
            return config != null ? config.SkillCategory : SkillCategory.None;
        }

        /// <summary>
        /// Idle 入口行：同键同按法，先精确 FormId 再 FormId=0，Priority 降序。
        /// 空 RequiredTags 始终可匹配；非空需 <paramref name="actor"/> 持有这些 Tag。
        /// actor 为空时只匹配无 Tag 行。缺行或 SkillId=0 返回 null。
        /// </summary>
        public CharacterSlotSetting GetCharacterSlotRow(
            int characterId,
            CombatButton button,
            CombatPress press,
            int formId,
            ICombatUnit actor)
        {
            if (characterId <= 0)
                return null;

            EnsureCharacterSlotCache();
            if (_slotsByCharacter == null || !_slotsByCharacter.TryGetValue(characterId, out CharacterSlotSetting[] rows) || rows == null)
                return null;

            CharacterSlotSetting bestForm = null;
            CharacterSlotSetting bestDefault = null;
            int bestFormPriority = int.MinValue;
            int bestDefaultPriority = int.MinValue;
            for (int i = 0; i < rows.Length; i++)
            {
                CharacterSlotSetting row = rows[i];
                if (row.Button != button || row.Press != press || row.SkillId <= 0)
                    continue;
                if (!SlotRowTagsMatch(row, actor))
                    continue;

                if (row.FormId == formId)
                {
                    if (row.Priority >= bestFormPriority)
                    {
                        bestFormPriority = row.Priority;
                        bestForm = row;
                    }
                }
                else if (row.FormId == 0)
                {
                    if (row.Priority >= bestDefaultPriority)
                    {
                        bestDefaultPriority = row.Priority;
                        bestDefault = row;
                    }
                }
            }

            return bestForm != null ? bestForm : bestDefault;
        }

        /// <summary>
        /// Idle 入口技能 Id（该行 <c>SkillId</c>，不含 Empowered 改写）。
        /// 不带 actor，只匹配无 Tag 行。缺行返回 0。
        /// </summary>
        public int ResolveCharacterSlotSkill(int characterId, CombatButton button, CombatPress press, int formId)
        {
            CharacterSlotSetting row = GetCharacterSlotRow(characterId, button, press, formId, actor: null);
            return row != null ? row.SkillId : 0;
        }

        static bool SlotRowTagsMatch(CharacterSlotSetting row, ICombatUnit actor)
        {
            if (row.RequiredTags == null || row.RequiredTags.Count == 0)
                return true;
            if (actor == null || actor.IsDisposed)
                return false;
            return actor.CanSpellSkillWithTagLists(row.RequiredTags, blocked: null);
        }

        /// <summary>该角色 Kit 列 + 槽位入口（含 Empowered）。0 不写入。不扫 Combo。</summary>
        public void CollectCharacterOwnedSkillIds(int characterId, HashSet<int> outIds)
        {
            if (outIds == null || characterId <= 0)
                return;

            CharacterKitSetting kit = GetCharacterKitOrNull(characterId);
            if (kit != null)
            {
                AddOwnedSkillId(outIds, kit.CorePassiveSkillId);
                AddOwnedSkillId(outIds, kit.AdditionalAbilitySkillId);
                List<int> extra = kit.ExtraPassiveSkillIds;
                if (extra != null)
                {
                    for (int i = 0; i < extra.Count; i++)
                        AddOwnedSkillId(outIds, extra[i]);
                }

                AddOwnedSkillId(outIds, kit.ChainSkillId);
                AddOwnedSkillId(outIds, kit.HarmonyBreakSkillId);
                AddOwnedSkillId(outIds, kit.QuickAssistSkillId);
                AddOwnedSkillId(outIds, kit.DefensiveAssistSkillId);
                AddOwnedSkillId(outIds, kit.EvasiveAssistSkillId);
                AddOwnedSkillId(outIds, kit.AssistFollowUpSkillId);
            }

            EnsureCharacterSlotCache();
            if (_slotsByCharacter == null || !_slotsByCharacter.TryGetValue(characterId, out CharacterSlotSetting[] rows) || rows == null)
                return;

            for (int i = 0; i < rows.Length; i++)
            {
                AddOwnedSkillId(outIds, rows[i].SkillId);
                AddOwnedSkillId(outIds, rows[i].EmpoweredSkillId);
            }
        }

        static void AddOwnedSkillId(HashSet<int> outIds, int skillId)
        {
            if (skillId > 0)
                outIds.Add(skillId);
        }

        void EnsureCharacterSlotCache()
        {
            if (_slotsByCharacter != null)
                return;

            List<CharacterSlotSetting> list = CurrentTable.CharacterSlotReader.DataList;
            var buckets = new Dictionary<int, List<CharacterSlotSetting>>(8);
            for (int i = 0; i < list.Count; i++)
            {
                CharacterSlotSetting row = list[i];
                if (!buckets.TryGetValue(row.CharacterId, out List<CharacterSlotSetting> bucket))
                {
                    bucket = new List<CharacterSlotSetting>(8);
                    buckets[row.CharacterId] = bucket;
                }

                bucket.Add(row);
            }

            _slotsByCharacter = new Dictionary<int, CharacterSlotSetting[]>(buckets.Count);
            foreach (KeyValuePair<int, List<CharacterSlotSetting>> kv in buckets)
                _slotsByCharacter[kv.Key] = kv.Value.ToArray();
        }
    }
}