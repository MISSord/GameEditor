using EGamePlay.Combat;
using UnityEngine;

namespace ACTGameEditor.Combat.Ai
{
    /// <summary>杂兵大脑调参。C 写死的常量抽到这里，和 MoveSet 一起给 D 用。</summary>
    [CreateAssetMenu(menuName = "ACT/AI/Enemy Brain Profile", fileName = "EnemyBrainProfile")]
    public sealed class EnemyBrainProfile : ScriptableObject
    {
        public const string ResourcePath = "Config/Ai/DefaultGruntBrain";

        /// <summary>精英档路径（EnemyA 模型位）。</summary>
        public const string EliteResourcePath = "Config/Ai/DefaultEliteBrain";

        public float AggroRadius = 20f;
        public float AlertDelayFirst = 0.35f;
        public float AlertDelayRepeat = 0.1f;
        public float KeepDistance = 3.6f;
        public float KeepDeadzone = 0.35f;
        public float OrbitLeaveSlack = 1.2f;
        public float AcquirePadding = 0.8f;
        public float RecoverSeconds = 0.65f;
        public float SelectInterval = 0.1f;

        [Tooltip("精英档：可申请 Special 牌、Orbit 时放假前摇")]
        public bool IsElite;
        [Tooltip("假前摇最小间隔（秒，世界钟）；0 = 不放")]
        public float FeintInterval = 3f;

        [Tooltip("站立抗打断。杂兵 1，精英 3。0 = 按档位默认")]
        public int AntiInterruptBase = 1;
        [Tooltip("有 Buff.UnStopped 时叠加的抗打断，不是布尔免疫")]
        public int SuperArmorBonus = 3;

        [Tooltip("欲望增速额外项（/秒）。精英拉开车轮战权重，杂兵保持 0")]
        public float DesireBonusPerSec = 0f;

        public EnemyMoveSet MoveSet;

        /// <summary>按档位读 Resources；精英缺档回退杂兵档，再缺则运行时拼回退，避免生成失败。</summary>
        public static EnemyBrainProfile LoadForArchetype(bool elite, out bool ownsFallback)
        {
            if (elite)
            {
                EnemyBrainProfile eliteProfile = Resources.Load<EnemyBrainProfile>(EliteResourcePath);
                if (eliteProfile != null)
                {
                    ownsFallback = false;
                    return eliteProfile;
                }
            }

            EnemyBrainProfile loaded = Resources.Load<EnemyBrainProfile>(ResourcePath);
            if (loaded != null)
            {
                ownsFallback = false;
                return loaded;
            }

            ownsFallback = true;
            EnemyBrainProfile profile = CreateInstance<EnemyBrainProfile>();
            profile.ApplyGruntDefaults();
            profile.MoveSet = EnemyMoveSet.CreateGruntFallback();
            return profile;
        }

        /// <summary>写入 §16 杂兵初值。</summary>
        public void ApplyGruntDefaults()
        {
            AggroRadius = 20f;
            AlertDelayFirst = 0.35f;
            AlertDelayRepeat = 0.1f;
            KeepDistance = 3.6f;
            KeepDeadzone = 0.35f;
            OrbitLeaveSlack = 1.2f;
            AcquirePadding = 0.8f;
            RecoverSeconds = 0.65f;
            SelectInterval = 0.1f;
            IsElite = false;
            FeintInterval = 3f;
            AntiInterruptBase = CombatInterrupt.DefaultBaseAnti;
            SuperArmorBonus = CombatInterrupt.DefaultSuperArmorBonus;
            DesireBonusPerSec = 0f;
        }
    }
}
