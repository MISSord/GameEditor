using UnityEngine;

namespace ACTGameEditor
{
    /// <summary>
    /// 深度视界控制：热键切换白→深灰的深度着色效果。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DepthVisionController : MonoBehaviour
    {
        [SerializeField]
        Color nearColor = Color.white;

        [SerializeField]
        Color farColor = new Color(0.18f, 0.18f, 0.18f, 1f);

        [SerializeField]
        float depthNear = 1f;

        [SerializeField]
        float depthFar = 35f;

        [Range(0f, 1f)]
        [SerializeField]
        float intensity = 1f;

        [Tooltip("开启后 ACT/Character（含 ScanTarget）会写入深度，从而出现在深度视界中")]
        [SerializeField]
        bool includeCharacterDepth = true;

        [SerializeField]
        bool respectGraphicsFxGate = true;

        /// <summary>当前是否开启。</summary>
        public bool IsActive => DepthVisionState.IsActive;

        void Awake()
        {
            DepthVisionState.IncludeCharacterDepth = includeCharacterDepth;
        }

        void OnDisable()
        {
            if (DepthVisionState.IsActive)
                SetActive(false);
        }

        void Update()
        {
            PushSettings();
            // 调试切换键已统一移到 SkillEditorScene（数字键 6）
        }

        /// <summary>切换深度视界。</summary>
        public void Toggle() => SetActive(!DepthVisionState.IsActive);

        /// <summary>设置深度视界开关。</summary>
        public void SetActive(bool active)
        {
            if (respectGraphicsFxGate && active && !GraphicsFxService.Query(GraphicsFxId.DepthVision))
                return;

            DepthVisionState.IsActive = active;
            PushSettings();
        }

        /// <summary>设置角色/Character 材质是否写入深度。</summary>
        public void SetIncludeCharacterDepth(bool include)
        {
            includeCharacterDepth = include;
            DepthVisionState.IncludeCharacterDepth = include;
        }

        void PushSettings()
        {
            DepthVisionState.NearColor = nearColor;
            DepthVisionState.FarColor = farColor;
            DepthVisionState.DepthNear = depthNear;
            DepthVisionState.DepthFar = depthFar;
            DepthVisionState.Intensity = intensity;
            DepthVisionState.IncludeCharacterDepth = includeCharacterDepth;
        }
    }
}
