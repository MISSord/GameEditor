using System;
using UnityEngine;
using Sirenix.OdinInspector;

namespace EGamePlay.Combat
{
    [System.AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    sealed class EffectAttribute : Attribute
    {
        readonly string effectType;
        readonly int order;

        public EffectAttribute(string effectType, int order)
        {
            this.effectType = effectType;
            this.order = order;
        }

        public string EffectType
        {
            get { return effectType; }
        }

        public int Order
        {
            get { return order; }
        }
    }

    [Serializable]
    public abstract class Effect
    {
        //效果名字
        [HideInInspector]
        public virtual string Label => "Effect";

        //显示名称
        [ToggleGroup("Enabled", "$Label")]
        public bool Enabled;
    }
}
