using DG.Tweening;
using EGamePlay.Combat;
using System;
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
        private Animator _animator;
        private AnimationClip _clip;
        private int _cachedClipHash;
        private Action _onTimeScaleChangedHandler;

        public override void Init(CombatEntity owner, XCNewEventsRunner runner)
        {
            base.Init(owner, runner);
            _cachedClipHash = Animator.StringToHash(eventData.AnimName);
        }

        /// <summary>时间流速变化时重新应用 animator.speed，保证时空断裂/HitStop 等生效。</summary>
        private void ApplyAnimatorSpeedToTimeScale()
        {
            if (_animator == null || SelfRunner == null) return;
            float entityScale = OwnCombat?.GetTimeScale() ?? 1f;
            _animator.speed = SelfRunner.Speed * GameTimeManager.PlayerScale * entityScale;
        }

        /// <summary>仅在 clip 为 null 时遍历 animationClips 查找，避免每 Trigger 重复分配</summary>
        private void TryCacheClipFromController()
        {
            if (_clip != null || _animator?.runtimeAnimatorController == null) return;
            var clips = _animator.runtimeAnimatorController.animationClips;
            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i].name == eventData.AnimName)
                {
                    _clip = clips[i];
                    return;
                }
            }
        }

        public override void OnTrigger(float timeSinceTrigger)
        {
            if (_animator == null)
                _animator = SelfRunner.GetAnimator();
            if (_animator == null)
            {
                Debug.LogError("no _animator " + eventData.AnimName);
                return;
            }

            TryCacheClipFromController();
            if (_clip == null)
            {
                Debug.LogError("no clip " + eventData.AnimName);
                return;
            }

            float entityScale = OwnCombat?.GetTimeScale() ?? 1f;
            _animator.speed = SelfRunner.Speed * GameTimeManager.PlayerScale * entityScale;
            if (_onTimeScaleChangedHandler == null)
                _onTimeScaleChangedHandler = ApplyAnimatorSpeedToTimeScale;
            GameTimeManager.OnTimeScaleChanged += _onTimeScaleChangedHandler;
            base.OnTrigger(timeSinceTrigger);
            _animator.CrossFade(_cachedClipHash, eventData.BlenderLength / _clip.length, 0, eventData.StartOffset / _clip.length);

            if (timeSinceTrigger > 0)
                _animator.Update(timeSinceTrigger - 0.001f);
        }

        public override void OnFinish()
        {
            if (_onTimeScaleChangedHandler != null)
            {
                GameTimeManager.OnTimeScaleChanged -= _onTimeScaleChangedHandler;
                _onTimeScaleChangedHandler = null;
            }
            if (eventData.IsBackToIdle && _animator != null)
            {
                _animator.CrossFade("Idle", 0.2f);
            }
            _animator = null;
            _clip = null;
        }
    }

    public class XCObjEvent : XCEvent
    {
        private XCObjEventData _eventData;
        private ParticleSystem _ps;
        public GameObject LoadObj { get; private set; }

        public override void Init(CombatEntity owner, XCNewEventsRunner runner)
        {
            base.Init(owner, runner);
            _eventData = (XCObjEventData)EventData;
            LoadObj = RunTimePoolManager.Instance.LoadResPoolObj(_eventData.BundlePath, _eventData.AssetPath);
            LoadObj.SetActive(false);
            SelfRunner.ObjEvent = this;  //赋值给运行器
        }

        public override void OnTrigger(float timeSinceTrigger)
        {
            SetFirstPos();
            if (_eventData.IsEffect)
            {
                _ps = LoadObj.GetComponentInChildren<ParticleSystem>();
                _ps.Play(true);
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
            {
                LoadObj.transform.SetParent(OwnCombat.RootTransform, true);
            }
        }

        public override void OnFinish()
        {
            if (_ps)
            {
                _ps.Stop(true);
                _ps = null;
            }

            if (LoadObj != null)
            {
                LoadObj.gameObject.SetActive(false);
                RunTimePoolManager.Instance.ReCycle(
                    RunTimePoolManager.GetResPath(_eventData.BundlePath, _eventData.AssetPath), 
                    LoadObj);
                LoadObj = null;
            }
        }
    }

    public class XCMoveEvent : XCLineEvent
    {
        private CharacterController _cc;
        private Matrix4x4 _m4;

        public override void Init(CombatEntity owner, XCNewEventsRunner runner)
        {
            base.Init(owner, runner);
            _m4 = OwnerTF.localToWorldMatrix;
        }

        public override void OnTrigger(float timeSinceTrigger)
        {
            base.OnTrigger(timeSinceTrigger);
            _cc = OwnerTF.GetComponent<CharacterController>();

            var moveData = (XCMoveEventData)EventData;
            if (moveData.StartVec != Vector3.zero)
            {
                ApplyDetalVec(moveData.StartDetal);
            }
        }

        public override void ApplyDetalVec(Vector3 detalMove)
        {
            if (_cc != null)
            {
                _cc.Move(_m4.MultiplyVector(detalMove));
            }
            else
            {
                OwnerTF.Translate(_m4.MultiplyVector(detalMove), Space.World);
            }

            var moveData = (XCMoveEventData)EventData;
            if (moveData.LookForward)
            {
                OwnerTF.forward = _m4.MultiplyVector(detalMove);
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
        private OnTriggerEnterCallback _callBack;

        public static string colliderBundle = "other_prefab";
        public static string colliderAsset = "ColliderPerfab";

        public override void Init(CombatEntity OwnCombat, XCNewEventsRunner runner)
        {
            base.Init(OwnCombat, runner);
            TriggerEventData = (XCTriggerEventData)EventData;
            FindTrigger(OwnCombat.IsCanCauseHarm);
            if (OwnCombat.IsCanCauseHarm)
            {
                _collider.enabled = false;

                _callBack = _colliderObj.GetOrAddComponent<OnTriggerEnterCallback>();
                _callBack.OnTriggerEnterCallbackAction = (other) =>
                {
                    if (SelfRunner.IsDisposed)
                    {
                        return;
                    }

                    CombatEntity owner = SelfRunner.OwnerEntity;
                    CombatEntity target = null;
                    if (CombatContext.Instance.Object2Entities.TryGetValue(other.gameObject, out var otherEntity))
                    {
                        if (otherEntity == owner) 
                            return;
                        target = otherEntity;
                    }

                    //产生碰撞实体，处理碰撞
                    if (target != null && owner.CollisionAbility.TryMakeAction(out var collisionAction))
                    {
                        collisionAction.Runner = SelfRunner;
                        collisionAction.Target = target;
                        collisionAction.triggerEvent = this;
                        collisionAction.ApplyCollision();
                    }
                };
            }
        }

        //按照配置寻找碰撞体
        private void FindTrigger(bool isNeed)
        {
            if (isNeed)
            {
                _colliderObj = RunTimePoolManager.Instance.LoadResPoolObj(colliderBundle, colliderAsset);

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
            else
            {
                if(_collider != null)
                {
                    _collider.enabled = false;
                }
            }
        }

        public override void OnFinish()
        {
            if (_colliderObj != null)
            {
                _collider.enabled = false;
                _collider = null;

                _callBack.OnTriggerEnterCallbackAction = null;
                _callBack = null;

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
                //通知总运行器
                SelfRunner.GetParent<ActSkillRunner>().Finish();
            }
            else if (eventData.InputType == EventTriggerType.ParentExit)
            {
                //通知总运行器
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

            //触发技能效果
            SelfRunner.TriggerEffectList(eventData.NormalEffectIds, OwnCombat, 0);
            SelfRunner.TriggerEffectList(eventData.SkillEffectIds, OwnCombat, 0);

            //触发标签添加
            List<string> list = eventData.SkillTagList;
            if (list != null && list.Count > 0)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    this.OwnCombat.AddTag(list[i]);
                }
            }
        }

        //轨道结束时立马移除
        public override void OnFinish()
        {
            SelfRunner.RemoveTriggerEffectList(eventData.SkillEffectIds, OwnCombat);

            List<string> list = eventData.SkillTagList;
            if (list != null && list.Count > 0)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    this.OwnCombat.RemoveTag(list[i]);
                }
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

        //每帧判断监听
        public override void OnUpdateEvent(int frame, float timeSinceTrigger)
        {
            //判断是否有玩家的输入
            if (_isHaveTrigger == false && this._attackPlayer != null && this._attackPlayer.IsHadInputRecords() && OwnCombat.IsCanSpellSkill)
            {
                //InputDataList在保存的时候是按照优先级从高到低保存的
                SkillInputData data;
                for (int i = 0; i < eventData.InputDataList.Count; i++)
                {
                    data = eventData.InputDataList[i];
                    //先检查 RequiredTags/BlockedTags，不满足则跳过（不消费输入）
                    if (OwnCombat.CanSpellSkillWithTagLists(data.RequiredTags, data.BlockedTags) == false)
                        continue;
                    if (_attackPlayer.CheckAndConsume(data.ListernType, data.PressType, data.InputCallBackType, data.InputTimeout > 0 ? data.InputTimeout : -1f))
                    {
                        SkillSpellInfo info = PoolManager.Instance.TryGet<SkillSpellInfo>();
                        info.Target = LockSystem.Instance?.LockedCombatEntity ?? _actSkillRunner?.InputTarget;
                        info.Point = MathHelper.GetPositionInFront(this.OwnCombat.Position, this.OwnCombat.Rotation, 3f);
                        info.SkillId = data.SkillId;
                        info.Sort = data.SkillSort;
                        this.OwnCombat.GetComponent<SpellComponent>().AddSkillSpellInfo(info);
                        this._attackPlayer.InputRecordsClear();
                        _isHaveTrigger = true;
                        break;
                    }
                }
            }
        }
    }

}
