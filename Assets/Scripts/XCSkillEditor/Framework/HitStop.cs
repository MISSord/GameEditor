using UnityEngine;
using XiaoCao;
using DG.Tweening;
using ACTGameEditor;

public class HitStop : MonoSingleton<HitStop>
{
    public float shakeTime = 0.025f;
    public float shakeLength = 0.25f;
    public int shakeCount = 8;

    bool isEnbleHitShop = true;

    private void OnEnable()
    {
        isEnbleHitShop = ResFinder.SoUsingFinder.DebugSo.isHitStop;
    }

    /// <summary> 触发命中顿帧，交由 TimeScaleEffectManager 管理。暂停时计时冻结。 </summary>
    public void DoHitStop(float time = 0.001f)
    {
        if (!isEnbleHitShop || time < 0f) return;
        TimeScaleEffectManager.AddHitStop(time);
        CameraPostFxController.TryPlayHitStopImpact(time);
    }

    /// <summary> 触发命中顿帧（可选相机震动）。 </summary>
    public void DoHitStop(float time, bool isShake)
    {
        if (!isEnbleHitShop || time <= 0f) return;
        TimeScaleEffectManager.AddHitStop(time);
        CameraPostFxController.TryPlayHitStopImpact(time);
        //if (isShake && CurrentPlayerData.shakeLengthRate > 0)
        //    CameraController.instance.CamShake(shakeTime, shakeLength, shakeCount);
    }

    public void Shake(float time = 0.2f)
    {
        Camera.main.DOShakePosition(time, 0.2f, 10);
        //if (CurrentPlayerData.shakeLengthRate > 0)
        //CameraController.instance.CamShake(shakeTime, shakeLength, shakeCount);
    }

    /// <summary> 取消当前 HitStop 效果。 </summary>
    public void Cancel()
    {
        TimeScaleEffectManager.RemoveByType(TimeScaleEffectType.HitStop);
    }

}
