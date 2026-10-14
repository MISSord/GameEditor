using UnityEngine;
using EGamePlay;

namespace ACTGameEditor
{
    public enum CameraEnumState 
    {
        FreeLook,
        LockLook,
        FullyFixedLook,  // 完全固定，指的是摄像机的位置和视角完全固定，没法调整
        TargetFixedLook, // 观察目标，摄像机位置固定，视角随着目标的位置进行调整
        MobaLook,        // 类Moba视角，摄像机角度固定，其坐标可以沿着一个平面进行移动
    }


    public abstract class CameraState
    {
        protected CameraManager machine;
        //每帧计算好参数，Manager去设置位置与角度
        public Vector3 lookPosition;
        public Quaternion lookRotation;

        public CameraState(CameraManager machine)
        {
            this.machine = machine;
        }

        public virtual void OnEnter(CameraState oldCameraState) { }

        public abstract void OnUpdate();

        public virtual void OnLateUpdate() { }

        public virtual void OnExit() { }

        public abstract CameraEnumState GetCameraState();
    }


    // 自由视角状态
    public class FreeLookState : CameraState
    {
        //[SerializeField, Min(0f)]
        float focusRadius = 0f;

        //[SerializeField, Range(0f, 1f)]
        float focusCentering = 0.1f;

        //[SerializeField, Range(1f, 360f)]
        float rotationSpeed = 90f;

        //[SerializeField, Range(-89f, 89f)]
        float minVerticalAngle = -45f, maxVerticalAngle = 45f;

        //[SerializeField, Min(0f)]
        float alignDelay = 5f;

        //[SerializeField, Range(0f, 90f)]
        float alignSmoothRange = 45f;

        Vector2 orbitAngles = new Vector2(45f, 0f);

        Vector2 oldInput = Vector2.zero;

        float lastManualRotationTime;

        public FreeLookState(CameraManager machine) : base(machine) { }

        public override void OnEnter(CameraState oldCameraState)
        {
            if (oldCameraState == null)
            {
                orbitAngles = new Vector2(45f, 0f);
                lookRotation = Quaternion.Euler(orbitAngles);
            }
            else
            {
                // 从干净朝向同步 orbit 角（无缝接管），绝不读带震屏的相机 transform
                SyncOrbitAngles(machine.CleanLookRotation);
                lookRotation = machine.CleanLookRotation;
            }
        }

        /// <summary>
        /// 把轨道角对齐到干净朝向。解锁时把混合结果写回基准，避免镜头甩回锁定前的旧角度。
        /// </summary>
        public void RebaseOrbit(Quaternion rotation)
        {
            SyncOrbitAngles(rotation);
            lookRotation = Quaternion.Euler(orbitAngles);
        }

        /// <summary>把干净朝向转回 orbitAngles（pitch 归一到 [-180,180)，yaw 由 ConstrainAngles 归一到 [0,360)）。</summary>
        void SyncOrbitAngles(Quaternion rotation)
        {
            Vector3 euler = rotation.eulerAngles;
            orbitAngles.x = euler.x > 180f ? euler.x - 360f : euler.x;
            orbitAngles.y = euler.y;
            ConstrainAngles();
        }

        public override void OnUpdate(){}

        public override void OnLateUpdate()
        {
            UpdateFocusPoint();
            OrbitCameraLateUpdate();
        }

        private void OrbitCameraLateUpdate()
        {
            if (ManualRotation()) //|| AutomaticRotation()
            {
                ConstrainAngles();
                this.lookRotation = Quaternion.Euler(orbitAngles);
            }
            else
            {
                this.lookRotation = machine.CleanLookRotation;
            }

            Vector3 lookDirection = this.lookRotation * Vector3.forward;
            this.lookPosition = machine.focusPoint - lookDirection * machine.Distance;

        }

        void UpdateFocusPoint()
        {
            machine.previousFocusPoint = machine.focusPoint;
            Vector3 targetPoint = machine.focus.position;
            //平滑的跟踪目标
            if (focusRadius > 0f)
            {
                float distance = Vector3.Distance(targetPoint, machine.focusPoint);
                float t = 1f;
                if (distance > 0.01f && focusCentering > 0f)
                {
                    t = Mathf.Pow(1f - focusCentering, GameTimeManager.CameraDelta);
                }
                if (distance > focusRadius)
                {
                    t = Mathf.Min(t, focusRadius / distance);
                }
                machine.focusPoint = Vector3.Lerp(targetPoint, machine.focusPoint, t);
            }
            else
            {
                machine.focusPoint = targetPoint;
            }
        }

        bool ManualRotation()
        {
            float x = Input.GetAxis("Mouse X");
            float y = Input.GetAxis("Mouse Y");

            const float e = 0.001f;
            if (oldInput.x - x < -e || oldInput.x - x > e || oldInput.y - y < -e || oldInput.y - y > e)
            {
                //-y和x，是为了符合大部分人的对镜头操控的习惯
                //未来可以加入如X轴反转，Y轴反转这样的设计
                orbitAngles += rotationSpeed * GameTimeManager.CameraDelta * new Vector2(-y, x);
                lastManualRotationTime = Time.unscaledTime;
                oldInput.x = x;
                oldInput.y = y;
                return true;
            }
            return false;
        }

