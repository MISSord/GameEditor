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
    }
}