using System;

namespace ACTGameEditor
{
    /// <summary>
    /// 显现频道：可组合，便于脚印 / 隐藏图 / 特效分类开关。
    /// </summary>
    [Flags]
    public enum RevealChannel : int
    {
        None = 0,
        Default = 1 << 0,
        Footprints = 1 << 1,
        HiddenImages = 1 << 2,
        Effects = 1 << 3,
        All = Default | Footprints | HiddenImages | Effects,
    }
}
