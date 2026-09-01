using UnityEngine;
using ACTGameEditor;

namespace ACTGameEditor.Locomotion
{
    /// <summary>相机相对水平移动方向（与 LocomotionMotor 同一套轴）。</summary>
    public static class CameraRelativeMove
    {
        const float DeadZoneSqr = 0.01f;

        static Vector3 _lastWorldDir;
        static bool _hasLastWorldDir;

        /// <summary>
        /// 有非零移动轴时记下世界水平方向。
        /// 闪避会关掉移动核，松杆后第二次闪避仍要吃技能中点过的方向。
        /// </summary>
        public static void LatchFromAxis(Vector2 axis)
        {
            if (!AxisToWorldDir(axis, out Vector3 worldDir))
                return;
            _lastWorldDir = worldDir;
            _hasLastWorldDir = true;
        }

        /// <summary>当帧快照的相机相对水平方向；无输入返回 false。不重新采设备。</summary>
        static bool TryGetWorldDir(out Vector3 worldDir)
        {
            ConfigurableInputManager mgr = ConfigurableInputManager.Instance;
            Vector2 axis = mgr != null ? mgr.Snapshot.MoveAxis : Vector2.zero;
            return AxisToWorldDir(axis, out worldDir);
        }

        /// <summary>当前摇杆方向；松开则用最后一次有效指向（含技能期间点过的方向）。</summary>
        public static bool TryGetWorldDirOrLast(out Vector3 worldDir)
        {
            if (TryGetWorldDir(out worldDir))
                return true;
            if (!_hasLastWorldDir)
                return false;
            worldDir = _lastWorldDir;
            return true;
        }

        static bool AxisToWorldDir(Vector2 axis, out Vector3 worldDir)
        {
            worldDir = default;
            if (axis.sqrMagnitude < DeadZoneSqr)
                return false;

            Vector3 forward = Vector3.forward;
            Transform cam = Camera.main != null ? Camera.main.transform : null;
            if (cam != null)
            {
                forward = cam.forward;
                forward.y = 0f;
                if (forward.sqrMagnitude > 0.0001f)
                    forward.Normalize();
                else
                    forward = Vector3.forward;
            }

            Vector3 right = new Vector3(forward.z, 0f, -forward.x);
            worldDir = axis.x * right + axis.y * forward;
            worldDir.y = 0f;
            if (worldDir.sqrMagnitude < 0.0001f)
                return false;

            worldDir.Normalize();
            return true;
        }
    }
}
