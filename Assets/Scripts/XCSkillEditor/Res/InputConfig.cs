
using System.Collections.Generic;
using UnityEngine;

namespace XiaoCao
{
    public static class PlayEventMsg
    {
        public static string SetCanMove = "SetCanMove"; //移动开关
        public static string SetCanRotate = "SetCanRotate"; //旋转开关
        public static string SetUnMoveTime = "SetUnMoveTime"; //设置不能动的时间
        public static string ActivePlayerRender = "ActivePlayerRender"; //隐藏玩家Mesh
        public static string TimeStop = "TimeStop"; //顿帧
        public static string SetNoGravityT = "SetNoGravityT"; //重力开关
        public static string SetNoBreakTime = "SetNoBreakTime"; //霸体开关
        public static string PlayAudio = "PlayAudio"; //霸体开关
    }
}
