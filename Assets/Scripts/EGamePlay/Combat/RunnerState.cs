namespace EGamePlay.Combat
{
    /// <summary>技能子轴 / 执行器生命周期状态。</summary>
    public enum RunnerState : byte
    {
        Update = 0,
        Stop,
        StopEnd,
        Break,
        Finish,
    }
}
