namespace EGamePlay.Combat
{
    public class EntityDeadEvent { public Entity DeadEntity; }

    public class RemoveStatusEvent
    {
        public Entity Entity { get; set; }
        public Buff buff { get; set; }
        public long BuffId { get; set; }
        /// <summary>本次卸除原因。</summary>
        public BuffRemoveReason Reason { get; set; }
    }

    public class AddStatusEvent
    {
        public Entity Entity { get; set; }
        public Buff buff { get; set; }
        public long BuffId { get; set; }
    }
}
