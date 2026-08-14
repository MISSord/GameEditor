using UnityEngine;

namespace ACTGameEditor.Locomotion
{
    /// <summary>Locomotion 测试场景用的极简跟随相机。</summary>
    public sealed class LocomotionTestCameraFollow : MonoBehaviour
    {
        [SerializeField]
        Transform target;

        [SerializeField]
        Vector3 offset = new Vector3(0f, 3.2f, -6f);

        [SerializeField]
        float followSpeed = 8f;

        /// <summary>绑定跟随目标。</summary>
        public void SetTarget(Transform newTarget) => target = newTarget;

        void LateUpdate()
        {
            if (target == null)
                return;

            Vector3 desired = target.position + offset;
            transform.position = Vector3.Lerp(
                transform.position,
                desired,
                1f - Mathf.Exp(-followSpeed * Time.deltaTime));
            transform.LookAt(target.position + Vector3.up * 1.2f);
        }
    }
}
