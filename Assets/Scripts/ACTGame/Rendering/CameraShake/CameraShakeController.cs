using UnityEngine;
using EGamePlay;

namespace ACTGameEditor
{
    /// <summary>
    /// 镜头震动状态机（Trauma 模型）：只维护 trauma/noise/FOV 并产出偏移，
    /// 最终 transform 与 FOV 由 CameraManager.LateUpdate 合成（单一写入口）。
    /// 走 unscaled 时间：断裂 / HitStop 里照常震，暂停（IsGameplayPaused）时冻结。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CameraShakeController : MonoBehaviour
    {
        static CameraShakeController _instance;

        /// <summary>
        /// 全局实例：场景未预挂时自动创建（与 GraphicsFxService 同模式）。
        /// 仅首次访问走 FindObjectOfType，之后走 _instance 快路径。
        /// </summary>
        public static CameraShakeController Instance
        {
            get
            {
                if (_instance != null)
                    return _instance;

                _instance = FindObjectOfType<CameraShakeController>();
                if (_instance != null)
                    return _instance;

                var go = new GameObject(nameof(CameraShakeController));
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<CameraShakeController>();
                return _instance;
            }
        }

        float _trauma;
        float _traumaDecay = 2.2f;
        float _posAmp, _rotAmp, _fovPunch;
        float _fovAge = float.MaxValue;
        float _fovDuration;
        float _noiseTime;
        float _frequency = 14f;
        Vector3 _posOffset, _rotOffset;
        float _fovOffset;
        Vector3 _kickDirWorld;
        float _kickAmp;
        float _kickAge = float.MaxValue;
        float _kickDuration;
        Vector3 _kickOffsetWorld;

        void Awake()
        {
            _instance = this;
        }

        void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        /// <summary>
        /// 叠加一次震动；profile 为空或两个开关都关时忽略。
        /// ignoreGate 用于调试面板等强制播放场景（绕过 GraphicsFx 开关）。
        /// kickDirectionWorld / kickAmplitude / kickDuration：方向性 Kick（沿命中方向的镜头推撞，回落后精确归零）。
        /// </summary>
        public void Play(CameraShakeProfile profile, bool ignoreGate = false,
            Vector3 kickDirectionWorld = default, float kickAmplitude = 0f, float kickDuration = 0f)
        {
            if (profile == null)
                return;

            bool shakeOn = ignoreGate || GraphicsFxService.Query(GraphicsFxId.ScreenShake);
            bool fovOn = ignoreGate || GraphicsFxService.Query(GraphicsFxId.FovPunch);
            if (!shakeOn && !fovOn)
                return;

            if (shakeOn)
            {
                _trauma = Mathf.Clamp01(_trauma + profile.Trauma);
                _traumaDecay = profile.TraumaDecay;
                _frequency = profile.Frequency;
                _posAmp = Mathf.Max(_posAmp, profile.PositionAmplitude);
                _rotAmp = Mathf.Max(_rotAmp, profile.RotationAmplitude);

                if (kickAmplitude > 0f && kickDuration > 0f)
                {
                    _kickDirWorld = kickDirectionWorld.sqrMagnitude > 0.0001f
                        ? kickDirectionWorld.normalized
                        : Vector3.zero;
                    _kickAmp = kickAmplitude;
                    _kickAge = 0f;
                    _kickDuration = kickDuration;
                }
            }

            if (fovOn)
            {
                _fovPunch = profile.FovPunch;
                _fovAge = 0f;
                _fovDuration = Mathf.Max(0.02f, profile.FovDuration);
            }
        }

        void Update()
        {
            float dt = TimeScaleEffectManager.IsGameplayPaused ? 0f : Time.unscaledDeltaTime;

            // 创伤线性衰减，实际强度取平方（轻伤微颤、重伤猛震）
            _trauma = Mathf.Max(0f, _trauma - _traumaDecay * dt);
            float intensity = _trauma * _trauma;

            // Perlin 采样（通道错开，位移/旋转不同步，观感连续不闪跳）
            _noiseTime += dt * _frequency;
            float t = _noiseTime;
            _posOffset = intensity * _posAmp * new Vector3(Perlin(t, 7.13f), Perlin(11.3f, t), Perlin(t, t));
            _rotOffset = intensity * _rotAmp * new Vector3(
                Perlin(t + 3.7f, t), Perlin(t, t + 5.9f), Perlin(t + 8.1f, t + 1.7f));

            // FOV 脉冲：立即到位，二次回落
            if (_fovAge < _fovDuration)
            {
                _fovAge += dt;
                float k = 1f - Mathf.Clamp01(_fovAge / _fovDuration);
                _fovOffset = _fovPunch * k * k;
            }
            else
            {
                _fovOffset = 0f;
            }

            // 方向性 Kick：命中瞬间推到满幅，二次包络回落，结束时精确归零（不产生累计漂移）
            if (_kickAge < _kickDuration)
            {
                _kickAge += dt;
                float k = 1f - Mathf.Clamp01(_kickAge / _kickDuration);
                _kickOffsetWorld = _kickDirWorld * (_kickAmp * k * k);
            }
            else
            {
                _kickOffsetWorld = Vector3.zero;
            }
        }

        static float Perlin(float x, float y) => Mathf.PerlinNoise(Mathf.Repeat(x, 1f), Mathf.Repeat(y, 1f)) * 2f - 1f;

        /// <summary>CameraManager 合成用：本帧偏移（位移/旋转为相机本地空间；kick 为世界空间；FOV 单位度）。</summary>
        public void GetOffset(out Vector3 posLocal, out Vector3 rotEulerLocal, out float fovOffset, out Vector3 kickWorld)
        {
            posLocal = _posOffset;
            rotEulerLocal = _rotOffset;
            fovOffset = _fovOffset;
            kickWorld = _kickOffsetWorld;
        }
    }
}
