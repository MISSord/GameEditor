using UnityEngine;

namespace ACTGameEditor
{
    /// <summary>
    /// 显现遮罩全局状态：球形脉冲 / 手电筒圆锥共用，供 ACT/RevealMasked 读取。
    /// </summary>
    public static class RevealMaskState
    {
        static readonly int SphereActiveId = Shader.PropertyToID("_RevealSphereActive");
        static readonly int SphereCenterId = Shader.PropertyToID("_RevealSphereCenter");
        static readonly int SphereRadiusId = Shader.PropertyToID("_RevealSphereRadius");
        static readonly int ConeActiveId = Shader.PropertyToID("_RevealConeActive");
        static readonly int ConeOriginId = Shader.PropertyToID("_RevealConeOrigin");
        static readonly int ConeDirId = Shader.PropertyToID("_RevealConeDir");
        static readonly int ConeRangeId = Shader.PropertyToID("_RevealConeRange");
        static readonly int ConeCosOuterId = Shader.PropertyToID("_RevealConeCosOuter");
        static readonly int ConeCosInnerId = Shader.PropertyToID("_RevealConeCosInner");

        /// <summary>球形遮罩是否开启。</summary>
        public static bool SphereActive { get; private set; }

        /// <summary>圆锥遮罩是否开启。</summary>
        public static bool ConeActive { get; private set; }

        /// <summary>写入球形参数。</summary>
        public static void SetSphere(bool active, Vector3 center, float radius)
        {
            SphereActive = active;
            Shader.SetGlobalFloat(SphereActiveId, active ? 1f : 0f);
            Shader.SetGlobalVector(SphereCenterId, center);
            Shader.SetGlobalFloat(SphereRadiusId, Mathf.Max(0f, radius));
        }

        /// <summary>写入圆锥（手电筒）参数。angleDegrees 为全锥角。</summary>
        public static void SetCone(bool active, Vector3 origin, Vector3 direction, float range, float angleDegrees, float softEdge01)
        {
            ConeActive = active;
            Vector3 dir = direction.sqrMagnitude > 1e-6f ? direction.normalized : Vector3.forward;
            float halfRad = Mathf.Max(0.1f, angleDegrees * 0.5f) * Mathf.Deg2Rad;
            float cosOuter = Mathf.Cos(halfRad);
            float soft = Mathf.Clamp01(softEdge01);
            float innerAngle = halfRad * Mathf.Max(0.05f, 1f - soft * 0.85f);
            float cosInner = Mathf.Cos(innerAngle);
            if (cosInner <= cosOuter)
                cosInner = Mathf.Min(1f, cosOuter + 0.02f);

            Shader.SetGlobalFloat(ConeActiveId, active ? 1f : 0f);
            Shader.SetGlobalVector(ConeOriginId, origin);
            Shader.SetGlobalVector(ConeDirId, dir);
            Shader.SetGlobalFloat(ConeRangeId, Mathf.Max(0.01f, range));
            Shader.SetGlobalFloat(ConeCosOuterId, cosOuter);
            Shader.SetGlobalFloat(ConeCosInnerId, cosInner);
        }

        /// <summary>关闭全部遮罩（隐藏所有 Shader 驱动显现物）。</summary>
        public static void ClearAll()
        {
            SetSphere(false, Vector3.zero, 0f);
            SetCone(false, Vector3.zero, Vector3.forward, 0f, 30f, 0.1f);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init()
        {
            ClearAll();
        }
    }
}
