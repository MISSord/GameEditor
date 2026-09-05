using ACTGameEditor.Combat;
using DG.Tweening;
using EGamePlay.Combat;
using EGamePlay.Unity;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

namespace ACTGameEditor
{
    public static class XCSetting
    {
        public static readonly int FrameRate = 30;
        public static readonly float FramePerSec = 1f / FrameRate;
    }

    public class XCEvent: IResettable
    {
        public XCEventData EventData;

        public XCRange Range;
        protected uint NetId { get => OwnCombat.NetId; }
        protected CombatEntity OwnCombat { get; private set; }

        private Transform _owenrTransform;
        protected Transform OwnerTF
        {
            get
            {
                if (_owenrTransform == null)
                {
                    _owenrTransform = SelfRunner.ObjEvent != null ? SelfRunner.ObjEvent.LoadObj.transform : OwnCombat.RootTransform;
                }
                return _owenrTransform;
            }
        }

        protected XCNewEventsRunner SelfRunner;

        private bool _hasTriggered = false;
        public bool HasTriggered { get { return _hasTriggered; } }

        protected bool _hasFinished = false;
        public bool HasFinished { get { return _hasFinished; } }

        public void SetFinished()
        {
            _hasFinished = true;
            _hasTriggered = true;
        }

        public int Start
        {
            get { return Range.Start; }
            //set { Range.Start = value; }
        }

        public int End
        {
            get { return Range.End; }
            //set { Range.End = value; }
        }

        public float StartTime
        {
            get { return Range.Start * XCSetting.FramePerSec; }
        }

        public float EndTime
        {
            get { return Range.End * XCSetting.FramePerSec; }
        }

        public float LengthTime
        {
            get { return Range.Length * XCSetting.FramePerSec; }
        }

        //复写部分
        public virtual void Init(CombatEntity owner, XCNewEventsRunner runner)
        {
            this.OwnCombat = owner;
            this.SelfRunner = runner;
            _hasFinished = false;
            _hasTriggered = false;
        }

        public virtual void OnTrigger(float timeSinceTrigger)
        {
            _hasTriggered = true;
            _hasFinished = false;
        }

        //数据重置回收写在这里，完成后直接清空
        public virtual void OnFinish() { }

        public virtual void OnUpdateEvent(int frame, float timeSinceTrigger) { }

        public void OnReset()
        {
            _hasFinished = false;
            _hasTriggered = false;
        }

        public void UpdateEvent(int frame, float timeSinceTrigger)
        {
            OnUpdateEvent(frame, timeSinceTrigger);
        }

        public virtual void Reset()
        {
            OwnCombat = null;
            SelfRunner = null;
            Range = null;
            _owenrTransform = null;
        }
    }

    public class XCAnimEvent : XCEvent
    {
        private XCAnimEventData eventData => (XCAnimEventData)EventData;
        private int _cachedClipHash;
        private float _cachedClipLength;
        private int _animToken;
        private CombatAnimDirector _director;

        public override void Init(CombatEntity owner, XCNewEventsRunner runner)
        {
            base.Init(owner, runner);
            _cachedClipHash = Animator.StringToHash(eventData.AnimName);
            _cachedClipLength = 0f;
            _animToken = 0;
            _director = null;
        }

