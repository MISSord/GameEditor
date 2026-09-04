namespace EGamePlay.Combat
{
    /// <summary>
    /// 卸 Buff 行动：只作为 OnRemoved 回调上下文，不走全局 Dispatch。
    /// 由 <see cref="StatusComponent"/> 创建，开火后立刻销毁。
    /// </summary>
    public class RemoveStatusAction : Entity, IActionExecute, ICombatRemoveStatusContext
    {
        /// <inheritdoc />
        public ICombatUnit Creator { get; set; }

        /// <inheritdoc />
        public ICombatUnit Target { get; set; }

        /// <inheritdoc />
        public ICombatUnit Owner => Target;

        /// <inheritdoc />
        public Buff RemovedBuff { get; set; }

        /// <inheritdoc />
        public int BuffId { get; set; }

        /// <inheritdoc />
        public BuffRemoveReason Reason { get; set; }

        /// <summary>结束并回收本单。</summary>
        public void FinishAction() => Entity.Destroy(this);

        public override void OnReset()
        {
            Creator = null;
            Target = null;
            RemovedBuff = null;
            BuffId = 0;
            Reason = BuffRemoveReason.Expired;
        }
    }
}
