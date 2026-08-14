using UnityEngine;
using UnityEngine.InputSystem;

namespace ACTGameEditor.Locomotion
{
    /// <summary>
    /// 独立移动场景启动器：自带物理 Simulate + 时间推进，不依赖 EGamePlayInit / Scene.prefab。
    /// 若场景已存在 EGamePlayInit，会自动跳过 Simulate，避免双重物理步进。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LocomotionBootstrap : MonoBehaviour
    {
        [SerializeField]
        bool simulatePhysics = false;

        [SerializeField]
        InputActionAsset actionsAsset;

        [SerializeField]
        LocomotionConfig locomotionConfig;

        [SerializeField]
        CharacterLocomotion playerLocomotion;

        [SerializeField]
        LocomotionInputReader inputReader;

        [SerializeField]
        Camera moveCamera;

        readonly UnityLocomotionTimeSource _timeSource = new();
        bool _ownsPhysics;

        void Awake()
        {
            if (simulatePhysics && HasCombatBootstrap())
            {
                Debug.LogWarning("[LocomotionBootstrap] 检测到 EGamePlayInit，已跳过 Physics.Simulate。", this);
                simulatePhysics = false;
            }

            if (simulatePhysics)
            {
                Physics.autoSimulation = false;
                _ownsPhysics = true;
            }

            if (moveCamera == null)
                moveCamera = Camera.main;

            if (inputReader == null)
                inputReader = FindObjectOfType<LocomotionInputReader>();

            if (inputReader != null && actionsAsset != null)
                inputReader.SetActionsAsset(actionsAsset);

            if (playerLocomotion == null)
                playerLocomotion = FindObjectOfType<CharacterLocomotion>();

            if (playerLocomotion != null)
            {
                playerLocomotion.Configure(
                    locomotionConfig,
                    inputReader,
                    _timeSource,
                    moveCamera,
                    AlwaysAllowMoveGate.Instance);
            }
        }

        void Update()
        {
            _timeSource.Tick();
        }

        void FixedUpdate()
        {
            if (!_ownsPhysics || !simulatePhysics)
                return;

            if (Time.fixedDeltaTime > 0f)
                Physics.Simulate(Time.fixedDeltaTime);
        }

        void OnDestroy()
        {
            if (_ownsPhysics)
                Physics.autoSimulation = true;
        }

        static bool HasCombatBootstrap()
        {
            var behaviours = FindObjectsOfType<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] != null && behaviours[i].GetType().Name == "EGamePlayInit")
                    return true;
            }

            return false;
        }
    }
}
