using UnityEngine;

namespace ACTGameEditor.Locomotion
{
    /// <summary>
    /// 本帧本地玩家设备快照。只由 <see cref="ConfigurableInputManager.Sample"/> 写入。
    /// 电机只认 MoveAxis；闪避朝向另走 LastAim，不要把本结构写进迈步意图。
    /// </summary>
    public readonly struct PlayerInputSnapshot
    {
        /// <summary>合并后的平面轴（键盘 + 摇杆），未做相机相对。</summary>
        public Vector2 MoveAxis { get; }

        /// <summary>Ctrl：本帧按下（走路 / 慢跑切换）。</summary>
        public bool WalkTogglePressed { get; }

        /// <summary>Shift：本帧按下（慢跑中切入快跑）。</summary>
        public bool SprintPressed { get; }

        /// <summary>采样时的 <see cref="Time.frameCount"/>。</summary>
        public int Frame { get; }

        /// <summary>构造一帧快照。</summary>
        public PlayerInputSnapshot(Vector2 moveAxis, bool walkTogglePressed, bool sprintPressed, int frame)
        {
            MoveAxis = moveAxis;
            WalkTogglePressed = walkTogglePressed;
            SprintPressed = sprintPressed;
            Frame = frame;
        }
    }
}
