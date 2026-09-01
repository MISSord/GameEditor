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
            BuffModifySetting res = CurrentTable.BuffModifyReader.GetOrDefault(id);
            if (res == null)
            {
                res = CurrentTable.BuffModifyReader.DataList[0];
                Debug.Log($"yns GetDefaut skillSetting ");
            }
            return res;
        }

        /// <summary>获取技能某一段伤害配置；未找到则返回 null。</summary>
        public SkillDamageSetting GetSkillDamageSetting(int skillId, int segmentIndex)
        {
            if (skillId <= 0 || segmentIndex <= 0) return null;
            var reader = CurrentTable.SkillDamageReader;
            var list = reader.DataList;
            for (int i = 0; i < list.Count; i++)
            {
                var setting = list[i];
                if (setting.SkillId == skillId && setting.SegmentIndex == segmentIndex)
                    return setting;
            }
            return null;
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

        /// <summary>默认技能内联 HP 伤害行（buffmodifyreader EffectModifyID=8）。</summary>
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