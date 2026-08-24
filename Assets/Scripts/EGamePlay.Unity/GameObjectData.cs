using EGamePlay;
using UnityEngine;

namespace EGamePlay.Unity
{
    /// <summary>
    /// 战斗实体 Unity 侧初始化数据（Animator、CC、装配器与角色属性）。
    /// </summary>
    public struct GameObjectData
    {
        public Animator animator;
        public CharacterController controller;
        public AgentTag agent;
        public PlayerMoveSettingSo playerSetting;

        /// <summary>InputMove 战斗侧装配；未设置则不绑定 Locomotion 依赖。</summary>
        public IInputMoveBinder inputMoveBinder;

        /// <summary>AnimDirector 使用的玩家层时间源。</summary>
        public IAnimTimeScaleSource animTimeScale;

        /// <summary>角色配置 ID，用于从 RoleAttriSetting 表读取属性。</summary>
        public int CharacterId;

        /// <summary>角色等级，用于属性计算：基础值 + 等级 * 增长值。</summary>
        public int Level;
    }
}
