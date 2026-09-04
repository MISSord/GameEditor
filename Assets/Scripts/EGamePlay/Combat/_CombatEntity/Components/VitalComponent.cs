using System;
using System.Collections.Generic;

namespace EGamePlay.Combat
{
    //这个是资源组件，管理如当前血量，当前能量等资源数据，与属性那边相互辅助！！
    //属性决定资源上限值，如最大生命，最大能量值等！！
    public class VitalComponent : Component
    {
        struct ShieldSegment
        {
            public int BuffId;
            public int Remaining;
        }

        private readonly Dictionary<AttributeType, float> VitalNameNumerics = new Dictionary<AttributeType, float>();
        private readonly Dictionary<AttributeType, AttributeType> AttributeMaxType = new Dictionary<AttributeType, AttributeType>(); //最大值属性映射
        private readonly List<ShieldSegment> _shields = new List<ShieldSegment>(4);
        private readonly List<int> _depletedScratch = new List<int>(4);
        private AttributeComponent _attributeComponent;
        int _shieldTotal;

        /// <summary>当前吸收层总和。不是生命，不进 Attribute。</summary>
        public int Shield => _shieldTotal;

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

        //造成伤害：先吞盾，溢出再扣 HP。
        public void ReceiveDamage(IActionExecute combatAction)
        {
            var damageAction = combatAction as DamageAction;
            if (damageAction == null)
                return;

            int incoming = damageAction.DamageValue;
            if (incoming < 0)
                incoming = 0;

            int leftover = AbsorbShield(incoming);
            damageAction.ShieldAbsorbed = incoming - leftover;
            damageAction.HpDamageApplied = leftover;

            if (leftover > 0)
                MinusValue(AttributeType.HealthPoint, leftover);

            // 先扣完本刀 HP，再卸破盾 Buff，避免 OnRemoved 嵌套伤害清掉本刀的破盾列表。
            FlushDepletedShieldBuffs();
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

        /// <summary>按当前生命 + 护盾预判该伤害值是否足以致死（尚未扣血、不消耗盾）。</summary>
        public bool WouldDieFrom(int damage)
        {
            if (damage <= 0)
                return false;
            if (!VitalNameNumerics.TryGetValue(AttributeType.HealthPoint, out float hp))
                return false;
            int leftover = damage - _shieldTotal;
            if (leftover <= 0)
                return false;
            return hp <= leftover;
        }

        /// <summary>挂上或刷新某 Buff 的盾段。同 BuffId 重置为新值。</summary>
        public void AddOrReplaceShield(int buffId, int value)
        {
            if (buffId <= 0 || value <= 0)
                return;

            for (int i = 0; i < _shields.Count; i++)
            {
                if (_shields[i].BuffId != buffId)
                    continue;
                _shieldTotal -= _shields[i].Remaining;
                if (_shieldTotal < 0)
                    _shieldTotal = 0;
                _shields[i] = new ShieldSegment { BuffId = buffId, Remaining = value };
                _shieldTotal += value;
                return;
            }

            _shields.Add(new ShieldSegment { BuffId = buffId, Remaining = value });
            _shieldTotal += value;
        }

        /// <summary>卸 Buff 时掉该段剩余盾。段已破则无操作。</summary>
        public void RemoveShield(int buffId)
        {
            for (int i = 0; i < _shields.Count; i++)
            {
                if (_shields[i].BuffId != buffId)
                    continue;
                _shieldTotal -= _shields[i].Remaining;
                if (_shieldTotal < 0)
                    _shieldTotal = 0;
                _shields.RemoveAt(i);
                return;
            }
        }

        int AbsorbShield(int damage)
        {
            _depletedScratch.Clear();
            if (damage <= 0 || _shieldTotal <= 0)
                return damage < 0 ? 0 : damage;

            int leftover = damage;
            int i = 0;
            while (i < _shields.Count && leftover > 0)
            {
                ShieldSegment seg = _shields[i];
                if (seg.Remaining >= leftover)
                {
                    seg.Remaining -= leftover;
                    _shieldTotal -= leftover;
                    leftover = 0;
                    if (seg.Remaining <= 0)
                    {
                        _depletedScratch.Add(seg.BuffId);
                        _shields.RemoveAt(i);
                    }
                    else
                    {
                        _shields[i] = seg;
                    }
                    break;
                }

                leftover -= seg.Remaining;
                _shieldTotal -= seg.Remaining;
                _depletedScratch.Add(seg.BuffId);
                _shields.RemoveAt(i);
            }

            if (_shieldTotal < 0)
                _shieldTotal = 0;
            return leftover;
        }

        void FlushDepletedShieldBuffs()
        {
            int n = _depletedScratch.Count;
            if (n == 0)
                return;

            // 先拷出再 Clear，嵌套 ReceiveDamage 可以安全复用 _depletedScratch。
            Span<int> ids = stackalloc int[8];
            int copy = n > 8 ? 8 : n;
            for (int i = 0; i < copy; i++)
                ids[i] = _depletedScratch[i];
            _depletedScratch.Clear();

            StatusComponent status = Entity?.GetComponent<StatusComponent>();
            for (int i = 0; i < copy; i++)
                status?.RemoveStatus(ids[i], BuffRemoveReason.Consumed);
        }

        public override void OnDestroy()
        {
            Entity?.UnSubscribe<AttributeUpdateEvent>(OnAttributeChange);
            _attributeComponent = null;
            _shields.Clear();
            _depletedScratch.Clear();
            _shieldTotal = 0;
        }

        public override void OnReset()
        {
            VitalNameNumerics.Clear();
            AttributeMaxType.Clear();
            _shields.Clear();
            _depletedScratch.Clear();
            _shieldTotal = 0;
            _attributeComponent = null;
        }
    }
}
