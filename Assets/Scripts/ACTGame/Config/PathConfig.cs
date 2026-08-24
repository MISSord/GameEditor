using EGamePlay;
using XiaoCao;

namespace ACTGameEditor
{
    public static class PrefabPath
    {
        public static readonly string Player = "actors/character_prefab";
        public static readonly string EnemyB = "actors/character/enemy_prefab";

        public static readonly string SoundDirPath = "sound_prefab";
        public static readonly string SoundHitDirPath = "sound/hit_prefab";

        public static readonly string SkillDataScriObjPath = "config/skilldatascriptable_prefab";
        public static readonly string SkillDataScriObjEnemyPath = "config/skilldatascriptable/skilldata_enemy_prefab";

        public static readonly string AbilityDataScriObjPath = "config/skilldatainfo_prefab";
        public static readonly string EffectDefinitionPath = "config/effectdefinition_prefab";

        public static readonly string HitEffectPath = "effects/hiteffect_prefab";

        public static readonly string UIBar = "UI/MainUI/UIBar.prefab";

        internal static string SingletonUI(SingletonUIType uiType)
        {
            return "UI/SingletonUI/" + uiType.ToString() + ".prefab";
        }

        public static string GetSkillDataScriObjPath(bool isEnemy = false)
        {
            return isEnemy ? SkillDataScriObjEnemyPath : SkillDataScriObjPath;
        }

        public static (string, string) GetMp3Path(string id)
        {
            return (SoundDirPath, id);
        }

        public static (string, string) GetHitMp3Path(string id)
        {
            return (SoundHitDirPath, id);
        }
    }

    public static class AgentNameExtend
    {
        public static bool IsEnemy(this AgentModelType name)
        {
            return name != AgentModelType.Player;
        }

        public static string GetSkillPath(this AgentModelType name)
        {
            return IsEnemy(name) ? "SkillDataScriptable/SkillData_Enemy/" : "SkillDataScriptable/";
        }
    }
}
