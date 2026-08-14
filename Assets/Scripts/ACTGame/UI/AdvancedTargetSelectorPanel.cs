using UnityEngine;
using XiaoCao;

namespace ACTGameEditor
{
    public class AdvancedTargetSelectorPanel : UIBase
    {
        [HideInInspector]
        public Transform targetObj;
        [HideInInspector]
        public Transform playerObj;

        [Header("锁定系统")]
        [Tooltip("勾选则从 LockSystem 自动获取锁定目标；取消勾选则需外部手动设置 targetObj/playerObj")]
        public bool useLockSystem = true;

        [Header("UI 元素设置")]
        [Tooltip("目标在【屏幕内】时显示的 UI (例如方框)")]
        public RectTransform onScreenUI;
        [Tooltip("目标在【屏幕外】时显示的 UI (例如箭头)")]
        public RectTransform offScreenArrowUI;

        [Header("屏幕内缩放设置")]
        public float referenceDistance = 10f;
        public float minScale = 0.6f;
        public float maxScale = 1.5f;
        public float scaleFactor = 1.0f;

        [Header("屏幕外设置")]
        [Tooltip("屏幕边缘内缩边距 (防止 UI 图片贴边显示不全)")]
        public float edgePadding = 50f;
        [Tooltip("箭头图片默认是否指向向上? 如果你的箭头图片默认指向右，请取消勾选")]
        public bool arrowSpritePointsUp = true;

        [Header("位置偏移")]
        public Vector3 targetOffset = Vector3.zero;

        private Vector3 _screenCenter;
        private Rect _screenBounds;

        private void OnEnable()
        {
            if (!useLockSystem) return;
            if (LockSystem.Instance != null)
            {
                LockSystem.Instance.OnLockChanged += RefreshBindingsFromLockSystem;
                LockSystem.Instance.OnTargetChanged += OnLockTargetChanged;
            }
        }

        private void OnDisable()
        {
            if (LockSystem.Instance != null)
            {
                LockSystem.Instance.OnLockChanged -= RefreshBindingsFromLockSystem;
                LockSystem.Instance.OnTargetChanged -= OnLockTargetChanged;
            }
        }

        private void Start()
        {
            if (onScreenUI) onScreenUI.gameObject.SetActive(false);
            if (offScreenArrowUI) offScreenArrowUI.gameObject.SetActive(false);
            if (useLockSystem) RefreshBindingsFromLockSystem();
        }

        void OnLockTargetChanged(ICameraTarget _) => RefreshBindingsFromLockSystem();

        /// <summary>从 LockSystem 更新 targetObj/playerObj，并控制显隐</summary>
        void RefreshBindingsFromLockSystem()
        {
            if (!useLockSystem) return;
            var ls = LockSystem.Instance;
            var cam = CameraManager.Instance;
            if (ls == null || !ls.IsLocked || ls.LockedTarget == null || cam == null || cam.CurrentTarget == null)
            {
                gameObject.SetActive(false);
                return;
            }
            targetObj = ls.LockedTarget.GetCameraTarget();
            playerObj = cam.CurrentTarget.GetCameraTarget();
            gameObject.SetActive(targetObj != null && playerObj != null);
        }

        void LateUpdate()
        {
            if (useLockSystem)
            {
                RefreshBindingsFromLockSystem();
                if (!gameObject.activeSelf) return;
            }
            if (!ValidateReferences()) return;

            // 更新屏幕参数 (以防分辨率在游戏中变化)
            _screenCenter = new Vector3(Screen.width / 2f, Screen.height / 2f, 0f);
            // 定义屏幕有效区域 (考虑边距)
            _screenBounds = new Rect(edgePadding, edgePadding, Screen.width - edgePadding * 2, Screen.height - edgePadding * 2);

            // 1. 计算基础数据
            Vector3 worldPos = targetObj.position + targetOffset;
            Vector3 screenPos = MainCam.WorldToScreenPoint(worldPos);
            float distance = Vector3.Distance(playerObj.position, targetObj.position);

            // 2. 判断目标是否在屏幕视野内
            // 条件：Z > 0 (在摄像机前方) 且 屏幕坐标 XY 在屏幕矩形范围内
            bool isOnScreen = screenPos.z > 0 && _screenBounds.Contains(screenPos);

            if (isOnScreen)
            {
                HandleOnScreenState(screenPos, distance);
            }
            else
            {
                HandleOffScreenState(screenPos);
            }
        }

        // --- 状态处理逻辑 ---

        // 处理目标在屏幕内的情况
        void HandleOnScreenState(Vector3 currentScreenPos, float distance)
        {
            // 切换显示
            if (!onScreenUI.gameObject.activeSelf) onScreenUI.gameObject.SetActive(true);
            if (offScreenArrowUI.gameObject.activeSelf) offScreenArrowUI.gameObject.SetActive(false);

            // 设置位置
            onScreenUI.position = currentScreenPos;
            // 重置旋转 (防止之前可能被意外修改)
            onScreenUI.rotation = Quaternion.identity;

            // 处理缩放 (仅针对屏幕内 UI)
            float scale = Mathf.Clamp((referenceDistance / Mathf.Max(distance, 0.01f)) * scaleFactor, minScale, maxScale);
            onScreenUI.localScale = Vector3.one * scale;
        }

        // 处理目标在屏幕外的情况
        void HandleOffScreenState(Vector3 currentScreenPos)
        {
            // 切换显示
            if (onScreenUI.gameObject.activeSelf) onScreenUI.gameObject.SetActive(false);
            if (!offScreenArrowUI.gameObject.activeSelf) offScreenArrowUI.gameObject.SetActive(true);

            // **关键难点处理：处理摄像机后方的情况**
            // 如果物体在摄像机背后 (z < 0)，WorldToScreenPoint 返回的坐标是反向的。
            // 我们需要将其相对于屏幕中心进行镜像翻转，这样指示器才会指向正确的方向。
            if (currentScreenPos.z < 0)
            {
                // 将坐标变换到以屏幕中心为原点
                currentScreenPos -= _screenCenter;
                // 镜像翻转
                currentScreenPos *= -1f;
                // 变换回屏幕坐标系
                currentScreenPos += _screenCenter;
            }

            // **边缘锁定计算**
            // 使用 Mathf.Clamp 将坐标限制在带 Padding 的屏幕范围内
            Vector3 clampedPos = currentScreenPos;
            clampedPos.x = Mathf.Clamp(clampedPos.x, _screenBounds.xMin, _screenBounds.xMax);
            clampedPos.y = Mathf.Clamp(clampedPos.y, _screenBounds.yMin, _screenBounds.yMax);

            // 应用位置
            offScreenArrowUI.position = clampedPos;

            // **箭头旋转计算**
            // 计算从屏幕中心指向边缘位置的方向向量
            Vector3 direction = clampedPos - _screenCenter;
            // 使用 Atan2 计算角度 (弧度转角度)
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

            // 修正角度偏移：Atan2 的 0 度是向右，如果你的箭头图片默认向上，需要减去 90 度
            float finalAngle = arrowSpritePointsUp ? angle - 90f : angle;

            offScreenArrowUI.rotation = Quaternion.Euler(0, 0, finalAngle);
        }

        bool ValidateReferences()
        {
            return targetObj != null && playerObj != null && MainCam != null && onScreenUI != null && offScreenArrowUI != null;
        }
    }
}
