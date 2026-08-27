using EGamePlay;
using EGamePlay.Combat;
using UnityEngine;

namespace ACTGameEditor.Combat
{
    /// <summary>
    /// 战斗表现统一路由：ActionPoint / EntityDead → Catalog 规则 → Package → Director。
    /// 替代原 <c>CombatDamagePresentation</c>、<c>CombatDeathPresentation</c>。
    /// </summary>
    public sealed class CombatActionPointFxRouter : EGamePlay.Component
    {
        CombatEntity _owner;
        CombatFxPackageCatalog _catalog;
        bool _deathHandled;

        public override void Awake()
        {
            _owner = GetEntity<CombatEntity>();
            _catalog = CombatFxPackageCatalog.Active;
            _owner.ListenActionPoint(ActionPointType.PostReceiveDamage, OnPostReceiveDamage);
            _owner.ListenActionPoint(ActionPointType.PostCauseDamage, OnPostCauseDamage);
            Entity.Subscribe<EntityDeadEvent>(OnEntityDead);
        }

        public override void OnDestroy()
        {
            Entity.UnSubscribe<EntityDeadEvent>(OnEntityDead);
            if (_owner != null)
            {
                _owner.UnListenActionPoint(ActionPointType.PostReceiveDamage, OnPostReceiveDamage);
                _owner.UnListenActionPoint(ActionPointType.PostCauseDamage, OnPostCauseDamage);
            }
            _owner = null;
            _catalog = null;
        }

        public override void OnReset()
        {
            _deathHandled = false;
            _owner = null;
            _catalog = null;
        }

        void OnPostReceiveDamage(Entity action)
        {
#if UNITY
            if (action is DamageAction damage)
                TryPlayDamageRules(ActionPointType.PostReceiveDamage, damage);
#endif
        }

        void OnPostCauseDamage(Entity action)
        {
#if UNITY
            if (action is DamageAction damage)
                TryPlayDamageRules(ActionPointType.PostCauseDamage, damage);
#endif
        }

        void OnEntityDead(EntityDeadEvent evt)
        {
            if (_deathHandled || evt.DeadEntity != Entity || _owner == null || _owner.IsDisposed)
                return;

            _deathHandled = true;
            PlayDeathPresentation();
        }

        void TryPlayDamageRules(ActionPointType actionPoint, DamageAction damage)
        {
            if (_catalog?.ActionPointRules == null)
                return;

            var context = new CombatFxPlayContext
            {
                Source = CombatFxSourceResolver.FromDamage(damage),
                Owner = _owner,
                ActionTarget = damage.Target,
                ActionCreator = damage.Creator,
            };

            for (int i = 0; i < _catalog.ActionPointRules.Count; i++)
            {
                CombatFxTriggerRuleDefinition rule = _catalog.ActionPointRules[i];
                if (rule.ActionPoint != actionPoint)
                    continue;

                if (actionPoint == ActionPointType.PostCauseDamage && damage.Creator?.Id != _owner.Id)
                    continue;

                CombatFxPackagePlayer.TryPlayActionPointRule(rule, _owner, damage, in context);
            }
        }

        void PlayDeathPresentation()
        {
#if UNITY
            ActPlayer actPlayer = _owner.AttackPlayer;
            if (actPlayer == null || !actPlayer.UseDeathDissolve)
            {
                DisableCombatInteraction();
                DespawnActor();
                return;
            }

            DisableCombatInteraction();

            var context = new CombatFxPlayContext
            {
                Source = CombatFxSource.Entity(_owner.Id),
                Owner = _owner,
                OnComplete = DespawnActor,
                DurationOverride = actPlayer.DeathDissolveDuration,
            };

            if (!CombatFxPackagePlayer.TryPlay(CombatFxPackageId.DeathDissolve, in context))
                DespawnActor();
#else
            DespawnActor();
#endif
        }

        void DisableCombatInteraction()
        {
#if UNITY
            _owner.ChangeInputMoveState(false);

            GameObject root = _owner.RootTransform != null ? _owner.RootTransform.gameObject : null;
            if (root == null)
                return;

            CharacterController controller = root.GetComponent<CharacterController>();
            if (controller != null)
                controller.enabled = false;

            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                    colliders[i].enabled = false;
            }

            Rigidbody rigidbody = root.GetComponent<Rigidbody>();
            if (rigidbody != null)
                rigidbody.isKinematic = true;
#endif
        }

        void DespawnActor()
        {
#if UNITY
            ActPlayer actPlayer = _owner?.AttackPlayer;
            if (actPlayer == null)
                return;

            if (LockSystem.Instance != null && LockSystem.Instance.LockedCombatEntity == _owner)
                LockSystem.Instance.Unlock();

            uint netId = _owner.NetId;
            PlayerManager.Instance?.DisRegisterPlayer(netId);

            if (CombatContext.Instance != null)
                CombatContext.Instance.Object2Entities.Remove(actPlayer.gameObject);

            actPlayer.gameObject.SetActive(false);
            actPlayer.Dispose();
#endif
        }
    }
}
