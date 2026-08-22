namespace ACTGameEditor
{
    /// <summary>
    /// 分槽位预输入：同槽覆盖、按 PlayerTime 过期、成功只消费赢家通道。
    /// </summary>
    public sealed class InputBuffer
    {
        private const int Capacity = 16;

        private struct Pending
        {
            public bool Occupied;
            public InputListernType Command;
            public PressType Press;
            public InputCallBackType Callback;
            public float SetAt;
            public float ExpireAt;
        }

        private readonly Pending[] _slots = new Pending[Capacity];

        /// <summary>写入或覆盖某槽位的预输入。</summary>
        public void Set(
            SkillSlotId slotId,
            InputListernType command,
            PressType press,
            InputCallBackType callback,
            float now,
            float expireAt)
        {
            int index = (int)slotId;
            if ((uint)index >= Capacity)
                return;

            _slots[index] = new Pending
            {
                Occupied = true,
                Command = command,
                Press = press,
                Callback = callback,
                SetAt = now,
                ExpireAt = expireAt,
            };
        }

        /// <summary>丢掉过期项。可每帧多次调用。</summary>
        public void Tick(float now)
        {
            for (int i = 0; i < Capacity; i++)
            {
                if (_slots[i].Occupied && now >= _slots[i].ExpireAt)
                    _slots[i].Occupied = false;
            }
        }

        /// <summary>是否还有未过期预输入。</summary>
        public bool HasAny()
        {
            for (int i = 0; i < Capacity; i++)
            {
                if (_slots[i].Occupied)
                    return true;
            }

            return false;
        }

        /// <summary>指定槽位是否仍有预输入。</summary>
        public bool HasSlot(SkillSlotId slotId)
        {
            int index = (int)slotId;
            return (uint)index < Capacity && _slots[index].Occupied;
        }

        /// <summary>预输入是否匹配该槽位的键位绑定。</summary>
        public bool MatchesSlot(
            SkillSlotId slotId,
            InputListernType command,
            PressType press,
            InputCallBackType callback)
        {
            int index = (int)slotId;
            if ((uint)index >= Capacity || !_slots[index].Occupied)
                return false;

            Pending p = _slots[index];
            return p.Command == command && p.Press == press && p.Callback == callback;
        }

        /// <summary>消费指定槽位。</summary>
        public void Consume(SkillSlotId slotId)
        {
            int index = (int)slotId;
            if ((uint)index >= Capacity)
                return;
            _slots[index].Occupied = false;
        }

        /// <summary>
        /// 按键匹配并消费。maxAge&gt;0 时限制「按下至今」不得超过该时长（窗边短预输入）。
        /// maxAge≤0 表示不按年龄筛（调用方应尽量传入正值）。过期槽由 Tick 清理。
        /// </summary>
        public bool TryConsume(
            InputListernType command,
            PressType press,
            InputCallBackType callback,
            float now,
            float maxAge)
        {
            int index = FindMatchingIndex(command, press, callback, now, maxAge);
            if (index < 0)
                return false;
            _slots[index].Occupied = false;
            return true;
        }

        /// <summary>与 TryConsume 相同匹配规则，但不消费。</summary>
        public bool CanConsume(
            InputListernType command,
            PressType press,
            InputCallBackType callback,
            float now,
            float maxAge)
        {
            return FindMatchingIndex(command, press, callback, now, maxAge) >= 0;
        }

        private int FindMatchingIndex(
            InputListernType command,
            PressType press,
            InputCallBackType callback,
            float now,
            float maxAge)
        {
            for (int i = 0; i < Capacity; i++)
            {
                if (!_slots[i].Occupied)
                    continue;

                Pending p = _slots[i];
                if (p.Command != command || p.Press != press || p.Callback != callback)
                    continue;
                // 窗边预输入：按下太久的意图作废，避免开打瞬间的键在后摇窗被吃成下一段
                if (maxAge > 0f && now - p.SetAt >= maxAge)
                    continue;
                return i;
            }

            return -1;
        }

        /// <summary>是否存在匹配且未因 maxAge 失效的预输入（不消费）。</summary>
        public bool HasCommand(
            InputListernType command,
            PressType press,
            InputCallBackType callback)
        {
            for (int i = 0; i < Capacity; i++)
            {
                if (!_slots[i].Occupied)
                    continue;
                Pending p = _slots[i];
                if (p.Command == command && p.Press == press && p.Callback == callback)
                    return true;
            }

            return false;
        }

        /// <summary>硬重置（销毁、强制清状态）。出手成功不要走这里。</summary>
        public void Clear()
        {
            for (int i = 0; i < Capacity; i++)
                _slots[i].Occupied = false;
        }
    }
}
