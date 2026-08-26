using EGamePlay;

namespace EGamePlay.Combat
{
    public class MotionActionAbility : Entity, IActionAbility
    {
        public ICombatUnit OwnerEntity => GetParent<Entity>() as ICombatUnit;
        public bool Enable { get; set; }

        public bool TryMakeAction(out MotionAction action)
        {
            if (!Enable)
            {
                action = null;
                return false;
            }

            action = (MotionAction)CombatContext.Instance.AddAction<MotionAction>();
            action.Creator = OwnerEntity;
            return true;
        }
    }

    /// <summary>动作行动。</summary>
    public class MotionAction : Entity, IActionExecute
    {
        /// <summary>行动实体。</summary>
        public ICombatUnit Creator { get; set; }
        /// <summary>目标对象。</summary>
        public ICombatUnit Target { get; set; }

        public void FinishAction() => Entity.Destroy(this);

        void PreProcess() { }

        public void ApplyMotion()
        {
            PreProcess();
            PostProcess();
        }

        void PostProcess() { }
    }
}
