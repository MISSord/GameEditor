using EGamePlay.Unity;
using UnityEngine;

namespace ACTGameEditor.Combat.Ai
{
    /// <summary>人机移动轴。Close=(0,1) 沿平面基前进；不读键盘。</summary>
    public sealed class AiMoveInputProvider : IMoveInputProvider
    {
        Vector2 _axis;

        /// <inheritdoc />
        public Vector2 MoveAxis => _axis;

        /// <inheritdoc />
        public bool WalkTogglePressed => false;

        /// <inheritdoc />
        public bool SprintPressed => false;

        /// <summary>沿平面基前进（慢跑）。</summary>
        public void SetClose() => SetMove(0f, 1f);

        /// <summary>沿平面基后撤。</summary>
        public void SetBackOff() => SetMove(0f, -1f);

        /// <summary>停步。</summary>
        public void SetStop() => _axis = Vector2.zero;

        /// <summary>
        /// 写入平面轴并限制在单位圆内。x=右（环绕），y=前（逼近）。
        /// </summary>
        public void SetMove(float x, float y)
        {
            _axis.x = x;
            _axis.y = y;
            float magSq = _axis.sqrMagnitude;
            if (magSq > 1f)
                _axis *= 1f / Mathf.Sqrt(magSq);
        }

        /// <summary>给 AnimDirector 用的无分配读取。</summary>
        public Vector2 GetAxis() => _axis;
    }

    /// <summary>平面基 = 朝向目标，(0,1) 逼近而不是跟玩家镜头走。</summary>
    public sealed class AiAimBasisCameraProvider : IMoveCameraProvider
    {
        Vector3 _forward = Vector3.forward;
        Vector3 _right = Vector3.right;

        /// <inheritdoc />
        public Vector3 PlanarForward => _forward;

        /// <inheritdoc />
        public Vector3 PlanarRight => _right;

        /// <summary>写入已展平、已归一的朝向目标方向。</summary>
        public void SetPlanarForward(Vector3 planarForward)
        {
            if (planarForward.sqrMagnitude < 0.0001f)
                return;
            _forward = planarForward;
            _right = new Vector3(_forward.z, 0f, -_forward.x);
        }
    }

    /// <summary>战斗中看向黑板目标，供电机 strafing / AutoRotate。</summary>
    public sealed class AiTargetFacingProvider : IMoveFacingProvider
    {
        Vector3 _point;
        bool _has;

        /// <summary>设置朝向点（通常是焦点玩家位置）。</summary>
        public void SetPoint(Vector3 worldPoint)
        {
            _point = worldPoint;
            _has = true;
        }

        /// <summary>清除朝向点，电机改朝移动方向。</summary>
        public void Clear() => _has = false;

        /// <inheritdoc />
        public bool TryGetFacingPoint(out Vector3 worldPoint)
        {
            worldPoint = _point;
            return _has;
        }
    }
}
