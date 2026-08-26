using UnityEngine;

namespace EGamePlay.Combat
{
    /// <summary>一次出手意图（纯数据）。</summary>
    public struct CombatCastIntent
    {
        public int SkillId;
        public int Sort;
        public ICombatUnit Target;
        public Vector3 Point;
        public Vector3 Direction;
    }
}
