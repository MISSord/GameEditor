using EGamePlay;
using EGamePlay.Unity;
using EGamePlay.Combat;
using ACTGameEditor.Combat;
using System.Collections.Generic;

namespace ACTGameEditor
{
    //结合XCEventsRunner与AbilityExecution
    //作为执行体的上层管理器，方便外部统一处理
    public class ActSkillRunner : Entity, ISkillExecutionHandle
    {
        public Ability AbilityEntity { get; set; }
        public CombatEntity OwnerEntity { get; set; }
        public CombatEntity InputTarget { get; set; }
        public List<XCNewEventsRunner> SubRuners { get; private set; } = new List<XCNewEventsRunner>();

        private int Count;
        public bool IsMainFinish { get; private set; } = false; //正常停止和Break都算Finish

        private RunnerState _state;
        public RunnerState State { get => _state; }

        long ISkillExecutionHandle.Id => Id;
        int ISkillExecutionHandle.Sort => Sort;
        bool ISkillExecutionHandle.IsFinished => _state == RunnerState.Finish;

        void ISkillExecutionHandle.Tick(float deltaTime) => Update(deltaTime);

        //优先级
        public int Sort = 0;

        /// <summary>本轴最近一次技能动画 Token，结束时按此 Release。</summary>
        public int AnimToken { get; private set; }

        /// <summary>最近一次动画事件的结束策略。</summary>
        public AnimExitPolicy AnimExitPolicy { get; private set; }

        /// <summary>本轴 Hurtbox 是否碰到过焦点侧（本地玩家或 PlayerA/B 假玩家）。E 落空用。</summary>
        public int ConfirmedPlayerHits { get; private set; }

        /// <summary>本轴是否带攻击盒（装配时写入）。无盒轴（假前摇等）不参与落空判定。</summary>
        public bool HasHitboxEvents { get; set; }

        /// <summary>是否被 BreakSkill 打断。打断不当作挥空。</summary>
        public bool WasBroken { get; private set; }

        /// <summary>盒申报碰到焦点侧单位时由 Trigger 调用。</summary>
        public void NotifyPlayerHitConnected() => ConfirmedPlayerHits++;

        /// <summary>XCAnimEvent 播放成功后登记 Token 与 ExitPolicy。</summary>
        public void NotifyAnimPlayed(int token, AnimExitPolicy exitPolicy)
        {
            if (token == 0)
                return;
            AnimToken = token;
            AnimExitPolicy = exitPolicy;
        }

        public void StartUpdate()
        {
            this.Count = SubRuners.Count;
            _state = RunnerState.Update;
            IsMainFinish = false;
            AnimToken = 0;
            AnimExitPolicy = AnimExitPolicy.Locomotion;
            ConfirmedPlayerHits = 0;
            WasBroken = false;
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
                // 自然结束：按 ExitPolicy；Break：不交回（新技能已占 Token）
                bool returnToLocomotion = State == RunnerState.StopEnd && AnimExitPolicy != AnimExitPolicy.Hold;
                _state = RunnerState.Finish;
                ReleaseAnimOwnership(returnToLocomotion);
                DestroyAll();
            }
        }

        //结束自身以及子Runner
        //接收到Break, 技能会在下一帧结束，不会触发后面的事件
        public void BreakSkill()
        {
            WasBroken = true;
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

        void ReleaseAnimOwnership(bool returnToLocomotion)
        {
            if (AnimToken == 0 || OwnerEntity == null || OwnerEntity.IsDisposed)
                return;

            CombatAnimDirector director = OwnerEntity.GetComponent<AnimComponent>()?.Director;
            director?.Release(AnimToken, returnToLocomotion);
            AnimToken = 0;
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

        public override void OnDestroy()
        {
            ReleaseAnimOwnership(returnToLocomotion: false);
            DestroyAll();
        }

        public override void OnReset()
        {
            _state = RunnerState.Finish;
            Sort = 0;
            IsMainFinish = false;
            AnimToken = 0;
            AnimExitPolicy = AnimExitPolicy.Locomotion;
            ConfirmedPlayerHits = 0;
            WasBroken = false;
            HasHitboxEvents = false;
            Count = 0;
            AbilityEntity = null;
            OwnerEntity = null;
            InputTarget = null;
            SubRuners.Clear();
        }
    }
}
