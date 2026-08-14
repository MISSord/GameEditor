using UnityEngine;

namespace ACTGameEditor
{
    // 这个脚本挂在与 Animator 同一个 GameObject 上
    public class RootMotionDriver : MonoBehaviour
    {
        private Animator _animator;

        // 开关：由逻辑层控制是否启用 RootMotion
        public bool useRootMotion = false;

        // 缓存当前的位移和旋转增量，供逻辑层读取
        public Vector3 DeltaPosition { get; private set; }
        public Quaternion DeltaRotation { get; private set; } = Quaternion.identity;

        void Awake()
        {
            _animator = GetComponent<Animator>();
        }

        // 【核心】：这是 Unity 每一帧计算完 Graph 后调用的回调
        private void OnAnimatorMove()
        {
            // 1. 获取 Graph 计算出的 Root Motion 数据
            // 即使是 PlayableGraph 驱动的，结果依然会汇总到这里
            DeltaPosition = _animator.deltaPosition;
            DeltaRotation = _animator.deltaRotation;

            // 2. 决定如何处理
            if (useRootMotion)
            {
                transform.position += DeltaPosition;
                transform.rotation *= DeltaRotation;
            }
        }

        // 提供给逻辑层的方法：每帧清理累积量（如果需要手动模拟物理）
        public void ResetDeltas()
        {
            DeltaPosition = Vector3.zero;
            DeltaRotation = Quaternion.identity;
        }
    }
}