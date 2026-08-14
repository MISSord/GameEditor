using System.Collections;
using UnityEngine;
using DG.Tweening;
namespace XiaoCao
{

    public static class DoTweenExtend
    {
        public static Tween DOHit(this CharacterController cc, float totalY, Vector3 horVec,float duration)
        {
            Transform tf = cc.transform;

            if (totalY == 0)
            {
                totalY = 0.1f;
            }

            float time = 0; 
            float lastT = 0, deltaT = 0;
            //0 ->1的数值动画
            Tween tween = DOTween.To(x => time = x, 0, 1, duration);
            tween.SetEase(Ease.OutQuart);
            //tween.SetLoops(2,LoopType.Yoyo);

            Vector3 targetMove = horVec;
            targetMove.y += totalY; //目标移动量

            tween.OnUpdate(() =>
            {
                deltaT = time - lastT;
                lastT = time;

                Vector3 delta = targetMove * deltaT;
                cc.Move(delta);
            });
            return tween;
        }

        public static void DOHit2(this CharacterController cc, float totalY, Vector3 hordir, float duration, bool snapping = false)
        {
            float time = 0;
            Tween tween = DOTween.To(x => time = x, 0, 1, duration);
            tween.SetEase(Ease.OutQuad);
            float targetY = DOVirtual.EasedValue(0, totalY, tween.ElapsedPercentage(), Ease.OutQuad);

        }
    }

    public class HitTweenInfo
    {
        public Vector3 targetVec;

        public int line;

        public int time;
    }
}