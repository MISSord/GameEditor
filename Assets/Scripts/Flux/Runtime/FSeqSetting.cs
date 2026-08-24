
using UnityEngine;
using EGamePlay;

namespace Flux
{
    [CreateAssetMenu(fileName = "FSeqSetting", menuName ="FSeqSetting")]
    public class FSeqSetting : ScriptableObject
    {
        public AgentModelType agentName;

        public RuntimeAnimatorController targetAnimtorController;

    }
}