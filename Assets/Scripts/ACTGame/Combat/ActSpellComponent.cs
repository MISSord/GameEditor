using EGamePlay;
using EGamePlay.Combat;
using System.Collections.Generic;
using UnityEngine;

namespace ACTGameEditor.Combat
{
    /// <summary>技能出手意图入队；每帧选最高 Sort，Gate 通过后启动 ActSpellSession。</summary>
    public sealed class ActSpellComponent : EGamePlay.Component
    {
        CombatEntity Unit => GetEntity<CombatEntity>();

        public override bool DefaultEnable { get; set; } = true;
        public override bool IsNeedUpdate { get; protected set; } = true;

        public SkillCDTimer CDTimer { get; private set; }

        readonly List<SkillSpellInfo> _queue = new List<SkillSpellInfo>(4);

        public override void Awake()
        {
            CDTimer = new SkillCDTimer();
        }

        public override void OnDestroy()
        {
            for (int i = _queue.Count - 1; i >= 0; i--)
                PoolManager.Instance.Return(_queue[i]);
            _queue.Clear();
            CDTimer = null;
        }

        public override void Update(float deltaTime)
        {
            CDTimer?.OnUpdate(deltaTime);
            if (_queue.Count == 0)
                return;

            SkillSpellInfo winner = null;
            for (int i = 0; i < _queue.Count; i++)
            {
                SkillSpellInfo info = _queue[i];
                if (winner == null || info.Sort > winner.Sort)
                {
                    if (winner != null)
                        PoolManager.Instance.Return(winner);
                    winner = info;
                }
                else
                {
                    PoolManager.Instance.Return(info);
                }
            }

            _queue.Clear();
            if (winner == null)
                return;

            bool checkCostAndCooldown = CombatContext.Instance != null && CombatContext.Instance.UseAbilityGate;
            ActivateFail fail = AbilityActivationGate.Evaluate(
                Unit, winner.SkillId, winner.Sort, CDTimer, checkCostAndCooldown);

            if (fail == ActivateFail.SortBlocked)
            {
                _queue.Add(winner);
                return;
            }

            if (fail != ActivateFail.None)
            {
                PoolManager.Instance.Return(winner);
                return;
            }

            if (Unit.GetComponent<AbilityComponent>().IdAbilities.TryGetValue(winner.SkillId, out Ability ability))
            {
                var intent = new CombatCastIntent
                {
                    SkillId = winner.SkillId,
                    Sort = winner.Sort,
                    Target = winner.Target,
                    Point = winner.Point,
                    Direction = winner.Target != null
                        ? Vector3.Normalize(winner.Target.Position - Unit.Position)
                        : Vector3.Normalize(winner.Point - Unit.Position),
                };
                ActSpellSession.Start(Unit, intent, ability);
            }

            PoolManager.Instance.Return(winner);
        }

        /// <summary>入队一次出手意图。</summary>
        public void Enqueue(in SkillSpellInfo skillSpell)
        {
            GameLog.CombatDebug($"Enqueue skillId={skillSpell.SkillId}");
            _queue.Add(skillSpell);
        }
    }

    /// <summary>技能释放入队信息。</summary>
    public class SkillSpellInfo : IResettable
    {
        public CombatEntity Target;
        public Vector3 Point;
        public int SkillId;
        public int Sort;

        public void Reset()
        {
            Target = null;
            Point = Vector3.zero;
            SkillId = 0;
            Sort = 0;
        }
    }

    /// <summary>公式用静态方法，方法名需唯一。</summary>
    public class SkillMethod
    {
        public static bool CheckIsHadEnoughEnergy(Entity target, AttributeType type, float num)
        {
            if (target is CombatEntity combatEntity)
            {
                if (combatEntity.CurrentVital.GetVitalValue(type) > num)
                    return true;
            }
            return false;
        }
    }
}
