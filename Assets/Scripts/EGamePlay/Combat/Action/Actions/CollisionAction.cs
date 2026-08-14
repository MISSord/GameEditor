using ACTGameEditor;

namespace EGamePlay.Combat
{
    public class CollisionActionAbility : Entity, IActionAbility
    {
        public CombatEntity OwnerEntity { get { return GetParent<CombatEntity>(); } set { } }
        public bool Enable { get; set; }

        public bool TryMakeAction(out CollisionAction action)
        {
            if (Enable == false)
            {
                action = null;
            }
            else
            {
                action = (CollisionAction)CombatContext.Instance.AddAction<CollisionAction>();
                action.Creator = OwnerEntity;
            }
            return Enable;
        }
    }

    /// <summary>
    /// 碰撞行动
    /// </summary>
    public class CollisionAction : Entity, IActionExecute
    {
        //行动实体
        public CombatEntity Creator { get; set; }
        //目标对象
        public Entity Target { get; set; }
        //执行器
        public XCNewEventsRunner Runner { get; set; }
        public XCTriggerEvent triggerEvent { get; set; }

        public void FinishAction()
        {
            Entity.Destroy(this);
        }

        //前置处理
        private void PreProcess()
        {
            if(Target != null)
            {
                //碰撞事件发送前，回调给释放者
                //Creator.TriggerActionPoint(ActionPointType.CollisionBeforeEffect, this);
            }
        }

        public void ApplyCollision()
        {
            PreProcess();

            if (Target != null)
            {
                if (Target is CombatEntity combatEntity)
                {
                    Runner.OnTriggerEvent(this);
                }

                //这里未来可以拓展加入子弹与子弹的碰撞
            }

            PostProcess();

            FinishAction();
        }

        //后置处理
        private void PostProcess()
        {
            if (Target != null)
            {
                //碰撞事件发送后，回调给释放者
                //Creator.TriggerActionPoint(ActionPointType.CollisionAfterEffect, this);
            }
        }
    }
}