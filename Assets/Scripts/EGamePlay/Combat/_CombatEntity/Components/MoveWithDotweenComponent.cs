using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using ET;
using DG.Tweening;
using System;

namespace EGamePlay.Combat
{
    public enum MoveType
    {
        TargetMove,
        PathMove,
    }

    public enum SpeedType
    {
        Speed,
        Duration,
    }

    public class MoveWithDotweenComponent : Component
    {
        public override bool IsNeedUpdate { get; protected set; } = true;
        public SpeedType SpeedType { get; set; }
        public float Speed { get; set; }
        public float Duration { get; set; }
        public float ElapsedTime { get; set; }
        public IPosition OwnerPositionEntity { get; set; }
        public IPosition TargetPositionEntity { get; set; }
        public Entity TargetEntity { get; set; }
        public Vector3 Destination { get; set; }
        public Tweener MoveTweener { get; set; }
        private System.Action MoveFinishAction { get; set; }


        public override void Awake()
        {
            OwnerPositionEntity = (IPosition)Entity;
            ElapsedTime = 0;
        }

        public override void OnDestroy()
        {
            MoveTweener?.Kill();
            MoveTweener = null;
        }

        public override void Update(float deltaTime)
        {
            if (TargetPositionEntity != null)
            {
                if (TargetEntity.IsDisposed)
                {
                    TargetEntity = null;
                    TargetPositionEntity = null;
                    Entity.Destroy(Entity);
                    return;
                }
                //这里多次赋值，修正终点
                if (SpeedType == SpeedType.Speed) DoTimeMoveSpeed(Speed);
                if (SpeedType == SpeedType.Duration) DoTimeMove(MathF.Max(0, Duration - ElapsedTime));
                ElapsedTime += deltaTime;
            }
        }

        /// <summary>
        /// 无目标的只有方向的飞行
        /// </summary>
        /// <param name="destination"></param>
        /// <param name="duration"></param>
        /// <returns></returns>
        public MoveWithDotweenComponent DoMoveTo(Vector3 destination, float duration)
        {
            Destination = destination;
            MoveTweener = DOTween.To(()=> { return OwnerPositionEntity.Position; }, (x) => OwnerPositionEntity.Position = x, Destination, duration).SetEase(Ease.Linear).OnComplete(OnMoveFinish);
            return this;
        }

        public void DoMoveToWithSpeed(IPosition targetPositionEntity, float Speed = 1f)
        {
            this.Speed = Speed;
            SpeedType = SpeedType.Speed;
            TargetPositionEntity = targetPositionEntity;
            TargetEntity = targetPositionEntity as Entity;
            DoTimeMoveSpeed(Speed);
        }

        private void DoTimeMoveSpeed(float Speed)
        {
            MoveTweener?.Kill();
            var dist = Vector3.Distance(OwnerPositionEntity.Position, TargetPositionEntity.Position);
            var duration = dist / Speed;
            MoveTweener = DOTween.To(() => { return OwnerPositionEntity.Position; }, (x) => OwnerPositionEntity.Position = x, TargetPositionEntity.Position, duration);
        }

        public void DoMoveToWithTime(IPosition targetPositionEntity, float time = 1f)
        {
            Duration = time;
            SpeedType = SpeedType.Duration;
            TargetPositionEntity = targetPositionEntity;
            TargetEntity = targetPositionEntity as Entity;
            DoTimeMove(time);
        }

        private void DoTimeMove(float time)
        {
            MoveTweener?.Kill();
            if (time == 0) return;
            MoveTweener = DOTween.To(() => { return OwnerPositionEntity.Position; }, (x) => OwnerPositionEntity.Position = x, TargetPositionEntity.Position, time);
            MoveTweener.SetEase(Ease.Linear);
        }

        public void OnMoveFinish(System.Action action)
        {
            MoveFinishAction = action;
        }

        private void OnMoveFinish()
        {
            MoveFinishAction?.Invoke();
            MoveFinishAction = null;
        }
    }
}