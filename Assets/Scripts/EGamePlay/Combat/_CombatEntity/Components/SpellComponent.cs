using ACTGameEditor;
using System.Collections.Generic;
using UnityEngine;

namespace EGamePlay.Combat
{
    /// <summary>
    /// 技能施法组件
    /// </summary>
    public class SpellComponent : EGamePlay.Component
    {
        private CombatEntity CombatEntity => GetEntity<CombatEntity>();
        public override bool DefaultEnable { get; set; } = true;
        public override bool IsNeedUpdate { get; protected set; } = true;

        /// <summary>冷却表挂在战斗实体上，Idle 与轨道入口共用。</summary>
        public SkillCDTimer CDTimer { get; private set; }

        private List<SkillSpellInfo> _spelllist = new List<SkillSpellInfo>();

        public override void Awake()
        {
            CDTimer = new SkillCDTimer();
        }

        public override void OnDestroy()
        {
            if (_spelllist.Count > 0)
            {
                for (int i = 0; i < _spelllist.Count; i++)
                {
                    PoolManager.Instance.Return<SkillSpellInfo>(_spelllist[i]);
                }
            }
            _spelllist.Clear();
            CDTimer = null;
        }

        public override void Update(float deltaTime)
        {
            CDTimer?.OnUpdate(deltaTime);

            if (_spelllist.Count == 0)
                return;

            SkillSpellInfo winner = null;
            SkillSpellInfo info;
            for (int i = 0; i < _spelllist.Count; i++)
            {
                info = _spelllist[i];
                if (winner == null || info.Sort > winner.Sort)
                {
                    if (winner != null)
                        PoolManager.Instance.Return<SkillSpellInfo>(winner);
                    winner = info;
                }
                else
                {
                    PoolManager.Instance.Return<SkillSpellInfo>(info);
                }
            }

            _spelllist.Clear();
            if (winner == null)
                return;

            bool checkCostAndCooldown = CombatContext.Instance != null && CombatContext.Instance.UseAbilityGate;
            ActivateFail fail = AbilityActivationGate.Evaluate(
                CombatEntity, winner.SkillId, winner.Sort, CDTimer, checkCostAndCooldown);

            if (fail == ActivateFail.SortBlocked)
            {
                _spelllist.Add(winner);
                return;
            }

            if (fail != ActivateFail.None)
            {
                PoolManager.Instance.Return<SkillSpellInfo>(winner);
                return;
            }

            Ability ability;
            if (Entity.GetComponent<AbilityComponent>().IdAbilities.TryGetValue(winner.SkillId, out ability))
            {
                if (winner.Target != null)
                    SpellWithTarget(ability, winner.Target, winner.Sort);
                else
                    SpellWithPoint(ability, winner.Point, winner.Sort);
            }
            PoolManager.Instance.Return<SkillSpellInfo>(winner);
        }
        
        /// <summary>
        /// 入队一次出手意图。入队前应由调用方 Gate 预检并消费预输入；
        /// 此处提交时再 Evaluate，防止入队后被命中改状态。
        /// </summary>
        public void AddSkillSpellInfo(SkillSpellInfo skillSpell)
        {
            GameLog.CombatDebug($"AddSkillSpellInfo skillId={skillSpell.SkillId}");
            _spelllist.Add(skillSpell);
        }

        private SpellAction SpellWithTarget(Ability spellSkill, CombatEntity targetEntity, int sort)
        {
            if (CombatEntity.SpellAbility.TryMakeAction(out var spellAction))
            {
                spellAction.SkillAbility = spellSkill;
                spellAction.InputTarget = targetEntity;
                spellAction.InputPoint = targetEntity.Position;
                spellAction.Sort = sort;
#if EGAMEPLAY_ET
                var rotation = Quaternion.LookRotation(targetEntity.Position - spellSkill.OwnerEntity.Position, math.up());
                spellSkill.OwnerEntity.Rotation = rotation;
                spellAction.InputDirection = math.forward(rotation).y;
#else
                //spellSkill.OwnerEntity.Rotation = Quaternion.LookRotation(targetEntity.Position - spellSkill.OwnerEntity.Position);
                //spellAction.InputRadian = spellSkill.OwnerEntity.Rotation.eulerAngles.y;
#endif
                spellAction.SpellSkill();
            }

            return spellAction;
        }

#if EGAMEPLAY_ET
        private float CalCos(float3 a, float3 b)
        {
            // 点积
            var dotProduct = a[0] * b[0] + a[1] * b[1];
            var d = MathF.Sqrt(a[0] * a[0] + a[1] * a[1]) * MathF.Sqrt(b[0] * b[0] + b[1] * b[1]);
            return dotProduct / d;
        }

        public SpellAction SpellWithPoint(SkillAbility spellSkill, float3 point)
        {
            if (CombatEntity.SpellAbility.TryMakeAction(out var spellAction))
            {
                spellAction.SkillAbility = spellSkill;
                spellAction.InputPoint = point;
                var forward = math.normalizesafe(point - spellSkill.OwnerEntity.Position, math.right());
                var rotate = quaternion.LookRotation(forward, math.up());
                spellSkill.OwnerEntity.Rotation = rotate;
                var cos = CalCos(math.right(), forward);
                var radian = MathF.Acos(cos);
                if (forward.y < 0) radian = -radian;
                spellAction.InputDirection = forward;
                spellAction.InputRadian = radian;
                spellAction.InputPoint = point;
                if (spellSkill.SkillConfig.Id == 2003) CombatEntity.AttackAbility.InputDirection = forward;
                spellAction.SpellSkill();
                return spellAction;
            }
            return null;
        }
#else
        private SpellAction SpellWithPoint(Ability spellSkill, Vector3 point, int sort)
        {
            if (CombatEntity.SpellAbility.TryMakeAction(out var spellAction))
            {
                spellAction.SkillAbility = spellSkill;
                var forward = Vector3.Normalize(point - spellSkill.OwnerEntity.Position);
                spellAction.InputDirection = forward;
                spellAction.InputPoint = point;
                spellAction.Sort = sort;
                spellAction.SpellSkill();
            }

            return spellAction;
        }
#endif
    }

    //技能释放信息
    public class SkillSpellInfo : IResettable
    {
        public CombatEntity Target;
        public Vector3 Point;
        public int SkillId;
        public int Sort; //优先级

        public void Reset()
        {
            Target = null;
            Point = Vector3.zero;
            SkillId = 0;
            Sort = 0;
        }
    }

    //这里要保证每个方法名都是唯一，不然快速解析会出现问题
    public class SkillMethod
    {
        /// <summary>
        /// 检查是否有足够的（资源）去释放技能
        /// </summary>
        /// <param name="target"></param>
        /// <param name="buff"></param>
        /// <returns></returns>
        public static bool CheckIsHadEnoughEnergy(Entity target, AttributeType type, float num)
        {
            if (target is CombatEntity)
            {
                CombatEntity combatEntity = (CombatEntity)target;
                if (combatEntity.GetComponent<VitalComponent>().GetVitalValue(type) > num)
                {
                    return true;
                }
            }
            return false;
        }
    }

}
