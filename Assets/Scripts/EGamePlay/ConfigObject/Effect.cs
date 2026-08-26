using System;
using UnityEngine;
using Sirenix.OdinInspector;

#if EGAMEPLAY_ET
using Unity.Mathematics;
using Vector3 = Unity.Mathematics.float3;
using Quaternion = Unity.Mathematics.quaternion;
using JsonIgnore = MongoDB.Bson.Serialization.Attributes.BsonIgnoreAttribute;
#endif


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
#if UNITY
    public abstract class Effect
#else
    public class Effect : ET.Object
#endif
    {
        //效果名字
        [HideInInspector]
        public virtual string Label => "Effect";

        //显示名称
        [ToggleGroup("Enabled", "$Label")]
        public bool Enabled;
    }
}
