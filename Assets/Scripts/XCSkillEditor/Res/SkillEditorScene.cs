using Sirenix.OdinInspector;
using UnityEngine;
using EGamePlay;
using ACTGameEditor;

#if UNITY_EDITOR

public class SkillEditorScene : MonoBehaviour
{
    [Header("添加一个Npc")]
    public AgentModelType agentName;
    public AgentTag agentTag;
    public Vector3 startPos = Vector3.zero;

    [Button("添加Npc")]
    public void AddNpc()
    {
        PlayerManager.Instance.AddFakePlayer(startPos, false, agentTag);
    }
}
#endif