        /// <summary>解析并缓存 clip 时长（优先 Director 缓存，避免重复扫表）。</summary>
        private bool TryResolveClipLength(Animator animator, CombatAnimDirector director)
        {
            if (_cachedClipLength > 0f)
                return true;
            if (director != null && director.TryGetClipLength(_cachedClipHash, out float cached) && cached > 0f)
            {
                _cachedClipLength = cached;
                return true;
            }

            if (animator?.runtimeAnimatorController == null)
                return false;

            var clips = animator.runtimeAnimatorController.animationClips;
            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i].name != eventData.AnimName)
                    continue;
                _cachedClipLength = clips[i].length;
                director?.CacheClipLength(_cachedClipHash, _cachedClipLength);
                return _cachedClipLength > 0f;
            }

            return false;
        }

        public override void OnTrigger(float timeSinceTrigger)
        {
            AnimComponent animComp = OwnCombat?.GetComponent<AnimComponent>();
            _director = animComp?.Director;
            Animator animator = animComp?.animator;
            if (_director == null || animator == null)
            {
                Debug.LogError("no CombatAnimDirector/animator " + eventData.AnimName);
                return;
            }

            if (!TryResolveClipLength(animator, _director))
            {
                Debug.LogError("no clip " + eventData.AnimName);
                return;
            }

            // BlenderLength / StartOffset 按秒（FixedTime），与旧「秒/clip.length」资源语义对齐
            float blendSeconds = eventData.BlenderLength > 0f ? eventData.BlenderLength : 0f;
            float offsetSeconds = eventData.StartOffset > 0f ? eventData.StartOffset : 0f;
            float skillSpeed = SelfRunner != null ? SelfRunner.Speed : 1f;

            _animToken = _director.PlaySkill(
                _cachedClipHash,
                blendSeconds,
                offsetSeconds,
                skillSpeed,
                eventData.UseRootMotion,
                eventData.SuppressGravity);
            AnimExitPolicy policy = eventData.ExitPolicy;
            SelfRunner?.GetParent<ActSkillRunner>()?.NotifyAnimPlayed(_animToken, policy);

            base.OnTrigger(timeSinceTrigger);
            _director.Scrub(timeSinceTrigger);
        }

        public override void OnFinish()
        {
            // 不在此回 Idle：交由 ActSkillRunner Release；速度由 Director 统一订阅时间缩放。
            _animToken = 0;
            _director = null;
        }
    }

    public class XCObjEvent : XCEvent
    {
        private XCObjEventData _eventData;
        private ParticleSystem _ps;
        ParticleSystem[] _particles;
        public GameObject LoadObj { get; private set; }

        public override void Init(CombatEntity owner, XCNewEventsRunner runner)
        {
            base.Init(owner, runner);
            _eventData = (XCObjEventData)EventData;
            LoadObj = RunTimePoolManager.Instance.LoadResPoolObj(_eventData.BundlePath, _eventData.AssetPath);
            if (LoadObj != null)
                LoadObj.SetActive(false);
            SelfRunner.ObjEvent = this;  //赋值给运行器
        }

        public override void OnTrigger(float timeSinceTrigger)
        {
            if (LoadObj == null)
            {
                base.OnTrigger(timeSinceTrigger);
                return;
            }

            SetFirstPos();
            if (_eventData.IsEffect)
            {
                ResetPooledVfx(LoadObj);
                _ps = LoadObj.GetComponentInChildren<ParticleSystem>(true);
                _particles = LoadObj.GetComponentsInChildren<ParticleSystem>(true);
                _ps?.Play(true);
                ApplyParticleClock();
            }
            LoadObj.SetActive(true);
            base.OnTrigger(timeSinceTrigger);
        }

        //设置起始位置
        private void SetFirstPos()
        {
            if (_eventData.TransfromType == TransfromType.FollowPlayer || _eventData.TransfromType == TransfromType.PlyerUnFollow)
            {
                //实时获取 Acker的坐标系 ,同步上会出现偏差,似乎不可避免,不想加同步数据就只能模拟了
                LoadObj.transform.eulerAngles = OwnCombat.RootTransform.eulerAngles + _eventData.StartRotation;
                LoadObj.transform.position = OwnCombat.RootTransform.TransformPoint(_eventData.StartPos);
            }

            if (_eventData.TransfromType == TransfromType.WorldPos)
            {
                LoadObj.transform.eulerAngles = SelfRunner.CastEuler + _eventData.StartRotation;
                //Quaternion(方向) * localPos = 世界向量
                LoadObj.transform.position = SelfRunner.CastPos + Quaternion.Euler(SelfRunner.CastEuler) * _eventData.StartPos;
            }

            LoadObj.transform.localScale = _eventData.StartScale;

            if (_eventData.TransfromType == TransfromType.FollowPlayer)
                LoadObj.transform.SetParent(OwnCombat.RootTransform, true);
            else
                RunTimePoolManager.Instance.AttachToSceneLayer(LoadObj.transform);
        }

        public override void OnUpdateEvent(int frame, float timeSinceTrigger)
        {
            ApplyParticleClock();
        }

        void ApplyParticleClock()
        {
            if (_particles == null || _particles.Length == 0)
                return;
            float speed = CombatTimeClock.GetSimulationSpeed(OwnCombat);
            for (int i = 0; i < _particles.Length; i++)
                CombatTimeClock.ApplySimulationSpeed(_particles[i], speed);
        }

        public override void OnFinish()
        {
            if (LoadObj != null)
            {
                ResetPooledVfx(LoadObj);
                _ps = null;
                _particles = null;
                LoadObj.SetActive(false);
                RunTimePoolManager.Instance.ReCycle(
                    RunTimePoolManager.GetResPath(_eventData.BundlePath, _eventData.AssetPath),
                    LoadObj);
                LoadObj = null;
            }
        }

        static void ResetPooledVfx(GameObject vfx)
        {
            if (vfx == null)
                return;

            ParticleSystem[] particles = vfx.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < particles.Length; i++)
                particles[i]?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            TrailRenderer[] trails = vfx.GetComponentsInChildren<TrailRenderer>(true);
            for (int i = 0; i < trails.Length; i++)
                trails[i]?.Clear();
        }
    }

    public class XCMoveEvent : XCLineEvent
    {
        private CharacterController _cc;
        private MotionDirector _motion;
        private Matrix4x4 _m4;

        public override void Init(CombatEntity owner, XCNewEventsRunner runner)
        {
            base.Init(owner, runner);
            _m4 = OwnerTF.localToWorldMatrix;
            _motion = owner?.GetComponent<AnimComponent>()?.Motion;
        }

        public override void OnTrigger(float timeSinceTrigger)
        {
            base.OnTrigger(timeSinceTrigger);
            _cc = OwnerTF.GetComponent<CharacterController>();
            if (_motion == null)
                _motion = OwnCombat?.GetComponent<AnimComponent>()?.Motion;

            var moveData = (XCMoveEventData)EventData;
            if (moveData.StartVec != Vector3.zero)
            {
                ApplyDetalVec(moveData.StartDetal);
            }
        }

        public override void ApplyDetalVec(Vector3 detalMove)
        {
            Vector3 world = _m4.MultiplyVector(detalMove);
            if (_motion != null)
            {
                // 曲线可带 Y；是否吃重力由 MotionDirector.GravityEnabled 决定
                _motion.TryApply(MotionSource.SkillCurve, world, flattenY: false);
            }
            else if (_cc != null)
            {
                _cc.Move(world);
            }
            else
            {
                OwnerTF.Translate(world, Space.World);
            }

            var moveData = (XCMoveEventData)EventData;
            if (moveData.LookForward)
            {
                OwnerTF.forward = world;
            }
        }

        public override Vector3 GetVec3Value(float t)
        {
            var moveData = (XCMoveEventData)EventData;
            float easingT = DOVirtual.EasedValue(0, 1, t, moveData.EaseType);
            if (moveData.IsBezier)
            {
                return MathTool.GetBezierPoint2(moveData.StartVec, moveData.EndVec, moveData.HandlePoint, easingT);
            }
            else
            {
                return MathTool.LinearVec3(moveData.StartVec, moveData.EndVec, easingT);
            }
        }

        public override void OnFinish()
        {
            _cc = null;
            _motion = null;
        }
    }

    public class XCScaleEvent : XCLineEvent
    {
        public override void ApplyDetalVec(Vector3 detalMove)
        {
            OwnerTF.localScale += detalMove;
        }
    }

    public class XCRotateEvent : XCLineEvent
    {
        private Vector3 _angle;
        public override void OnTrigger(float timeSinceTrigger)
        {
            base.OnTrigger(timeSinceTrigger);
            _angle = SelfRunner.CastEuler + eventData.StartVec;
            OwnerTF.eulerAngles = _angle;
        }

        public override void ApplyDetalVec(Vector3 detalMove)
        {
            _angle += detalMove;
            OwnerTF.eulerAngles = _angle;
        }
    }

    //Vec线性变化事件基类
    public class XCLineEvent : XCEvent
    {
        public XCLineEventData eventData;
        private float _lastTime = 0;

        public override void OnTrigger(float timeSinceTrigger)
        {
            base.OnTrigger(timeSinceTrigger);
            _lastTime = 0;
            eventData = (XCLineEventData)EventData;
        }

        public override void OnUpdateEvent(int frame, float timeSinceTrigger)
        {
            base.OnUpdateEvent(frame, timeSinceTrigger);
            float t = timeSinceTrigger / LengthTime;

            var move = GetVec3Value(t) - GetVec3Value(_lastTime);
            _lastTime = t;
            ApplyDetalVec(move);
        }

        /// <summary>
        /// 主要修改
        /// </summary>
        /// <param name="detalMove"></param>
        public virtual void ApplyDetalVec(Vector3 detalMove) { }

        public virtual Vector3 GetVec3Value(float t)
        {
            float easingT = DOVirtual.EasedValue(0, 1, t, eventData.EaseType);
            return MathTool.LinearVec3(eventData.StartVec, eventData.EndVec, easingT);
        }

        //public void ChageDir(float angle)
        //{
        //    //angle旋转角度 axis围绕旋转轴 position自身坐标 自身坐标 center旋转中心
        //    //Quaternion.AngleAxis(angle, axis) * (position - center) + center;

        //    startVec = MathTool.ChageDir(startVec, angle);
        //    endVec = MathTool.ChageDir(endVec, angle);
        //}

        //public void ChangeOffset(Vector3 offset)
        //{
        //    startVec += offset;
        //    endVec += offset;
        //}

        //public Vector3 RotateRound(Vector3 position, Vector3 center, Vector3 axis, float angle)
        //{
        //    return Quaternion.AngleAxis(angle, axis) * (position - center) + center;
        //}
    }

    //技能触发器
    public class XCTriggerEvent : XCEvent
    {
        public XCTriggerEventData TriggerEventData;
        private Collider _collider;
        private GameObject _colliderObj;
        private static int _nextHitInstanceId = 1;
        private static readonly Collider[] OverlapBuffer = new Collider[16];
        private static readonly HashSet<long> ScanDedup = new HashSet<long>(8);
        private int _hitInstanceId;

        public static string colliderBundle = "other_prefab";
        public static string colliderAsset = "ColliderPerfab";

        /// <summary>HitGroupId 为 0 时用于「本事件实例」去重。</summary>
        public int HitInstanceId => _hitInstanceId;

        public override void Init(CombatEntity OwnCombat, XCNewEventsRunner runner)
        {
            base.Init(OwnCombat, runner);
            TriggerEventData = (XCTriggerEventData)EventData;
            _hitInstanceId = _nextHitInstanceId++;
            if (_hitInstanceId == 0)
                _hitInstanceId = _nextHitInstanceId++;
            FindTrigger(OwnCombat.IsCanCauseHarm);
            if (OwnCombat.IsCanCauseHarm && _collider != null)
                _collider.enabled = false;
        }

        public override void OnUpdateEvent(int frame, float timeSinceTrigger)
        {
            if (!OwnCombat.IsCanCauseHarm || _collider == null || !_collider.enabled)
                return;

            ScanExistingOverlaps();
        }

        void TryEnqueueHit(ICombatUnit target, Collider hitCollider)
        {
            if (target == null || SelfRunner == null || SelfRunner.IsDisposed)
                return;

            CombatEntity owner = SelfRunner.OwnerEntity;
            if (owner == null || target.Id == owner.Id)
                return;

            var context = CombatContext.Instance;
            if (context == null || context.HitPipeline == null)
                return;

            context.HitPipeline.Enqueue(new HitRequest
            {
                Attacker = owner,
                Defender = target,
                Runner = SelfRunner,
                TriggerEvent = this,
                HasHitWorldPosition = true,
                HitWorldPosition = ResolveOverlapHitPoint(_collider, hitCollider),
            });
        }

        /// <summary>
        /// 攻击盒与受击体的接触近似：受击体上靠近攻击盒中心的点。
        /// 非凸 MeshCollider 的 ClosestPoint 会失败，回退到 AABB。
        /// </summary>
        static Vector3 ResolveOverlapHitPoint(Collider attack, Collider defender)
        {
            if (defender == null)
                return Vector3.zero;
            if (attack == null)
                return defender.bounds.center;

            Vector3 attackCenter = attack.bounds.center;
            Vector3 onDefender = defender.ClosestPoint(attackCenter);
            if (!IsUsableClosestPoint(defender, attackCenter, onDefender))
                onDefender = defender.bounds.ClosestPoint(attackCenter);
            return onDefender;
        }

        static bool IsUsableClosestPoint(Collider col, Vector3 query, Vector3 result)
        {
            if ((result - query).sqrMagnitude > 0.0001f)
                return true;
            return col.bounds.Contains(query);
        }

        static bool IsColliderUnderRoot(Collider col, Transform unitRoot)
        {
            if (col == null || unitRoot == null)
                return false;

            Transform t = col.transform;
            return t == unitRoot || t.IsChildOf(unitRoot);
        }

        static void ClearPooledTriggerCallbacks(GameObject colliderObj)
        {
            if (colliderObj == null)
                return;

            var callback = colliderObj.GetComponent<OnTriggerEnterCallback>();
            if (callback != null)
                callback.OnTriggerEnterCallbackAction = null;
        }

        /// <summary>攻击窗口内每帧 Overlap 检测，不依赖 OnTriggerEnter 重入。</summary>
        void ScanExistingOverlaps()
        {
            if (_collider == null || !_collider.enabled)
                return;

            Physics.SyncTransforms();
            int count = QueryTriggerOverlapsNonAlloc(_collider, OverlapBuffer);
            if (count <= 0)
                return;

            var context = CombatContext.Instance;
            if (context == null)
                return;

            ScanDedup.Clear();
            CombatEntity owner = SelfRunner?.OwnerEntity;
            Transform ownerRoot = owner?.RootTransform;
            for (int i = 0; i < count; i++)
            {
                Collider hitCollider = OverlapBuffer[i];
                if (hitCollider == null)
                    continue;
                if (ownerRoot != null && IsColliderUnderRoot(hitCollider, ownerRoot))
                    continue;
                if (!context.TryResolveCombatUnit(hitCollider.gameObject, out var target)
                    || target == null)
                    continue;
                if (owner != null && target.Id == owner.Id)
                    continue;
                if (!ScanDedup.Add(target.Id))
                    continue;

                TryEnqueueHit(target, hitCollider);
            }
        }

        static int QueryTriggerOverlapsNonAlloc(Collider collider, Collider[] buffer)
        {
            Transform transform = collider.transform;
            if (collider is BoxCollider box)
            {
                Vector3 center = transform.TransformPoint(box.center);
                Vector3 halfExtents = Vector3.Scale(box.size * 0.5f, transform.lossyScale);
                return Physics.OverlapBoxNonAlloc(center, halfExtents, buffer, transform.rotation, ~0, QueryTriggerInteraction.Collide);
            }

            if (collider is SphereCollider sphere)
            {
                Vector3 center = transform.TransformPoint(sphere.center);
                float radius = sphere.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z);
                return Physics.OverlapSphereNonAlloc(center, radius, buffer, ~0, QueryTriggerInteraction.Collide);
            }

            if (collider is CapsuleCollider capsule)
            {
                GetCapsuleWorldPoints(capsule, out Vector3 point0, out Vector3 point1, out float radius);
                return Physics.OverlapCapsuleNonAlloc(point0, point1, radius, buffer, ~0, QueryTriggerInteraction.Collide);
            }

            return 0;
        }

        static void GetCapsuleWorldPoints(CapsuleCollider capsule, out Vector3 point0, out Vector3 point1, out float radius)
        {
            Transform transform = capsule.transform;
            Vector3 lossyScale = transform.lossyScale;
            float radiusScale = capsule.direction switch
            {
                0 => Mathf.Max(lossyScale.y, lossyScale.z),
                2 => Mathf.Max(lossyScale.x, lossyScale.y),
                _ => Mathf.Max(lossyScale.x, lossyScale.z),
            };
            radius = capsule.radius * radiusScale;
            float height = Mathf.Max(capsule.height * lossyScale[capsule.direction], radius * 2f);
            Vector3 center = transform.TransformPoint(capsule.center);
            Vector3 axis = capsule.direction switch
            {
                0 => transform.right,
                2 => transform.forward,
                _ => transform.up,
            };
            float halfHeight = height * 0.5f - radius;
            point0 = center - axis * halfHeight;
            point1 = center + axis * halfHeight;
        }

        //按照配置寻找碰撞体
        private void FindTrigger(bool isNeed)
        {
            if (isNeed)
            {
                _colliderObj = RunTimePoolManager.Instance.LoadResPoolObj(colliderBundle, colliderAsset);
                if (_colliderObj == null)
                    return;

                if (TriggerEventData.CubeRange.colliderType == ColliderType.Box)
                {
                    _collider = _colliderObj.GetComponent<BoxCollider>();
                }
                else if (TriggerEventData.CubeRange.colliderType == ColliderType.Sphere)
                {
                    _collider = _colliderObj.GetComponent<SphereCollider>();
                }
                else if (TriggerEventData.CubeRange.colliderType == ColliderType.Capsule)
                {
                    _collider = _colliderObj.GetComponent<CapsuleCollider>();
                }

                ClearPooledTriggerCallbacks(_colliderObj);
                // 命中盒仅用于 Overlap 查询，必须是 Trigger，否则会参与 Physics.Simulate 把敌人挤开。
                _collider.isTrigger = true;
                _colliderObj.transform.SetParent(this.OwnerTF.transform, false);
                _colliderObj.transform.localPosition = Vector3.zero;
                _colliderObj.SetActive(true);
            }
        }

        public override void OnTrigger(float timeSinceTrigger)
        {
            base.OnTrigger(timeSinceTrigger);
            if (OwnCombat.IsCanCauseHarm)
            {
                _colliderObj.transform.localEulerAngles = TriggerEventData.CubeRange.rotation;
                //设置大小 位置 角度
                if (TriggerEventData.CubeRange.colliderType == ColliderType.Box)
                {
                    BoxCollider boxCollider = (BoxCollider)_collider;
                    boxCollider.size = TriggerEventData.CubeRange.size;
                    boxCollider.center = TriggerEventData.CubeRange.pos;
                    boxCollider.enabled = true;
                }
                else if (TriggerEventData.CubeRange.colliderType == ColliderType.Sphere)
                {
                    SphereCollider sphereCollider = (SphereCollider)_collider;
                    sphereCollider.radius = TriggerEventData.CubeRange.radius;
                    sphereCollider.center = TriggerEventData.CubeRange.pos;
                    sphereCollider.enabled = true;
                }
                else if (TriggerEventData.CubeRange.colliderType == ColliderType.Capsule)
                {
                    CapsuleCollider capsuleCollider = (CapsuleCollider)_collider;
                    capsuleCollider.radius = TriggerEventData.CubeRange.radius;
                    capsuleCollider.center = TriggerEventData.CubeRange.pos;
                    capsuleCollider.height = TriggerEventData.CubeRange.height;
                    capsuleCollider.enabled = true;
                }
            }
            else if (_collider != null)
            {
                _collider.enabled = false;
            }
        }

        public override void OnFinish()
        {
            if (_colliderObj != null)
            {
                ClearPooledTriggerCallbacks(_colliderObj);
                if (_collider != null)
                    _collider.enabled = false;
                _collider = null;

                string path = RunTimePoolManager.GetResPath(colliderBundle, colliderAsset);
                RunTimePoolManager.Instance.ReCycle(path, _colliderObj);
                _colliderObj = null;
            }
        }

    }

    /// <summary>
    /// 事件切换（倾向于叫事件触发）
    /// </summary>
    public class XCSwitchEvent : XCEvent
    {
        public XCSwitchEventData eventData => (XCSwitchEventData)EventData;

        public override void OnTrigger(float timeSinceTrigger)
        {
            base.OnTrigger(timeSinceTrigger);

            //if (InputType == EventTriggerType.Exit)
            //{
            //    SelfRunner.BreakSkill();
            //}
            //else if (InputType == EventTriggerType.Finish)
            //{
            //    SelfRunner.Finish();
            //}
            //else

            if (eventData.InputType == EventTriggerType.ParentFinish)
            {
                //通知总运行器 当前技能主要部分已经运行结束，但依然会运行当前技能后续部分
                SelfRunner.GetParent<ActSkillRunner>().Finish();
            }
            else if (eventData.InputType == EventTriggerType.ParentExit)
            {
                //通知总运行器，当前技能已经全部结束，应回到其他状态。
                SelfRunner.GetParent<ActSkillRunner>().BreakSkill();
            }
        }
    }

    /// <summary>
    /// 消息发送
    /// </summary>
    public class XCMsgEvent : XCEvent
    {
        public XCMsgEventData eventData => (XCMsgEventData)EventData;

        public override void OnTrigger(float timeSinceTrigger)
        {
            base.OnTrigger(timeSinceTrigger);

            if (eventData.IsLocalTrueOnly)
            {
                if (NetId != PlayerManager.Instance.LocalNetId)
                {
                    Debug.Log($"yns isLocalOnly {eventData.MsgName}");
                    return;
                }
            }

            // 本地战斗实体直接落地（编辑器/单机）；网络仍走 PlayerManager
            long runnerId = SelfRunner?.GetParent<ActSkillRunner>()?.Id ?? 0;
            OwnCombat?.HandleTimelineMessage(
                eventData.MsgName,
                eventData.FloatdMsg,
                eventData.BoolMsg,
                TagSource.Skill(runnerId),
                eventData.StrMsg);

            //TODO 修改为本地
            switch (eventData.MsgEType)
            {
                case MsgType.Bool:
                    PlayerManager.Instance.SendBool(NetId, eventData.IsLocalTrueOnly, eventData.MsgName, eventData.BoolMsg);
                    break;
                case MsgType.All:
                    PlayerManager.Instance.SendAll(NetId, eventData.IsLocalTrueOnly, eventData.MsgName, eventData.FloatdMsg, eventData.BoolMsg, eventData.StrMsg);
                    break;
            }
        }

        public override void OnFinish()
        {
            if (eventData.MsgEType == MsgType.Bool)
            {
                if (eventData.SetOppositeOnFinish)
                {
                    OwnCombat?.HandleTimelineMessageFinish(
                        eventData.MsgName,
                        eventData.BoolMsg,
                        true);
                    PlayerManager.Instance.SendBool(NetId, eventData.IsLocalTrueOnly, eventData.MsgName, !eventData.BoolMsg);
                }
            }
        }
    }

    /// <summary>
    /// 效果触发器
    /// </summary>
    public class XCEffectEvent : XCEvent
    {
        public XCEffectEventData eventData => (XCEffectEventData)EventData;
        public override void OnTrigger(float timeSinceTrigger)
        {
            base.OnTrigger(timeSinceTrigger);

            // 空列表不再表示「技能全部效果」。标签轨常不配 Id，不能把段表伤害打到自己身上。
            if (eventData.NormalEffectIds != null && eventData.NormalEffectIds.Count > 0)
                SelfRunner.TriggerEffectList(eventData.NormalEffectIds, OwnCombat, 0);
            if (eventData.SkillEffectIds != null && eventData.SkillEffectIds.Count > 0)
                SelfRunner.TriggerEffectList(eventData.SkillEffectIds, OwnCombat, 0);

            //触发标签添加
            List<string> list = eventData.SkillTagList;
            if (list != null && list.Count > 0)
            {
                long runnerId = SelfRunner?.GetParent<ActSkillRunner>()?.Id ?? 0;
                var src = TagSource.Skill(runnerId);
                for (int i = 0; i < list.Count; i++)
                    OwnCombat.PushTag(src, list[i]);
            }
        }

        //轨道结束时立马移除
        public override void OnFinish()
        {
            if (eventData.SkillEffectIds != null && eventData.SkillEffectIds.Count > 0)
                SelfRunner.RemoveTriggerEffectList(eventData.SkillEffectIds, OwnCombat);

            List<string> list = eventData.SkillTagList;
            if (list != null && list.Count > 0)
            {
                long runnerId = SelfRunner?.GetParent<ActSkillRunner>()?.Id ?? 0;
                var src = TagSource.Skill(runnerId);
                for (int i = 0; i < list.Count; i++)
                    OwnCombat.PopTag(src, list[i]);
            }
        }
    }

    /// <summary>
    /// 玩家输入监听信息
    /// </summary>
    public class XCSkillInputEvent : XCEvent
    {
        public XCSkillInputEventData eventData;
        private bool _isHaveTrigger;
        private IAttackPlayer _attackPlayer;
        private ActSkillRunner _actSkillRunner;

        public override void Init(CombatEntity owner, XCNewEventsRunner runner)
        {
            base.Init(owner, runner);
            _actSkillRunner = runner.GetParent<ActSkillRunner>();
            if (owner.AttackPlayer is IAttackPlayer ap)
            {
                _attackPlayer = ap;
                _isHaveTrigger = false;
            }
            eventData = (XCSkillInputEventData)EventData;
        }

        public override void OnFinish()
        {
            _isHaveTrigger = false;
            _attackPlayer = null;
            _actSkillRunner = null;
        }

        public override void OnUpdateEvent(int frame, float timeSinceTrigger)
        {
            if (_isHaveTrigger || _attackPlayer == null || !OwnCombat.IsCanSpellSkill)
                return;
            if (eventData?.InputDataList == null)
                return;
            if (!_attackPlayer.TryResolveEdges(eventData.InputDataList, out int skillId, out int sort))
                return;

            SkillSpellInfo info = PoolManager.Instance.TryGet<SkillSpellInfo>();
            info.Target = LockSystem.Instance?.LockedCombatEntity ?? _actSkillRunner?.InputTarget;
            info.Point = MathHelper.GetPositionInFront(this.OwnCombat.Position, this.OwnCombat.Rotation, 3f);
            info.SkillId = skillId;
            info.Sort = sort;
            this.OwnCombat.GetComponent<ACTGameEditor.Combat.ActSpellComponent>().Enqueue(info);
            _isHaveTrigger = true;
        }
    }

}
