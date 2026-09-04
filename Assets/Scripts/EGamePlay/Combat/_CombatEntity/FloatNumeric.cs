using System;
using System.Collections.Generic;

namespace EGamePlay.Combat
{
    /// <summary>
    /// 浮点型修饰器
    /// </summary>
    public class FloatModifier
    {
        public float Value;
    }

    /// <summary>
    /// 浮点型修饰器集合
    /// </summary>
    public class FloatModifierCollection
    {
        public float TotalValue { get; private set; }
        private List<FloatModifier> Modifiers { get; } = new List<FloatModifier>();

        public float AddModifier(FloatModifier modifier)
        {
            Modifiers.Add(modifier);
            Update();
            return TotalValue;
        }

        public float RemoveModifier(FloatModifier modifier)
        {
            Modifiers.Remove(modifier);
            Update();
            return TotalValue;
        }

        public void Update()
        {
            TotalValue = 0;
            foreach (var item in Modifiers)
            {
                TotalValue += item.Value;
            }
        }
    }

    /// <summary>
    /// 浮点型数值
    /// </summary>
    public class FloatNumeric : Entity
    {
        public float OldValue { get; private set; }  //旧的总数值
        public float Value { get; private set; }     //新的总数值
        public float baseValue { get; private set; } //基础数值
        public float baseAdd { get; private set; }       //基础数值加成
        public float pctAdd { get; private set; }    //百分比加成

        //public float finalAdd { get; private set; }
        //public float finalPctAdd { get; private set; }

        private Dictionary<int, FloatModifierCollection> TypeModifierCollections { get; } = new Dictionary<int, FloatModifierCollection>();
        public AttributeType AttributeType { get; set; }
        public Action<FloatNumeric> OnValueChanged { get; set; } //更具拓展性，但开销会比原来的多

        public override void Awake()
        {
            EnsureModifierCollections();
            ResetNumericState();
        }

        public override void OnReset()
        {
            ResetNumericState();
            TypeModifierCollections.Clear();
        }

        public override void OnDestroy()
        {
            OnValueChanged = null;
        }

        void ResetNumericState()
        {
            OldValue = Value = baseValue = baseAdd = pctAdd = 0f;
            OnValueChanged = null;
            AttributeType = default;
        }

        void EnsureModifierCollections()
        {
            int addKey = (int)ModifyType.Add;
            int pctKey = (int)ModifyType.PctAdd;
            if (!TypeModifierCollections.ContainsKey(addKey))
                TypeModifierCollections.Add(addKey, new FloatModifierCollection());
            if (!TypeModifierCollections.ContainsKey(pctKey))
                TypeModifierCollections.Add(pctKey, new FloatModifierCollection());
        }

        public float SetBase(float value)
        {
            baseValue = value;
            OnChangeNumber();
            return baseValue;
        }

        //public float AddBase(float value)
        //{
        //    baseValue += value;
        //    OnChangeNumber();
        //    return baseValue;
        //}

        //public float MinusBase(float value)
        //{
        //    baseValue -= value;
        //    if (baseValue < 0) baseValue = 0;
        //    OnChangeNumber();
        //    return baseValue;
        //}

        public void AddModifier(ModifyType modifierType, FloatModifier modifier)
        {
            var value = TypeModifierCollections[((int)modifierType)].AddModifier(modifier);
            if (modifierType == ModifyType.Add) baseAdd = value;
            if (modifierType == ModifyType.PctAdd) pctAdd = value;
            //if (modifierType == ModifyType.FinalAdd) finalAdd = value;
            //if (modifierType == ModifyType.FinalPctAdd) finalPctAdd = value;
            OnChangeNumber();
        }

        public void RemoveModifier(ModifyType modifierType, FloatModifier modifier)
        {
            var value = TypeModifierCollections[((int)modifierType)].RemoveModifier(modifier);
            if (modifierType == ModifyType.Add) baseAdd = value;
            if (modifierType == ModifyType.PctAdd) pctAdd = value;
            //if (modifierType == ModifyType.FinalAdd) finalAdd = value;
            //if (modifierType == ModifyType.FinalPctAdd) finalPctAdd = value;
            OnChangeNumber();
        }

        /// <summary>
        /// 修饰器 <see cref="FloatModifier.Value"/> 已就地改写时，重新汇总该类型并刷新总属性。热路径无分配。
        /// </summary>
        public void RefreshModifier(ModifyType modifierType)
        {
            if (!TypeModifierCollections.TryGetValue((int)modifierType, out var collection) || collection == null)
                return;
            collection.Update();
            if (modifierType == ModifyType.Add) baseAdd = collection.TotalValue;
            if (modifierType == ModifyType.PctAdd) pctAdd = collection.TotalValue;
            OnChangeNumber();
        }

        public void OnChangeNumber()
        {
            OldValue = Value;
            var value1 = baseValue;
            var value2 = (value1 + baseAdd) * (100 + pctAdd) / 100f;
            //var value3 = (value2 + finalAdd) * (100 + finalPctAdd) / 100f;
            Value = value2; //value3;
            OnValueChanged?.Invoke(this);
        }
    }
}