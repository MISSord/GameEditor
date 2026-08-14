using EGamePlay;
using EGamePlay.Combat;
using System.Collections.Generic;

namespace ACTGameEditor
{
    public enum RunnerState
    {
        Update,
        Stop,    //这个给未来可能会有的效果暂停
        StopEnd, //这个是全部事件触发后的自我停止
        Break,   //技能打断，外部强制性关闭技能
        Finish,  //执行器完成运行后一直处于这个状态，直到重新回收利用
    }

    //结合XCEventsRunner与AbilityExecution
    //作为执行体的上层管理器，方便外部统一处理
    public class ActSkillRunner : Entity
    {
        public Ability AbilityEntity { get; set; }
        public CombatEntity OwnerEntity { get; set; }
        public CombatEntity InputTarget { get; set; }
        public List<XCNewEventsRunner> SubRuners { get; private set; } = new List<XCNewEventsRunner>();

        private int Count;
        public bool IsMainFinish { get; private set; } = false; //正常停止和Break都算Finish

        private RunnerState _state;
        public RunnerState State { get => _state; }

        //优先级
        public int Sort = 0;

        public void StartUpdate()
        {
            this.Count = SubRuners.Count;
            _state = RunnerState.Update;
            IsMainFinish = false;
        }

        public override void Update(float deltaTime)
        {
            if (State == RunnerState.Stop || State == RunnerState.Finish)
                return;

            if (State == RunnerState.Update)
            {
                bool isSelfEnd = true; //真完成
                for (int i = Count - 1; i >= 0; i--)
                {
                    SubRuners[i].Update(deltaTime);
                    if (SubRuners[i].State != RunnerState.Finish)
                    {
                        isSelfEnd = false;
                    }
                }

                if (isSelfEnd)
                {
                    Finish();
                    _state = RunnerState.StopEnd;
                }
            }
            else if (State == RunnerState.StopEnd || State == RunnerState.Break)
            {
                _state = RunnerState.Finish;
                DestroyAll();
            }
        }

        //结束自身以及子Runner
        //接收到Break, 技能会在下一帧结束，不会触发后面的事件
        public void BreakSkill()
        {
            _state = RunnerState.Break;
            foreach (var item in SubRuners)
            {
                if (item != null)
                {
                    item.State = RunnerState.Break;
                }
            }
            Finish();
        }

        //Finish表示玩家脱离skill状态,而不影响skill的自我运行
        public void Finish()
        {
            if (!IsMainFinish)
            {
                IsMainFinish = true;
            }
        }

        private void DestroyAll()
        {
            foreach (var item in SubRuners)
            {
                if (item != null)
                {
                    item.DestroyAll();
                }
            }
            SubRuners.Clear();
            AbilityEntity = null;
            OwnerEntity = null;
            InputTarget = null;
        }
    }
}
