using UnityEngine;
using XiaoCao;

public struct GameObjectData
{
    public Animator animator;
    public CharacterController controller;
    public AgentTag agent;
    public PlayerMoveSettingSo playerSetting;
    /// <summary>角色配置 ID，用于从 RoleAttriSetting 表读取属性。</summary>
    public int CharacterId;
    /// <summary>角色等级，用于属性计算：基础值 + 等级 * 增长值。</summary>
    public int Level;
}
