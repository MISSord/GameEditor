using System.Collections.Generic;
using System.Data;
using System.Numerics;
using UnityEngine;
using UnityEngine.InputSystem;

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
        //是否有输入记录
        bool IsHadInputRecords();
        //清空输入记录
        void InputRecordsClear();
        //添加输入记录
        void AddInputRecord(InputListernType cmd, PressType type, InputCallBackType inputCallBackType);
        void ChangeInputMoveState(bool state);
        //判断是否有某种类型（这个和下面的区别是，下面只会查询，不会移除，而这个再找到后会移除掉）
        /// <param name="customTimeout">预输入年龄上限（秒），≤0 表示使用 NormalActPlayer.InputTimeout</param>
        bool CheckAndConsume(InputListernType cmd, PressType type, InputCallBackType inputCallBackType = InputCallBackType.Performed, float customTimeout = -1f);
        //查看有某种类型的输入
        bool HasValidInput(InputListernType cmd, PressType type, InputCallBackType inputCallBackType = InputCallBackType.Performed);
        /// <summary>连招窗按白名单解析预输入，只消费赢家通道。</summary>
        bool TryResolveEdges(List<SkillInputData> edges, out int skillId, out int sort);
    }
}
