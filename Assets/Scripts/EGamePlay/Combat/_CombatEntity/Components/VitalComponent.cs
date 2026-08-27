using System;
using System.Collections.Generic;

namespace EGamePlay.Combat
{
    //这个是资源组件，管理如当前血量，当前能量等资源数据，与属性那边相互辅助！！
    //属性决定资源上限值，如最大生命，最大能量值等！！
    public class VitalComponent : Component
    {
        private readonly Dictionary<AttributeType, float> VitalNameNumerics = new Dictionary<AttributeType, float>();
        private readonly Dictionary<AttributeType, AttributeType> AttributeMaxType = new Dictionary<AttributeType, AttributeType>(); //最大值属性映射
        private AttributeComponent _attributeComponent;

        public void InitVital()
        {
            VitalNameNumerics.Clear();
            AttributeMaxType.Clear();

            _attributeComponent = Entity.GetComponent<AttributeComponent>();
            if (_attributeComponent == null) return;

            Entity.UnSubscribe<AttributeUpdateEvent>(OnAttributeChange);
            Entity.Subscribe<AttributeUpdateEvent>(OnAttributeChange);

            //血量
            if (_attributeComponent.HealthPointMax != null)
            {
                VitalNameNumerics.Add(AttributeType.HealthPoint, _attributeComponent.HealthPointMax.Value);
                AttributeMaxType.Add(AttributeType.HealthPoint, AttributeType.HealthPointMax);
            }
            //能量
            if (_attributeComponent.ManaMax != null)
            {
                VitalNameNumerics.Add(AttributeType.Mana, _attributeComponent.ManaMax.Value);
                AttributeMaxType.Add(AttributeType.Mana, AttributeType.ManaMax);
            }
        }

        //最大属性值变化后资源值的变化
        //目前还在思考要不要进行广播，不广播的话，其他功能就要Update获取了
        //继续观望
        private void OnAttributeChange(AttributeUpdateEvent changeEvent)
        {
            if(changeEvent.Numeric.AttributeType == AttributeType.HealthPointMax)
            {
                //目前按照下面的逻辑，是有种可能，玩家因为最大血量的多次变化导致血量回满了，这个视为设计如此吧
                FloatNumeric floatNumeric = changeEvent.Numeric;
                if (floatNumeric.OldValue > floatNumeric.Value && VitalNameNumerics[AttributeType.HealthPoint] > floatNumeric.Value)
                {
                    //最大血量下降了并且当前血量值大于最大血量，重新调整当前血量
                    VitalNameNumerics[AttributeType.HealthPoint] = floatNumeric.Value;
                }
                else if (floatNumeric.Value > floatNumeric.OldValue) //最大血量上升，目前默认补上变化的量
                {
                    float diff = floatNumeric.Value - floatNumeric.OldValue;
                    VitalNameNumerics[AttributeType.HealthPoint] += diff;
                }
            }
            else if(changeEvent.Numeric.AttributeType == AttributeType.ManaMax)
            {
                FloatNumeric floatNumeric = changeEvent.Numeric;
                if (floatNumeric.OldValue > floatNumeric.Value && VitalNameNumerics[AttributeType.Mana] > floatNumeric.Value)
                {
                    //最大能量值下降了并且当前能量值大于最大能量值，重新调整当前能量值
                    VitalNameNumerics[AttributeType.Mana] = floatNumeric.Value;
                }
            }
        }

        /// <summary>
        /// 增加数值
        /// </summary>
        /// <param name="type"></param>
        /// <param name="value"></param>
        public void AddValue(AttributeType type, int value)
        {
            FloatNumeric numeric = _attributeComponent.GetNumeric(AttributeMaxType[type]);
            if (numeric != null)
            {
                float next = VitalNameNumerics[type] + value;
                VitalNameNumerics[type] = MathF.Min(next, numeric.Value);
            }
        }

        /// <summary>减少数值，结果钳制在 [0, max]。</summary>
        public void MinusValue(AttributeType type, int value)
        {
            GameLog.CombatDebug($"MinusValue {type}: {value}");
            VitalNameNumerics[type] = MathF.Max(VitalNameNumerics[type] - Math.Abs(value), 0f);
        }

        //获取某个属性现有百分比
        public float ToPercent(AttributeType type)
        {
            FloatNumeric numeric = _attributeComponent.GetNumeric(AttributeMaxType[type]);
            return (float)VitalNameNumerics[type] / numeric.Value;
        }

        //获取某个属性的最大值的百分比
        public int GetPercentHealth(AttributeType type, float pct)
        {
            return (int)(_attributeComponent.GetNumeric(AttributeMaxType[type]).Value * pct);
        }

        //获取某个属性是否满格
        public bool IsFull(AttributeType type)
        {
            return VitalNameNumerics[type] == _attributeComponent.GetNumeric(AttributeMaxType[type]).Value;
        }

        //造成伤害
        public void ReceiveDamage(IActionExecute combatAction)
        {
            var damageAction = combatAction as DamageAction;
            MinusValue(AttributeType.HealthPoint, damageAction.DamageValue);
        }

        //给予治疗
        public void ReceiveCure(IActionExecute combatAction)
        {
            if (combatAction is not ResourceAction cureAction)
            {
                return;
            }

            var cureEffect = cureAction.CureEffect;
            var attrType = cureEffect != null ? cureEffect.AttributeType : AttributeType.HealthPoint;
            int delta = cureAction.CureValue;

            if (delta > 0)
            {
                AddValue(attrType, delta);
            }
            else if (delta < 0)
            {
                MinusValue(attrType, -delta);
            }
        }

        //获取数值
        public float GetVitalValue(AttributeType type)
        {
            return VitalNameNumerics[type];
        }

        public bool CheckDead()
        {
            return VitalNameNumerics[AttributeType.HealthPoint] <= 0;
        }

        public override void OnDestroy()
        {
            Entity?.UnSubscribe<AttributeUpdateEvent>(OnAttributeChange);
            _attributeComponent = null;
        }

        public override void OnReset()
        {
            VitalNameNumerics.Clear();
            AttributeMaxType.Clear();
            _attributeComponent = null;
        }
    }
}
