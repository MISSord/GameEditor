using UnityEngine;

namespace ACTGameEditor
{
    /// <summary>
    /// 扫描可揭示目标标记。挂上后可被 <see cref="ScanPulseController"/> 检测到。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ScanTarget : MonoBehaviour
    {
        [Tooltip("可选：指定揭示视觉；为空则自动查找或添加")]
        [SerializeField]
        ScanRevealVisual revealVisual;

        [Tooltip("边缘高亮材质（ACT/ScanEdgeHighlight）")]
        [SerializeField]
        Material edgeHighlightMaterial;

        /// <summary>是否正处于扫描揭示状态。</summary>
        public bool IsRevealed { get; private set; }

        void Awake()
        {
            EnsureVisual();
        }

        /// <summary>
        /// 开启 / 关闭扫描边缘高亮。
        /// </summary>
        public void SetRevealed(bool revealed)
        {
            if (revealed && !GraphicsFxService.Query(GraphicsFxId.ScanEdgeHighlight))
                revealed = false;

            // 对象级门闩
            var objectFx = GetComponent<ObjectFxController>()
                           ?? GetComponentInParent<ObjectFxController>();
            if (revealed && objectFx != null && !objectFx.IsAllowed(ObjectFxFlags.ScanEdgeHighlight))
                revealed = false;

            IsRevealed = revealed;
            EnsureVisual();
            if (revealVisual != null)
                revealVisual.SetRevealed(revealed);
        }

        void EnsureVisual()
        {
            if (revealVisual == null)
            {
                revealVisual = GetComponent<ScanRevealVisual>()
                               ?? GetComponentInChildren<ScanRevealVisual>()
                               ?? gameObject.AddComponent<ScanRevealVisual>();
            }

            if (edgeHighlightMaterial != null)
                revealVisual.EnsureMaterial(edgeHighlightMaterial);
        }

        void OnDisable()
        {
            if (IsRevealed)
                SetRevealed(false);
        }
    }
}
