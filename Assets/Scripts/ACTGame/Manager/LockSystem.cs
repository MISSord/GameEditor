using System;
using System.Collections.Generic;
using EGamePlay.Combat;
using UnityEngine;

namespace ACTGameEditor
{
    /// <summary>
    /// 崩坏3风格锁定系统：统一管理目标锁定、切换、镜头与技能目标。
    /// 结合 TargetSelectManager 与 LockOnState 逻辑。
    /// </summary>
    public class LockSystem : MonoBehaviour
    {
        public static LockSystem Instance { get; private set; }

        [Header("搜索参数")]
        [Tooltip("索敌最大半径")]
        public float searchRadius = 20f;
        [Tooltip("屏幕中心视野角度限制，超过不锁定")]
        public float maxAngle = 90f;
        [Tooltip("敌人所在 Layer")]
        public LayerMask enemyLayer;

        [Header("目标评分")]
        [Tooltip("角度权重越高，越倾向屏幕中心")]
        [Range(0f, 1f)]
        public float angleWeight = 0.7f;
        [Tooltip("距离权重越高，越倾向最近")]
        [Range(0f, 1f)]
        public float distanceWeight = 0.3f;

        /// <summary>是否已锁定</summary>
        public bool IsLocked { get; private set; }

        /// <summary>当前锁定目标（ICameraTarget）</summary>
        public ICameraTarget LockedTarget { get; private set; }

        /// <summary>当前锁定目标的 CombatEntity，供技能系统使用</summary>
        public CombatEntity LockedCombatEntity { get; private set; }

        public event Action OnLockChanged;
        public event Action<ICameraTarget> OnTargetChanged;

        private static readonly Collider[] s_overlapBuffer = new Collider[64];
        private readonly List<ICameraTarget> _candidates = new List<ICameraTarget>(32);

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
            Unlock();
        }

        /// <summary>ICameraTarget 转 CombatEntity</summary>
        public static CombatEntity GetCombatEntity(ICameraTarget target)
        {
            if (target == null) return null;
            var go = target.GetPlayerTransform()?.gameObject;
            if (go == null) return null;
            return CombatContext.Instance != null && CombatContext.Instance.Object2Entities.TryGetValue(go, out var entity)
                ? entity
                : null;
        }

        /// <summary>切换锁定：无锁→锁最近；已锁→解锁</summary>
        public void ToggleLock()
        {
            if (IsLocked)
            {
                Unlock();
                return;
            }

            var best = FindBestTarget();
            if (best != null)
            {
                LockOn(best);
            }
        }

        /// <summary>锁定指定目标</summary>
        public void LockOn(ICameraTarget target)
        {
            if (target == null) return;

            bool wasLocked = IsLocked;
            var oldTarget = LockedTarget;

            LockedTarget = target;
            LockedCombatEntity = GetCombatEntity(target);
            IsLocked = true;

            if (!wasLocked) OnLockChanged?.Invoke();
            if (oldTarget != target) OnTargetChanged?.Invoke(target);
        }

        /// <summary>解锁</summary>
        public void Unlock()
        {
            if (!IsLocked) return;

            IsLocked = false;
            var oldTarget = LockedTarget;
            LockedTarget = null;
            LockedCombatEntity = null;

            OnLockChanged?.Invoke();
            OnTargetChanged?.Invoke(null);
        }

        /// <summary>切换到下一个目标（崩坏3 右侧切换）</summary>
        public void SwitchTargetNext()
        {
            if (!IsLocked || LockedTarget == null) return;

            var list = FindAllCandidatesSorted();
            if (list.Count <= 1) return;

            int idx = list.IndexOf(LockedTarget);
            idx = (idx + 1) % list.Count;
            LockOn(list[idx]);
        }

        /// <summary>切换到上一个目标（崩坏3 左侧切换）</summary>
        public void SwitchTargetPrev()
        {
            if (!IsLocked || LockedTarget == null) return;

            var list = FindAllCandidatesSorted();
            if (list.Count <= 1) return;

            int idx = list.IndexOf(LockedTarget);
            idx = idx <= 0 ? list.Count - 1 : idx - 1;
            LockOn(list[idx]);
        }

        /// <summary>更新目标有效性，死亡/消失时自动解锁</summary>
        public void ValidateLockedTarget()
        {
            if (!IsLocked || LockedTarget == null) return;

            var t = LockedTarget.GetPlayerTransform();
            if (t == null || !t.gameObject.activeInHierarchy)
            {
                Unlock();
                return;
            }

            if (LockedCombatEntity != null && LockedCombatEntity.IsDisposed)
            {
                Unlock();
            }
        }

        /// <summary>寻找最佳目标（角度+距离加权）</summary>
        public ICameraTarget FindBestTarget()
        {
            var cam = CameraManager.Instance;
            if (cam == null || cam.CurrentTarget == null) return null;

            Vector3 playerPos = cam.CurrentTarget.GetPlayerPos();
            int count = Physics.OverlapSphereNonAlloc(playerPos, searchRadius, s_overlapBuffer, enemyLayer);

            ICameraTarget bestCandidate = null;
            float minScore = float.MaxValue;
            Vector3 camForward = cam.MainCamera.forward;

            for (int i = 0; i < count; i++)
            {
                var col = s_overlapBuffer[i];
                if (col == null) continue;
                if (!col.TryGetComponent<ICameraTarget>(out var candidate)) continue;

                Vector3 candidatePos = candidate.GetPlayerPos();
                Vector3 dirToEnemy = (candidatePos - playerPos).normalized;

                float dist = Vector3.Distance(playerPos, candidatePos);
                float angle = Vector3.Angle(camForward, dirToEnemy);
                if (angle > maxAngle) continue;

                float normalDist = dist / searchRadius;
                float normalAngle = angle / maxAngle;
                float score = normalAngle * angleWeight + normalDist * distanceWeight;

                if (score < minScore)
                {
                    minScore = score;
                    bestCandidate = candidate;
                }
            }

            return bestCandidate;
        }

        /// <summary>获取所有候选目标（按角度排序，用于左右切换）</summary>
        public List<ICameraTarget> FindAllCandidatesSorted()
        {
            _candidates.Clear();
            var cam = CameraManager.Instance;
            if (cam == null || cam.CurrentTarget == null) return _candidates;

            Vector3 playerPos = cam.CurrentTarget.GetPlayerPos();
            Vector3 camForward = cam.MainCamera.forward;
            int count = Physics.OverlapSphereNonAlloc(playerPos, searchRadius, s_overlapBuffer, enemyLayer);

            for (int i = 0; i < count; i++)
            {
                var col = s_overlapBuffer[i];
                if (col == null) continue;
                if (!col.TryGetComponent<ICameraTarget>(out var candidate)) continue;

                Vector3 dirToEnemy = (candidate.GetPlayerPos() - playerPos).normalized;
                float angle = Vector3.Angle(camForward, dirToEnemy);
                if (angle <= maxAngle)
                {
                    _candidates.Add(candidate);
                }
            }

            // 按角度排序：屏幕中心优先
            _candidates.Sort((a, b) =>
            {
                float angA = Vector3.Angle(camForward, (a.GetPlayerPos() - playerPos).normalized);
                float angB = Vector3.Angle(camForward, (b.GetPlayerPos() - playerPos).normalized);
                return angA.CompareTo(angB);
            });

            return _candidates;
        }
    }
}
