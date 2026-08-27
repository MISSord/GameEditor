using System.Collections.Generic;

namespace EGamePlay.Combat
{
    /// <summary>
    /// 能力组件，记录有什么能力，主动被动技能也是能力的一种，因此合并到这里。
    /// 通过 skillId 挂载能力，避免传入 Ability 实例时语义混乱。
    /// </summary>
    public class AbilityComponent : Component
    {
        public Dictionary<long, Ability> IdAbilities { get; set; } = new Dictionary<long, Ability>();

        /// <summary>
        /// 挂载能力，按技能 ID 创建 Ability 实例并激活。
        /// </summary>
        public Ability AttachAbility(int skillId)
        {
            var ability = Entity.AddChild<Ability>(skillId);
            ability.TryActivateAbility();
            IdAbilities.Add(skillId, ability);
            return ability;
        }

        public void RemoveAbility(Ability ability)
        {
            IdAbilities.Remove(ability.SkillID);
            ability.EndAbility();
        }

        public void RemoveAbility(int id)
        {
            if(IdAbilities.Remove(id, out Ability ability))
            {
                ability.EndAbility();
            }
        }

        public override void OnDestroy()
        {
            IdAbilities?.Clear();
        }

        public override void OnReset() => OnDestroy();
    }
}
