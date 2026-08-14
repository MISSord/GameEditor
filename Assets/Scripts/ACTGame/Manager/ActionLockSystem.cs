using ACTGameEditor;
using UnityEngine;

public class ActionLockSystem : MonoBehaviour
{


    //void UpdateUI()
    //{
    //    if (!_isLocked || _currentTarget == null || lockIconUI == null) return;

    //    // UI 旋转特效
    //    lockIconUI.Rotate(Vector3.forward, -iconRotateSpeed * Time.deltaTime);

    //    // UI 跟随
    //    Vector3 targetPos = _currentTarget.GetCameraTargetPos();
    //    Vector3 screenPos = mainCamera.WorldToScreenPoint(targetPos);

    //    // 处理背后逻辑 (同之前的代码)
    //    if (screenPos.z > 0)
    //    {
    //        lockIconUI.position = screenPos;
    //        lockIconUI.gameObject.SetActive(true);
    //    }
    //    else
    //    {
    //        lockIconUI.gameObject.SetActive(false);
    //    }
    //}

    //// Debug 辅助线
    //void OnDrawGizmosSelected()
    //{
    //    Gizmos.color = Color.yellow;
    //    Gizmos.DrawWireSphere(playerTrans.position, searchRadius);
    //}
}