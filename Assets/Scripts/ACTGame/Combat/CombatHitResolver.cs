using ACTGameEditor.Combat.Ai;
using EGamePlay;
using EGamePlay.Combat;

namespace ACTGameEditor.Combat
{
    /// <summary>
    /// 命中裁决：PreReceiveDamage 修改 <see cref="DamageActionEffect"/>（招架/闪避/免疫）。
    /// 招架成功在按键成交时已结算；这里只消后续盒，不含表现。
    /// </summary>
    public sealed class CombatHitResolver : Component
    {
        CombatEntity _owner;
        int _rollTagIndex;
        int _dazeRecoverIndex;
        int _parryWindowIndex;

        public override void Awake()
        {
            _owner = GetEntity<CombatEntity>();
            _rollTagIndex = TagCollection.TagToIndexDic[CombatTags.BuffRoll];
            _dazeRecoverIndex = TagCollection.TagToIndexDic[CombatTags.CombatDazeRecover];
            _parryWindowIndex = TagCollection.TagToIndexDic[CombatTags.CombatParryWindow];
            _owner.ListenActionPoint(ActionPointType.PreReceiveDamage, OnPreReceiveDamage);
        }

        public override void OnDestroy()
        {
            if (_owner != null)
                _owner.UnListenActionPoint(ActionPointType.PreReceiveDamage, OnPreReceiveDamage);
            _owner = null;
            _rollTagIndex = 0;
            _dazeRecoverIndex = 0;
            _parryWindowIndex = 0;
        }

        public override void OnReset()
        {
            _owner = null;
            _rollTagIndex = 0;
            _dazeRecoverIndex = 0;
            _parryWindowIndex = 0;
        }

        void OnPreReceiveDamage(Entity action)
        {
            if (action is not DamageAction damage || _owner?.TagHost == null)
                return;

            if (_owner.TagHost.HasIndex(_dazeRecoverIndex))
            {
                damage.DamageActionEffect |= DamageActionEffect.Immunity;
                return;
            }

            if (damage.Creator is CombatEntity attacker && CombatParry.IsConsumedHit(attacker, _owner))
            {
                damage.DamageActionEffect |= DamageActionEffect.Parry;
                return;
            }

            if (_owner.TagHost.HasIndex(_parryWindowIndex))
            {
                damage.DamageActionEffect |= DamageActionEffect.Immunity;
                return;
            }

            if (!_owner.TagHost.HasIndex(_rollTagIndex))
                return;

            damage.DamageActionEffect |= DamageActionEffect.Dodge;
            if (_owner.isTruePlayer && damage.Creator != null && damage.Creator.Id != _owner.Id)
                CombatEncounterDirector.Instance?.NotifyPlayerDodge(perfect: true);
        }
    }
}
