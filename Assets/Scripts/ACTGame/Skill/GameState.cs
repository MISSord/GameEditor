using EGamePlay;
using EGamePlay.Combat;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ACTGameEditor
{
    // 角色当前行为逻辑枚举
    // 这个和PlayerStateEnum是不同，PlayerStateEnum是描述人物的当前行为，如释放技能，移动等
    // GamePlayerState是描述人物行为逻辑，比PlayerStateEnum高一层，普通状态下也会有移动，待机等行为
    // 不同行为逻辑下，其行为表现与触发逻辑也有区别
    public enum GamePlayerState
    {
        None,
        //普通状态
        NormalState,
        //战斗状态
        CombatState,
        //巡逻状态
        PatrolState,
    }

    // 状态机接口
    public interface IGamePlayerState
    {
        void Enter(GamePlayerState oldState);
        void Update(float deltaTime);
        void Exit();
        GamePlayerState GetStateType();
    }

    // 状态基类，包含上下文引用
    public abstract class BaseGameState : IGamePlayerState
    {
        protected CombatEntity combat;
        protected BaseGameState(CombatEntity context)
        {
            this.combat = context;
        }
        public abstract void Enter(GamePlayerState oldState);
        public abstract void Update(float deltaTime);
        public abstract void Exit();
        public abstract GamePlayerState GetStateType();
    }

    public class GameStateMachine
    {
        private Dictionary<GamePlayerState, IGamePlayerState> _states = new Dictionary<GamePlayerState, IGamePlayerState>();
        private IGamePlayerState _currentState;
        private CombatEntity _combat;

        public void Init(Entity entity)
        {
            _combat = entity as CombatEntity;
        }

        public void AddState<T>(GamePlayerState state) where T : BaseGameState
        {
            if (_states.ContainsKey(state))
            {
                UnityEngine.Debug.LogError($"重复添加状态，检测代码，{state}");
                return;
            }
            var playstate = (IGamePlayerState)Activator.CreateInstance(typeof(T), _combat);
            _states.Add(state, playstate);
        }

        public void AddState(GamePlayerState state, IGamePlayerState playstate)
        {
            _states.Add(state, playstate);
        }

        public void RemoveState(GamePlayerState state)
        {
            if (_states.ContainsKey(state))
            {
                _states.Remove(state);
            }
            else
            {
                UnityEngine.Debug.LogError($"无当前状态，检测代码，移除有问题: {state}");
            }
        }

        public void Update(float deltaTime)
        {
            if (_currentState != null)
            {
                _currentState.Update(deltaTime);
            }
        }

        public void ChangeState(GamePlayerState newState)
        {
            if (_currentState != null && _currentState.GetStateType() == newState) return;
            if (_states.ContainsKey(newState))
            {
                GamePlayerState oldState = GamePlayerState.None;
                UnityEngine.Debug.Log($"开始切换到状态: {newState}");
                if (_currentState != null)
                {
                    oldState = _currentState.GetStateType();
                    _currentState.Exit();
                }

                _currentState = _states[newState];
                _currentState.Enter(oldState);
            }
        }

        public GamePlayerState GetCurState()
        {
            return _currentState.GetStateType();
        }
    }

    // 具体状态实现
    public class NormalState : BaseGameState
    {
        public NormalState(CombatEntity context) : base(context){}

        public override GamePlayerState GetStateType() => GamePlayerState.NormalState;

        public override void Exit()
        {

        }

        public override void Enter(GamePlayerState oldState)
        {

        }

        public override void Update(float deltaTime)
        {

        }
    }

    public class CombatState : BaseGameState
    {
        public CombatState(CombatEntity context) : base(context)
        {

        }

        public override GamePlayerState GetStateType() => GamePlayerState.CombatState;

        public override void Enter(GamePlayerState oldState)
        {

        }

        public override void Exit()
        {

        }

        public override void Update(float deltaTime)
        {

        }
    }
}
