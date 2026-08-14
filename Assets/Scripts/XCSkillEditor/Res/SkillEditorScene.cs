using Sirenix.OdinInspector;
using UnityEngine;
using XiaoCao;
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
        var setting = ResFinder.SoUsingFinder.DebugSo;
        PlayerManager.Instance.AddFakePlayer(startPos, setting.AI, agentTag);
    }
}
#endif