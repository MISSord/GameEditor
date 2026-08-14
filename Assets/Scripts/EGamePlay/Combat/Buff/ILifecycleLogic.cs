using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EGamePlay.Combat
{
    public interface ILifecycleLogic
    {
        // 每一帧轮询（处理时间、特殊条件）
        // 返回 true 表示 Buff 应该结束
        bool OnUpdate(float deltaTime);
    }
}
