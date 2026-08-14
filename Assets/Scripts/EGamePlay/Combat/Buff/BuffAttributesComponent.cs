using System;
using System.Collections.Generic;

namespace EGamePlay.Combat
{
    public class PropertyUpdateEvent { public BuffProperty Property; }

    public class BuffProperty: Entity
    {
        private float _currentValue;     // 运行时状态（如当前层数、剩余时间）
        public AttributeType AttributeType;
        public FloatNumeric MaxValue { get; private set; }    // 受修改器影响的属性（如最大层数、总时长）
        public Action<BuffProperty> OnCurrentValueChanged;

        public float CurrentValue
        {
            get => _currentValue;
            set
            {
                float old = _currentValue;
                // 核心逻辑：当前值始终受 MaxValue 限制
                _currentValue = Math.Clamp(value, 0, MaxValue.Value);
                if (Math.Abs(old - _currentValue) > 0.0001f)
                {
                    OnCurrentValueChanged?.Invoke(this);
                }
            }
        }

        public override void Awake()
        {
            MaxValue = new FloatNumeric();
            // 当修改器导致最大值变化时（例如最大层数从5变3），强制重算当前值
            MaxValue.OnValueChanged += (FloatNumeric) =>
            {
                CurrentValue = CurrentValue; // 触发 Clamp
            };
        }
    }

    public class BuffAttributesComponent: Component
    {
        private readonly Dictionary<string, BuffProperty> _attributeNameNumerics = new Dictionary<string, BuffProperty>();
        private readonly PropertyUpdateEvent _attributeUpdateEvent = new PropertyUpdateEvent();

        public BuffProperty AddNumeric(AttributeType attributeType, float baseValue)
        {
            var numeric = Entity.AddChild<BuffProperty>();
            numeric.Name = attributeType.ToString();
            numeric.AttributeType = attributeType;
            numeric.MaxValue.SetBase(baseValue);
            numeric.OnCurrentValueChanged += OnNumericUpdate;
            _attributeNameNumerics.Add(attributeType.ToString(), numeric);
            return numeric;
        }

        public BuffProperty GetNumeric(AttributeType attributeType)
        {
            _attributeNameNumerics.TryGetValue(attributeType.ToString(), out var property);
            return property;
        }

        public bool TryGetNumeric(AttributeType attributeType, out BuffProperty property)
        {
            return _attributeNameNumerics.TryGetValue(attributeType.ToString(), out property);
        }

        public BuffProperty GetNumeric(string attributeName)
        {
            _attributeNameNumerics.TryGetValue(attributeName, out var property);
            return property;
        }

        public void OnNumericUpdate(BuffProperty property)
        {
            _attributeUpdateEvent.Property = property;
            Entity.Publish(_attributeUpdateEvent);
        }

        public override void OnDestroy()
        {
            if (_attributeNameNumerics != null)
            {
                foreach (var kv in _attributeNameNumerics)
                {
                    if (kv.Value != null)
                        kv.Value.OnCurrentValueChanged -= OnNumericUpdate;
                }
                _attributeNameNumerics.Clear();
            }
        }
    }
}
