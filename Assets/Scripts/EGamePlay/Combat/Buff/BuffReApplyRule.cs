using System;

namespace EGamePlay.Combat
{
    /// <summary>
    /// Buff 重复添加时的处理规则。
    /// 对应配置表 BuffDemoSetting.RepeatedAddition 的取值：
    /// 1 = 叠加时间；2 = 刷新时间；3 = 叠加层数并刷新时间；4 = 互斥不叠加。
    /// </summary>
    public enum BuffReApplyRule
    {
        /// <summary>
        /// 1. 叠加时间到当前 Buff 中。
        /// </summary>
        AddDuration = 1,

        /// <summary>
        /// 2. 刷新时间（使用配置中的默认持续时间）。
        /// </summary>
        RefreshDuration = 2,

        /// <summary>
        /// 3. 在已有 Buff 上叠加层数并刷新时间。
        /// </summary>
        AddStackAndRefresh = 3,

        /// <summary>
        /// 4. 互斥：已有 Buff 时忽略新的添加。
        /// </summary>
        Exclusive = 4,
    }

    /// <summary>
    /// BuffDemoSetting 的扩展，提供对 RepeatedAddition 的强类型封装。
    /// </summary>
    public sealed partial class BuffDemoSetting
    {
        /// <summary>
        /// Buff 重复添加时的规则（从 RepeatedAddition 转换而来）。
        /// </summary>
        public BuffReApplyRule ReApplyRule => (BuffReApplyRule)RepeatedAddition;
    }
}

