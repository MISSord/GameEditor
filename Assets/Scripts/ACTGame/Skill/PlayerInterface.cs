using System.Collections.Generic;
using UnityEngine;

namespace ACTGameEditor
{
    public interface ICameraTarget
    {
        Transform GetCameraTarget();
        Transform GetPlayerTransform();
        UnityEngine.Vector3 GetPlayerPos();
        UnityEngine.Vector3 GetCameraTargetPos();
    }

    public interface IAttackPlayer
    {
        //添加输入记录
        void AddInputRecord(InputListernType cmd, PressType type, InputCallBackType inputCallBackType);
        void ChangeInputMoveState(bool state);
        /// <summary>连招窗按白名单解析预输入，只消费赢家通道。</summary>
        bool TryResolveEdges(List<SkillInputData> edges, out int skillId, out int sort);
    }
}