        //是否自动旋转
        bool AutomaticRotation()
        {
            if (Time.unscaledTime - lastManualRotationTime < alignDelay)
            {
                return false;
            }

            Vector2 movement = new Vector2(
                machine.focusPoint.x - machine.previousFocusPoint.x,
                machine.focusPoint.z - machine.previousFocusPoint.z
            );

            float movementDeltaSqr = movement.sqrMagnitude;
            if (movementDeltaSqr < 0.0001f)
            {
                return false;
            }

            float headingAngle = GetAngle(movement / Mathf.Sqrt(movementDeltaSqr));
            float deltaAbs = Mathf.Abs(Mathf.DeltaAngle(orbitAngles.y, headingAngle));
            float rotationChange = rotationSpeed * Mathf.Min(GameTimeManager.CameraDelta, movementDeltaSqr);
            if (deltaAbs < alignSmoothRange)
            {
                rotationChange *= deltaAbs / alignSmoothRange;
            }
            else if (180f - deltaAbs < alignSmoothRange)
            {
                rotationChange *= (180f - deltaAbs) / alignSmoothRange;
            }
            orbitAngles.y = Mathf.MoveTowardsAngle(orbitAngles.y, headingAngle, rotationChange);
            return true;
        }

        void ConstrainAngles()
        {
            orbitAngles.x = Mathf.Clamp(orbitAngles.x, minVerticalAngle, maxVerticalAngle);
            if (orbitAngles.y < 0f)
            {
                orbitAngles.y += 360f;
            }
            else if (orbitAngles.y >= 360f)
            {
                orbitAngles.y -= 360f;
            }
        }

        static float GetAngle(Vector2 direction)
        {
            float angle = Mathf.Acos(direction.y) * Mathf.Rad2Deg;
            return direction.x < 0f ? 360f - angle : angle;
        }

        public override CameraEnumState GetCameraState() => CameraEnumState.FreeLook;
    }

    // 锁定视角：不再独占状态机，只作为混合层的朝向提议源（Q/E 与目标校验仍走这里）。
    public class LockOnState : CameraState
    {
        public LockOnState(CameraManager machine) : base(machine) { }

        public override void OnEnter(CameraState oldCameraState) { }

        public override void OnUpdate()
        {
            var ls = LockSystem.Instance;
            if (ls == null) return;

            if (Input.GetKeyDown(KeyCode.Q))
                ls.SwitchTargetPrev();
            else if (Input.GetKeyDown(KeyCode.E))
                ls.SwitchTargetNext();

            ls.ValidateLockedTarget();
        }

        public override void OnLateUpdate() { }

        /// <summary>
        /// 提出锁定朝向（玩家根位置 → 目标相机点）。不写基准，失败表示当前无有效锁。
        /// </summary>
        public bool TryProposeLook(out float pitch, out float yaw)
        {
            pitch = 0f;
            yaw = 0f;
            var ls = LockSystem.Instance;
            if (ls == null || !ls.IsLocked)
                return false;

            var target = ls.LockedTarget;
            if (target == null || machine.CurrentTarget == null)
                return false;

            Vector3 dirToTarget = target.GetCameraTargetPos() - machine.CurrentTarget.GetPlayerPos();
            if (dirToTarget.sqrMagnitude < 0.0001f)
                return false;

            Vector3 euler = Quaternion.LookRotation(dirToTarget.normalized, Vector3.up).eulerAngles;
            pitch = euler.x > 180f ? euler.x - 360f : euler.x;
            yaw = euler.y;
            return true;
        }

        public override CameraEnumState GetCameraState() => CameraEnumState.LockLook;
    }

    //  固定视角状态
    //public class FixedLockState : CameraState
    //{
    //    private Vector3 lock
    //    private float moveSpeed = 5f;

    //    public FixedLockState(CameraManager machine, Transform anchor) : base(machine)
    //    {
    //        this.fixedAnchor = anchor;
    //    }

    //    public override void OnEnter(CameraState oldCameraState)
    //    {

    //    }

    //    public override void OnUpdate()
    //    {
    //        // 既然是固定视角，我们可能不再跟随玩家，或者以特定方式跟随
    //        // 这里演示：相机平滑飞向固定点，并与固定点保持一致的旋转

    //        this.lookPosition = Vector3.Lerp(machine.MainCamera.position, fixedAnchor.position, Time.deltaTime * moveSpeed);
    //        this.lookRotation = Quaternion.Slerp(machine.MainCamera.rotation, fixedAnchor.rotation, Time.deltaTime * moveSpeed);
    //    }

    //    public override CameraEnumState GetCameraState() => CameraEnumState.FixedLook;

    //}
}
