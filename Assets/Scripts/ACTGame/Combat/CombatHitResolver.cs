using EGamePlay;
using EGamePlay.Combat;

namespace ACTGameEditor.Combat
{
    /// <summary>
    /// 命中裁决：PreReceiveDamage 等行动点修改 <see cref="DamageActionEffect"/>（闪避/免疫等）。
    /// 不含表现；替代原 <c>CombatRollPresentation</c>。
    /// </summary>
    public sealed class CombatHitResolver : Component
    {
        CombatEntity _owner;
        int _rollTagIndex;

        public override void Awake()
        {
            _owner = GetEntity<CombatEntity>();
            _rollTagIndex = TagCollection.TagToIndexDic[CombatTags.BuffRoll];
            _owner.ListenActionPoint(ActionPointType.PreReceiveDamage, OnPreReceiveDamage);
        }

        public override void OnDestroy()
        {
            if (_owner != null)
                _owner.UnListenActionPoint(ActionPointType.PreReceiveDamage, OnPreReceiveDamage);
            _owner = null;
            _rollTagIndex = 0;
        }

        public override void OnReset()
        {
            _owner = null;
            _rollTagIndex = 0;
        }

        void OnPreReceiveDamage(Entity action)
        {
            if (action is not DamageAction damage || _owner?.TagHost == null)
                return;

            if (!_owner.TagHost.HasIndex(_rollTagIndex))
                return;

            damage.DamageActionEffect |= DamageActionEffect.Dodge;
        }
    }
}
