using EGamePlay.Unity.Locomotion;
using UnityEngine;
namespace ACTGameEditor.Locomotion
{
    /// <summary>
    /// 独立可挂接的移动驱动：不依赖 Scene.prefab / 战斗 ECS。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class CharacterLocomotion : MonoBehaviour
    {
        [SerializeField]
        LocomotionConfig config;

        [SerializeField]
        Animator animator;

        [SerializeField]
        Camera moveCamera;

        [SerializeField]
        bool locomotionEnabled = true;

        readonly LocomotionMotor _motor = new();
        TransformCameraProvider _cameraProvider;
        IMoveInputProvider _inputProvider;
        IMoveGate _gate = AlwaysAllowMoveGate.Instance;
        ILocomotionTimeSource _timeSource;
        ILocomotionStateSink _stateSink = NullLocomotionStateSink.Instance;
        CharacterController _controller;
        bool _bound;

        /// <summary>底层移动核（战斗桥可共用同一实例逻辑）。</summary>
        public LocomotionMotor Motor => _motor;

        /// <summary>是否落地。</summary>
        public bool IsGrounded => _motor.IsGrounded;

        /// <summary>开关移动输入与水平位移。</summary>
        public bool LocomotionEnabled
        {
            get => locomotionEnabled;
            set
            {
                locomotionEnabled = value;
                _motor.LocomotionEnabled = value;
            }
        }

        void Awake()
        {
            _controller = GetComponent<CharacterController>();
            if (animator == null)
                animator = GetComponentInChildren<Animator>();

            EnsureBound();
        }

        void Update()
        {
            EnsureBound();
            _motor.LocomotionEnabled = locomotionEnabled;
            _motor.TickUpdate();
        }

        void FixedUpdate()
        {
            EnsureBound();
            _motor.TickFixed();
        }

        /// <summary>注入依赖（Bootstrap / 测试场景调用）。</summary>
        public void Configure(
            LocomotionConfig newConfig,
            IMoveInputProvider input,
            ILocomotionTimeSource time,
            Camera camera = null,
            IMoveGate gate = null,
            ILocomotionStateSink stateSink = null)
        {
            if (newConfig != null)
                config = newConfig;

            _inputProvider = input;
            _timeSource = time;
            if (camera != null)
                moveCamera = camera;
            if (gate != null)
                _gate = gate;
            if (stateSink != null)
                _stateSink = stateSink;

            _bound = false;
            EnsureBound();
        }

        /// <summary>临时关闭重力。</summary>
        public void SetNoGravityT(float time) => _motor.SetNoGravityT(time);

        void EnsureBound()
        {
            if (_bound)
                return;

            if (_controller == null)
                _controller = GetComponent<CharacterController>();

            _cameraProvider ??= new TransformCameraProvider(moveCamera);
            _cameraProvider.SetCamera(moveCamera);

            _inputProvider ??= GetComponent<LocomotionInputReader>() as IMoveInputProvider
                               ?? GetComponentInParent<LocomotionInputReader>();
            _inputProvider ??= new MutableMoveInputProvider();

            _timeSource ??= new UnityLocomotionTimeSource();

            _motor.SetTuning(LocomotionTuningBuilder.FromConfig(config));
            _motor.Bind(
                _controller,
                transform,
                animator,
                _inputProvider,
                _cameraProvider,
                _gate,
                _timeSource,
                _stateSink);
            _motor.LocomotionEnabled = locomotionEnabled;
            _bound = true;
        }
    }
}
