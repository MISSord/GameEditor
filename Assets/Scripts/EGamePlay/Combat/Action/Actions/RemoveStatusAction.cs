using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EGamePlay.Combat
{
    public class RemoveStatusActionAbility : Entity, IActionAbility
    {
        public CombatEntity OwnerEntity { get { return GetParent<CombatEntity>(); } set { } }
        public bool Enable { get; set; }

        public bool TryMakeAction(out RemoveStatusAction action)
        {
            if (Enable == false)
            {
                action = null;
            }
            else
            {
                action = (RemoveStatusAction)CombatContext.Instance.AddAction<RemoveStatusAction>();
                action.Creator = OwnerEntity;
            }
            return Enable;
        }
    }

    public class RemoveStatusAction : Entity, IActionExecute
    {
        public CombatEntity Creator { get; set; }
        public Entity Target { get; set; }

        //是否全部Buff移除
        public bool IsAllRemove;
        //BuffId列表
        public List<int> BuffIdList;
        //Buff大类型列表
        public List<int> BuffBigTypeList;

        //前置处理
        private void PreProcess()
        {

        }

        //后置处理
        private void PostProcess()
        {

        }

        public void AddRemoveStatus()
        {
            PreProcess();

            StatusComponent state = Target.GetComponent<StatusComponent>();
            if (state != null)
            {

            }

            PostProcess();
        }
    }
}
