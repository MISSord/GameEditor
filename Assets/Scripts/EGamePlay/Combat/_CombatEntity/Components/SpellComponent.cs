using ACTGameEditor;
using System.Collections.Generic;
using UnityEngine;
using XiaoCao;
using Debug = UnityEngine.Debug;

#if EGAMEPLAY_ET
using Unity.Mathematics;
using Vector3 = Unity.Mathematics.float3;
using Quaternion = Unity.Mathematics.quaternion;
using JsonIgnore = MongoDB.Bson.Serialization.Attributes.BsonIgnoreAttribute;
#endif

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

        private List<SkillSpellInfo> _spelllist = new List<SkillSpellInfo>();

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
        }

        public override void Update(float deltaTime)
        {
            if(_spelllist.Count > 0)
            {
                SkillSpellInfo curHightSkill = null;
                SkillSpellInfo info;
                for(int i = 0; i < _spelllist.Count; i++)
                {
                    info = _spelllist[i];
                    SkillDemoSetting setting = SkillSettingMgr.Instance.GetSkillDemoSetting(info.SkillId);
                    if (setting != null)
                    {
                        //判断当前能否释放这个技能
                        bool isCanSpell = (bool)FastStaticExecutor.Execute(setting.TriggerFormula);
                        if(isCanSpell && curHightSkill == null)
                        {
                            curHightSkill = info;
                        }
                        else if(isCanSpell && info.Sort > curHightSkill.Sort)
                        {
                            PoolManager.Instance.Return<SkillSpellInfo>(curHightSkill);
                            curHightSkill = info;
                        }
                    }
                    //不是当前的直接塞回去
                    if(info != curHightSkill)
                    {
                        PoolManager.Instance.Return<SkillSpellInfo>(info);
                    }
                }
                if(curHightSkill != null)
                {
                    if (CombatEntity.SpellingExecution != null && CombatEntity.SpellingExecution.Sort > curHightSkill.Sort)
                        return;

                    Ability ability;
                    if(Entity.GetComponent<AbilityComponent>().IdAbilities.TryGetValue(curHightSkill.SkillId, out ability))
                    {
                        if(curHightSkill.Target != null)
                        {
                            this.SpellWithTarget(ability, curHightSkill.Target);
                        }
                        else
                        {
                            this.SpellWithPoint(ability, curHightSkill.Point);
                        }
                    }
                    PoolManager.Instance.Return<SkillSpellInfo>(curHightSkill);
                }
                //每帧清空，不缓存
                _spelllist.Clear();
            }
        }
        
        //能进入到这里的，说明常规的外部判断都已经完成，如玩家状态，标签检查那些，后面的判断只会判断配置部分
        //因此要在外部进行非配置部分判断！！
        public void AddSkillSpellInfo(SkillSpellInfo skillSpell)
        {
            GameLog.CombatDebug($"AddSkillSpellInfo skillId={skillSpell.SkillId}");
            _spelllist.Add(skillSpell);
        }

        private SpellAction SpellWithTarget(Ability spellSkill, CombatEntity targetEntity = null, int sort = 0)
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
            //if (CombatEntity.SpellingExecution != null)
            //    return null;

            if (CombatEntity.SpellAbility.TryMakeAction(out var spellAction))
            {
                spellAction.SkillAbility = spellSkill;
                spellAction.InputPoint = point;
                var forward = math.normalizesafe(point - spellSkill.OwnerEntity.Position, math.right());
                //var forward = new float3(rawForward.x, -rawForward.y, 1);
                var rotate = quaternion.LookRotation(forward, math.up());
                spellSkill.OwnerEntity.Rotation = rotate;//.GetQuaternionEulerAngles() / MathF.PI * 180;
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
        private SpellAction SpellWithPoint(Ability spellSkill, Vector3 point, int sort = 0)
        {
            if (CombatEntity.SpellAbility.TryMakeAction(out var spellAction))
            {
                spellAction.SkillAbility = spellSkill;
                var forward = Vector3.Normalize(point - spellSkill.OwnerEntity.Position);
                //var rotation = Quaternion.LookRotation(forward);
                //var angle = rotation.eulerAngles.y;
                //var radian = angle * MathF.PI / 180f;
                //spellAction.InputRadian = radian;
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