using ACTGameEditor;
using System.Collections.Generic;

namespace EGamePlay.Combat
{
    /// <summary>
    /// 状态组件，这个组件只关心玩家当前有什么buff
    /// </summary>
    public class StatusComponent : Component
    {
        public override bool IsNeedUpdate { get; protected set; } = true;
        public List<Buff> Statuses = new List<Buff>();
        public Dictionary<int, List<Buff>> TypeIdStatuses = new Dictionary<int, List<Buff>>(); //这里按照Buff大类型分类
        public Dictionary<int, Buff> IdStatuses = new Dictionary<int, Buff>(); //这里是记录BuffId
        public GameplayTagContainer TagContainer = new GameplayTagContainer(); //标签

        public override void OnDestroy()
        {
            if(Statuses.Count > 0)
            {
                for(int i = 0; i < Statuses.Count; i++)
                {
                    //统一移除这里，其实可以不调用Buff的RemoveTag，未来看看把
                    Statuses[i].DeactivateBuff();
                    Entity.Destroy(Statuses[i]); //组件移除的时候一同把Buff也移除
                }
            }
            Statuses.Clear();
            TypeIdStatuses.Clear();
            IdStatuses.Clear();
            TagContainer.Reset();
        }

        public Buff AttachStatus(int buffId)
        {
            var buff = Entity.AddChild<Buff>(buffId);
            Statuses.Add(buff);
            IdStatuses.Add(buffId, buff);
            BuffDemoSetting Setting = SkillSettingMgr.Instance.GetBuffDemoSetting(buffId);
            if (!TypeIdStatuses.ContainsKey(Setting.BigBuffType))
            {
                TypeIdStatuses.Add(Setting.BigBuffType, new List<Buff>());
            }
            TypeIdStatuses[Setting.BigBuffType].Add(buff);
            return buff;
        }

        public Buff AttachStatus(BuffDemoSetting config)
        { 
            return AttachStatus(config.BuffId);
        }

        public void RemoveStatus(int buffId)
        {
            if (!IdStatuses.TryGetValue(buffId, out var buff) || buff == null)
                return;
            buff.DeactivateBuff();
            // Buff的添加必定走Action来完成，但Buff的移除不一定走Action，有可能是Buff的自我移除，因此要广播一下
            Entity.Publish(new RemoveStatusEvent() { Entity = this.Entity, buff = buff, BuffId = buff.Id });

            Statuses.Remove(buff);
            IdStatuses.Remove(buffId);
            int BigBuffType = buff.Setting.BigBuffType;
            TypeIdStatuses[BigBuffType].Remove(buff);
            //this.OnStatusesChanged(buff, false);
            Entity.Destroy(buff);
        }

        public bool HasBuffId(int BuffId)
        {
            return IdStatuses.ContainsKey(BuffId);
        }

        public Buff GetBuffById(int BuffId)
        {
            IdStatuses.TryGetValue(BuffId, out var buff);
            return buff;
        }

        public bool TryGetBuffById(int buffId, out Buff buff)
        {
            return IdStatuses.TryGetValue(buffId, out buff) && buff != null;
        }

        //是否有某大类Buff
        public bool HasBigBuffType(int BigBuffType)
        {
            //某种大类型Buff数量为空后暂不移除列表，因为重新加列表的GC还是有点大，而且频繁添加移除的概率很高！！
            if(TypeIdStatuses.TryGetValue(BigBuffType, out List<Buff> list))
            {
                return list.Count > 0;
            }
            return false;
        }

        //移除某种大类的Buff
        public void RemoveBuffByBigType(int BigBuffType)
        {
            if(TypeIdStatuses.TryGetValue(BigBuffType, out List<Buff> list))
            {
                for(int i = 0; i < list.Count; i++)
                {
                    Buff buff = list[i];
                    buff.DeactivateBuff();
                    Entity.Publish(new RemoveStatusEvent() { Entity = this.Entity, buff = buff, BuffId = buff.Id });
                    Statuses.Remove(buff);
                    IdStatuses.Remove(buff.BuffID);
                    //this.OnStatusesChanged(buff, false);
                    Entity.Destroy(buff);
                }
                list.Clear(); //只清空移除
            }
        }

        //public override void Update(float deltaTime)
        //{
        //    Buff buff;
        //    for(int i = Statuses.Count - 1; i >= 0; i--)
        //    {
        //        buff = Statuses[i];
        //        if (buff.IsNeedBuffUpdate) buff.Update(deltaTime);
        //        //是否能移除Buff
        //        if (buff.IsCanRemoveBuff == true)
        //        {
        //            this.RemoveStatus(buff.BuffID);
        //        }
        //    }
        //}

        public void OnStatusesChanged(Buff buff, bool isAdd)
        {
            //foreach (var item in Statuses)
            //{
            //    //没激活或者不是控制Buff
            //    if (!item.Enable)
            //    {
            //        continue;
            //    }
            //    //这里未来重新写
            //    //foreach (var effect in item.GetComponent<AbilityEffectComponent>().AbilityEffects)
            //    //{
            //    //    if (effect.Enable && effect.TryGet(out EffectActionControlComponent actionControlComponent))
            //    //    {
            //    //        tempActionControl = tempActionControl | actionControlComponent.ActionControlEffect.ActionControlType;
            //    //    }
            //    //}
            //}

            //if (Entity is CombatEntity combatEntity)
            //{
            //    combatEntity.ActionControlType = tempActionControl;
            //    CheckCombatEntity();
            //}
        }

        ////按照当前状态检查刷新组件
        //private void CheckCombatEntity()
        //{
        //    CombatEntity combatEntity = Entity as CombatEntity;
        //    var moveForbid = combatEntity.ActionControlType.HasFlag(ActionControlType.MoveForbid);
        //    combatEntity.GetComponent<MotionComponent>().Enable = !moveForbid;
        //}
    }
}
