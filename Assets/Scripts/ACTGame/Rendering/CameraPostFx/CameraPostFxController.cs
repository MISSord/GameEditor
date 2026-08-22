using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ACTGameEditor
{
    /// <summary>
    /// 战斗镜头后处理：Chromatic Aberration + RadialBlur pulse，与 HitStop 联动。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CameraPostFxController : MonoBehaviour
    {
        static CameraPostFxController _instance;

        [SerializeField]
        CombatImpactProfile defaultProfile = new CombatImpactProfile();

        [SerializeField]
        bool respectGraphicsFxGate = true;

        readonly PostFxPulse _caPulse = new PostFxPulse();
        readonly PostFxPulse _radialPulse = new PostFxPulse();

        Vector2 _radialCenter = new Vector2(0.5f, 0.5f);
        int _radialSampleCount = 10;
        float _caRestIntensity;

        /// <summary>全局实例（可选）。</summary>
        public static CameraPostFxController Instance => _instance;

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(this);
                return;
            }

            _instance = this;
        }

        void Start()
        {
            CacheCaRestIntensity();
        }

        void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
                RadialBlurState.Clear();
                ApplyChromaticAberration(_caRestIntensity);
            }
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;

            if (_caPulse.Tick(dt, out float caValue))
                ApplyChromaticAberration(caValue);

            if (_radialPulse.Tick(dt, out float radialValue))
            {
                RadialBlurState.Center = _radialCenter;
                RadialBlurState.SampleCount = _radialSampleCount;
                RadialBlurState.Intensity = radialValue;
            }
            else if (_radialPulse.WasActive && !Mathf.Approximately(RadialBlurState.Intensity, 0f))
            {
                RadialBlurState.Clear();
            }
        }

        /// <summary>
        /// 播放战斗冲击（CA + RadialBlur）。
        /// </summary>
        public void PlayCombatImpact(CombatImpactProfile profile = null)
        {
            CombatImpactProfile p = profile ?? defaultProfile ?? new CombatImpactProfile();
            if (p.Duration <= 0f)
                return;

            _radialCenter = p.RadialCenter;
            _radialSampleCount = p.RadialSampleCount;

            if (IsAllowed(GraphicsFxId.ChromaticAberration))
                _caPulse.Start(_caRestIntensity, p.ChromaticAberrationPeak, p.Duration);

            if (IsAllowed(GraphicsFxId.RadialBlur))
                _radialPulse.Start(0f, p.RadialBlurPeak, p.Duration);
        }

        /// <summary>
        /// HitStop 联动入口（由 HitStop 或 TimeScale 事件调用）。
        /// </summary>
        public void PlayHitStopImpact(float hitStopDuration)
        {
            if (hitStopDuration <= 0f)
                return;

            CombatImpactProfile p = (defaultProfile ?? new CombatImpactProfile()).ScaledByHitStop(hitStopDuration);
            PlayCombatImpact(p);
        }

        /// <summary>
        /// 静态便捷调用（无实例时静默跳过）。
        /// </summary>
        public static void TryPlayHitStopImpact(float hitStopDuration)
        {
            if (_instance != null)
                _instance.PlayHitStopImpact(hitStopDuration);
        }

        /// <summary>
        /// 静态便捷调用战斗冲击。
        /// </summary>
        public static void TryPlayCombatImpact(CombatImpactProfile profile = null)
        {
            if (_instance != null)
                _instance.PlayCombatImpact(profile);
        }

        bool IsAllowed(GraphicsFxId id)
        {
            if (!respectGraphicsFxGate)
                return true;

            return GraphicsFxService.Query(id);
        }

        void CacheCaRestIntensity()
        {
            if (GraphicsFxApplier.TryGetChromaticAberration(out ChromaticAberration ca))
                _caRestIntensity = ca.intensity.value;
        }

        void ApplyChromaticAberration(float intensity)
        {
            if (!GraphicsFxApplier.TryGetChromaticAberration(out ChromaticAberration ca))
                return;

            ca.intensity.overrideState = true;
            ca.intensity.value = Mathf.Clamp01(intensity);
        }

        sealed class PostFxPulse
        {
            float _peak;
            float _rest;
            float _age;
            float _duration;
            bool _active;

            public bool Active => _active;
            public bool WasActive { get; private set; }

            public void Start(float rest, float peak, float duration)
            {
                _rest = rest;
                _peak = peak;
                _age = 0f;
                _duration = Mathf.Max(0.02f, duration);
                _active = true;
                WasActive = true;
            }

            public bool Tick(float unscaledDelta, out float value)
            {
                WasActive = _active;
                value = _rest;

                if (!_active)
                    return false;

                _age += unscaledDelta;
                float t = Mathf.Clamp01(_age / _duration);
                float envelope = 1f - t;
                envelope *= envelope;
                value = Mathf.Lerp(_rest, _peak, envelope);

                if (_age >= _duration)
                {
                    _active = false;
                    value = _rest;
                }

                return true;
            }
        }
    }
}
