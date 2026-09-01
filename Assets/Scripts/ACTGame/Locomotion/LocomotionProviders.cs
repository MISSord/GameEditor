using EGamePlay.Unity.Locomotion;
using UnityEngine;

namespace ACTGameEditor.Locomotion
{
    /// <summary>从指定 Camera 取平面朝向；未指定时回退 Camera.main。</summary>
    public sealed class TransformCameraProvider : IMoveCameraProvider
    {
        readonly Camera _camera;

        /// <summary>绑定移动相机；传入 null 时按 Camera.main 解析。</summary>
        public TransformCameraProvider(Camera camera = null)
        {
            _camera = camera;
        }

        /// <inheritdoc />
        public Vector3 PlanarForward
        {
            get
            {
                Transform t = Resolve();
                if (t == null)
                    return Vector3.forward;

                Vector3 f = t.forward;
                f.y = 0f;
                return f.sqrMagnitude > 0.0001f ? f.normalized : Vector3.forward;
            }
        }

        /// <inheritdoc />
        public Vector3 PlanarRight
        {
            get
            {
                Vector3 f = PlanarForward;
                return new Vector3(f.z, 0f, -f.x);
            }
        }

        Transform Resolve()
        {
            if (_camera != null)
                return _camera.transform;
            return Camera.main != null ? Camera.main.transform : null;
        }
    }
}
