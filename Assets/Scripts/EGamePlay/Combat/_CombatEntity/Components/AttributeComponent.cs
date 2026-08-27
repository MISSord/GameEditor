using System;
using System.Collections.Generic;

namespace EGamePlay.Combat
{
    public class AttributeUpdateEvent { public FloatNumeric Numeric; }

    /// <summary>
    /// 战斗属性数值组件，在这里管理角色所有战斗属性数值的存储、变更、刷新等
    /// </summary>
    public class AttributeComponent : Component
    {
        private readonly Dictionary<AttributeType, FloatNumeric> _attributeNameNumerics = new Dictionary<AttributeType, FloatNumeric>();

        private readonly AttributeUpdateEvent _attributeUpdateEvent = new AttributeUpdateEvent();
        public FloatNumeric MoveSpeed { get { return _attributeNameNumerics[AttributeType.MoveSpeed]; } }//移动速度
        public FloatNumeric HealthPointMax { get { return _attributeNameNumerics[AttributeType.HealthPointMax]; } }//生命值上限
        public FloatNumeric ManaMax { get { return _attributeNameNumerics[AttributeType.ManaMax]; } } //能量值上限
        public FloatNumeric Attack { get { return _attributeNameNumerics[AttributeType.Attack]; } }//攻击力
        public FloatNumeric Defense { get { return _attributeNameNumerics[AttributeType.Defense]; } }//防御力（护甲）
        public FloatNumeric CriticalProbability { get { return _attributeNameNumerics[AttributeType.CriticalProbability]; } }//暴击概率
        public FloatNumeric CriticalValue { get { return _attributeNameNumerics[AttributeType.CriticalValue]; } }//暴击伤害

        /// <summary>使用写死的默认数值初始化（兼容未传角色 ID/等级 的旧逻辑）。</summary>
        public void InitializeCharacter()
        {
            AddNumeric(AttributeType.HealthPointMax, 999);
            AddNumeric(AttributeType.ManaMax, 100);
            AddNumeric(AttributeType.MoveSpeed, 1f);
            AddNumeric(AttributeType.Attack, 400);
            AddNumeric(AttributeType.Defense, 300);
            AddNumeric(AttributeType.CriticalProbability, 0.5f);
            AddNumeric(AttributeType.CriticalValue, 1.5f);
            InitializeDamageRelatedAttributes(300f);
        }

        /// <summary>根据角色 ID 与等级从 RoleAttriSetting 配置初始化属性（基础值 + 等级 * 增长值），无 buff 影响。暴击率/暴击倍率由万分比转为逻辑值（/10000）。</summary>
        public void InitializeCharacter(int characterId, int level)
        {
            RoleAttriAtLevel attri = SkillSettingMgr.Instance.GetRoleAttriAtLevel(characterId, level);
            AddNumeric(AttributeType.HealthPointMax, attri.HealthPointMax);
            AddNumeric(AttributeType.ManaMax, attri.ManaMax);
            AddNumeric(AttributeType.MoveSpeed, 1f);
            AddNumeric(AttributeType.Attack, attri.Attack);
            AddNumeric(AttributeType.Defense, attri.Defense);
            AddNumeric(AttributeType.CriticalProbability, attri.CriticalProbability / 10000f);
            AddNumeric(AttributeType.CriticalValue, attri.CriticalValue / 10000f);
            InitializeDamageRelatedAttributes(attri.Defense);
        }

        /// <summary>防御/抗性/易伤/增伤区属性，物理防御基础值由调用方传入（默认表或等级表）。</summary>
        private void InitializeDamageRelatedAttributes(float physicalDefenseBase)
        {
            AddNumeric(AttributeType.PhysicalDefense, physicalDefenseBase);
            AddNumeric(AttributeType.PhysicResist, 0);
            AddNumeric(AttributeType.FireResist, 0);
            AddNumeric(AttributeType.IceResist, 0);
            AddNumeric(AttributeType.ElectricResist, 0);
            AddNumeric(AttributeType.Vulnerability, 0);
            AddNumeric(AttributeType.DamageBonus, 0);
            AddNumeric(AttributeType.PhysicDamageBonus, 0);
            AddNumeric(AttributeType.FireDamageBonus, 0);
            AddNumeric(AttributeType.IceDamageBonus, 0);
            AddNumeric(AttributeType.ElectricDamageBonus, 0);
        }

        public FloatNumeric AddNumeric(AttributeType attributeType, float baseValue)
        {
            var numeric = Entity.AddChild<FloatNumeric>();
            numeric.Name = attributeType.ToString();
            numeric.AttributeType = attributeType;
            numeric.SetBase(baseValue);
            numeric.OnValueChanged += OnNumericUpdate;
            _attributeNameNumerics[attributeType] = numeric;
            return numeric;
        }

        public FloatNumeric GetNumeric(AttributeType attributeType)
        {
            return _attributeNameNumerics[attributeType];
        }

        /// <summary>安全获取属性，不存在时返回 false，用于伤害公式等兼容未初始化新属性的实体。</summary>
        public bool TryGetNumeric(AttributeType attributeType, out FloatNumeric numeric)
        {
            return _attributeNameNumerics.TryGetValue(attributeType, out numeric);
        }

        public FloatNumeric GetNumeric(string attributeType)
        {
            if(Enum.TryParse(attributeType, out AttributeType type))
            {
                return _attributeNameNumerics[type];
            }
            return null;
        }

        public void OnNumericUpdate(FloatNumeric numeric)
        {
            _attributeUpdateEvent.Numeric = numeric;
            Entity.Publish(_attributeUpdateEvent);
#if EGAMEPLAY_ET
            if (Entity.GetComponent<CombatUnitComponent>() != null)
            {
                var unit = Entity.GetComponent<CombatUnitComponent>().Unit;
                if (unit != null)
                {
                    AOGame.PublishServer(new UnitAttributeNumericChanged() { Unit = unit, AttributeNumeric = numeric });
                }
            }
#endif
        }

        public override void OnReset()
        {
            foreach (var pair in _attributeNameNumerics)
            {
                if (pair.Value != null)
                    pair.Value.OnValueChanged -= OnNumericUpdate;
            }
            _attributeNameNumerics.Clear();
        }
    }
}
