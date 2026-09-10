using EGamePlay;
using EGamePlay.Combat;

namespace ACTGameEditor.Combat
{
    /// <summary>
    /// 抗打断合成：站立基数 + UnStopped 加成。命中后只把两个 int 交给 <see cref="CombatInterrupt"/>。
    /// </summary>
    public sealed class CombatPoiseComponent : Component
    {
        CombatEntity _owner;
        int _unstoppedIndex;

        /// <summary>站立抗打断。玩家/杂兵 1，精英 3。</summary>
        public int BaseLevel = CombatInterrupt.DefaultBaseAnti;

        /// <summary>有 <see cref="CombatTags.BuffUnStopped"/> 时叠加。不是布尔免疫。</summary>
        public int SuperArmorBonus = CombatInterrupt.DefaultSuperArmorBonus;

        public override void Awake()
        {
            _owner = GetEntity<CombatEntity>();
            _unstoppedIndex = TagCollection.TagToIndexDic[CombatTags.BuffUnStopped];
            _owner.ListenActionPoint(ActionPointType.PostReceiveDamage, OnPostReceiveDamage);
        }

        public override void OnDestroy()
        {
            if (_owner != null)
                _owner.UnListenActionPoint(ActionPointType.PostReceiveDamage, OnPostReceiveDamage);
            _owner = null;
            _unstoppedIndex = 0;
        }

        public override void OnReset()
        {
            BaseLevel = CombatInterrupt.DefaultBaseAnti;
            SuperArmorBonus = CombatInterrupt.DefaultSuperArmorBonus;
        }

        /// <summary>生成时按档写入；缺档回退杂兵 1 / 精英 3。</summary>
        public void Configure(int baseLevel, int superArmorBonus, bool elite)
        {
            BaseLevel = baseLevel > 0
                ? baseLevel
                : (elite ? CombatInterrupt.DefaultEliteAnti : CombatInterrupt.DefaultBaseAnti);
            SuperArmorBonus = superArmorBonus >= 0
                ? superArmorBonus
                : CombatInterrupt.DefaultSuperArmorBonus;
        }

        /// <summary>当前抗打断。热路径只读 Tag 索引，不扫 Buff。</summary>
        public int GetAntiInterruptLevel()
        {
            int anti = BaseLevel > 0 ? BaseLevel : CombatInterrupt.DefaultBaseAnti;
            if (_owner?.TagHost != null && _owner.TagHost.HasIndex(_unstoppedIndex))
                anti += SuperArmorBonus;
            return anti;
        }

        void OnPostReceiveDamage(Entity action)
        {
            if (action is not DamageAction damage || _owner == null)
                return;
            if (damage.DamageActionEffect.HasFlag(DamageActionEffect.Dodge)
                || damage.DamageActionEffect.HasFlag(DamageActionEffect.Immunity)
                || damage.DamageActionEffect.HasFlag(DamageActionEffect.Interrupt)
                || damage.DamageActionEffect.HasFlag(DamageActionEffect.Parry))
                return;
            if (_owner.IsDead)
                return;
            if (_owner.StateDirector != null && _owner.StateDirector.IsStagger)
                return;
            if (!CombatInterrupt.ShouldBreakSkill(damage, GetAntiInterruptLevel()))
                return;

            _owner.TryApplyHitReaction(damage.Id, 0.35f);
        }
    }
}
