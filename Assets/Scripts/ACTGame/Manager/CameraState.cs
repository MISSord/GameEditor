using UnityEngine;

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
            if(oldCameraState == null)
            {
                machine.MainCamera.localRotation = Quaternion.Euler(new Vector2(45f, 0f));
            }
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
                this.lookRotation = machine.MainCamera.localRotation;
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

    // 锁定视角状态（崩坏3风格，逻辑委托给 LockSystem）
    public class LockOnState : CameraState
    {
        [Tooltip("镜头转向目标的平滑时间")]
        public float lockSmoothTime = 0.1f;

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

        public override void OnLateUpdate()
        {
            if (LockSystem.Instance == null || !LockSystem.Instance.IsLocked) return;

            var target = LockSystem.Instance.LockedTarget;
            if (target == null) return;

            HandleCameraLockRotation(target);
        }

        void HandleCameraLockRotation(ICameraTarget currentTarget)
        {
            if (machine.CurrentTarget == null) return;

            Vector3 playerPos = machine.CurrentTarget.GetPlayerPos();
            Vector3 dirToTarget = (currentTarget.GetCameraTargetPos() - playerPos).normalized;

            lookPosition = -dirToTarget * machine.Distance + playerPos;
            Quaternion targetLook = Quaternion.LookRotation(dirToTarget);
            lookRotation = Quaternion.Slerp(machine.MainCamera.rotation, targetLook, GameTimeManager.CameraDelta * (1f / lockSmoothTime));
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
