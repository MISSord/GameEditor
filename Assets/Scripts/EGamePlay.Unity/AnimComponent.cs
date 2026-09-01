using System;
using System.Collections.Generic;
using ACTGameEditor.Combat;
using UnityEngine;

namespace EGamePlay.Unity
{
    public enum AnimationEnem
    {
        Idle,
        Run,
        Jump,
        Attack,
        Skill1,
        Skill2,
        Skill3,
        Stun,
        Damage,
        Dead,
        Walk,
    }

    /// <summary>技能轴结束时 Animator 如何交回。</summary>
    public enum AnimExitPolicy : byte
    {
        /// <summary>交回 Locomotion（默认）。</summary>
        Locomotion = 0,
        /// <summary>只释放 Token，保持最后一帧 Pose。</summary>
        Hold = 1,
    }

    /// <summary>
    /// 动画系统使用的玩家层时间源（缩放、累计时间、变化通知）。
    /// </summary>
    public interface IAnimTimeScaleSource
    {
        /// <summary>玩家层时间缩放。</summary>
        float PlayerScale { get; }

        /// <summary>玩家层累计时间。</summary>
        float PlayerTime { get; }

        /// <summary>时间缩放变化时触发。</summary>
        event Action OnTimeScaleChanged;
    }

    /// <summary>将 <see cref="GameTimeManager"/> 桥接为动画时间源。</summary>
    public sealed class GameTimeAnimTimeScaleSource : IAnimTimeScaleSource
    {
        /// <summary>战斗场景默认时间源。</summary>
        public static readonly GameTimeAnimTimeScaleSource Default = new();

        /// <inheritdoc />
        public float PlayerScale => GameTimeManager.PlayerScale;

        /// <inheritdoc />
        public float PlayerTime => GameTimeManager.PlayerTime;

        /// <inheritdoc />
        public event Action OnTimeScaleChanged
        {
            add => GameTimeManager.OnTimeScaleChanged += value;
            remove => GameTimeManager.OnTimeScaleChanged -= value;
        }
    }

    /// <summary>未注入战斗时间源时的兜底（无缩放、无事件）。</summary>
    public sealed class UnityAnimTimeScaleSource : IAnimTimeScaleSource
    {
        /// <summary>默认兜底实例。</summary>
        public static readonly UnityAnimTimeScaleSource Default = new();

        /// <inheritdoc />
        public float PlayerScale => 1f;

        /// <inheritdoc />
        public float PlayerTime => Time.time;

#pragma warning disable 67
        /// <inheritdoc />
        public event Action OnTimeScaleChanged;
#pragma warning restore 67
    }

    /// <summary>
    /// 战斗动画唯一写入口：Token 所有权 + animator.speed + RootMotion/Motion Policy。
    /// </summary>
    public sealed class CombatAnimDirector
    {
        static readonly int IdleHash = Animator.StringToHash("Idle");
        static readonly int MoveSpeedId = Animator.StringToHash("MoveSpeed");
        static readonly int IsRunId = Animator.StringToHash("IsRun");
        static readonly int JumpHash = Animator.StringToHash("Jump");
        static readonly int DamageHash = Animator.StringToHash("Damage");

        const float DefaultLocomotionBlendSeconds = 0.08f;
        const float DefaultMoveDeadZone = 0.1f;
        const float DefaultReactionLength = 0.35f;

        private readonly AnimComponent _anim;
        private readonly Dictionary<int, float> _clipLengthByHash = new Dictionary<int, float>(32);

        private CombatEntity _owner;
        private RootMotionDriver _rootMotion;
        private MotionDirector _motion;
        private IAnimTimeScaleSource _timeScale;
        private int _token;
        private float _skillSpeed = 1f;
        private Action _onTimeScaleChanged;
        private bool _subscribedTimeScale;

        private int _autoReleaseToken;
        private float _autoReleaseAt;

        /// <summary>当前技能/反应动画所有权；0 表示已交回 Locomotion。</summary>
        public int CurrentToken => _token;

        /// <summary>是否仍有技能轴持有 Animator。</summary>
        public bool HasSkillOwner => _token != 0;

        /// <summary>
        /// 技能所在 Animator 层。0=与 Locomotion 同层（默认，无需改 Controller）；
        /// &gt;0 时播放前拉高该层权重，交回时权重置 0（需 Controller 有对应 Override 层）。
        /// </summary>
        public int SkillLayer { get; set; }

        /// <summary>SkillLayer&gt;0 时的目标权重。</summary>
        public float SkillLayerWeight { get; set; } = 1f;

        /// <summary>回 Locomotion 时读取的移动意图。</summary>
        public Func<Vector2> MoveIntentProvider { get; set; }

        /// <summary>判定有移动意图的死区。</summary>
        public float MoveIntentDeadZone { get; set; } = DefaultMoveDeadZone;

        public CombatAnimDirector(AnimComponent anim)
        {
            _anim = anim;
        }

        /// <summary>绑定玩家层时间源（动画 speed / 反应计时）。</summary>
        public void BindTimeScale(IAnimTimeScaleSource timeScale)
        {
            UnsubscribeTimeScale();
            _timeScale = timeScale ?? UnityAnimTimeScaleSource.Default;
            EnsureTimeScaleSubscription();
        }

        /// <summary>绑定战斗实体，用于时间缩放与实体缩放。</summary>
        public void BindOwner(CombatEntity owner)
        {
            _owner = owner;
            EnsureTimeScaleSubscription();
        }

        /// <summary>绑定位移裁决（Policy / 重力）。</summary>
        public void BindMotion(MotionDirector motion)
        {
            _motion = motion;
            _motion?.SetPolicy(MotionPolicy.Locomotion);
            _motion?.SetSkillSuppressGravity(false);
        }

        /// <summary>绑定 RootMotion 落地驱动（Animator 同物体）。</summary>
        public void BindRootMotion(RootMotionDriver driver)
        {
            _rootMotion = driver;
            _rootMotion?.SetTokenOwnsMotion(false);
        }

        /// <summary>解绑并取消时间缩放订阅。</summary>
        public void Unbind()
        {
            UnsubscribeTimeScale();

            ClearMotionSkillOwnership();
            _rootMotion = null;
            _motion = null;
            _owner = null;
            _timeScale = null;
            MoveIntentProvider = null;
            _clipLengthByHash.Clear();
            _token = 0;
            _skillSpeed = 1f;
            _autoReleaseToken = 0;
        }

        /// <summary>
        /// 播放技能片段并取得新 Token。旧 Token 立即失效。
        /// blendSeconds / fixedTimeOffset 单位为秒。
        /// applyRootMotion：本段是否采样 RM；suppressGravity：技能全控位移时关重力。
        /// </summary>
        public int PlaySkill(
            int stateHash,
            float blendSeconds,
            float fixedTimeOffset,
            float skillSpeed,
            bool applyRootMotion = true,
            bool suppressGravity = false)
        {
            _autoReleaseToken = 0;

            _token++;
            if (_token == 0)
                _token = 1;

            _skillSpeed = skillSpeed > 0f ? skillSpeed : 1f;
            Animator animator = _anim.animator;
            if (animator != null)
            {
                int layer = ResolvePlayLayer(animator);
                EnsureSkillLayerWeight(animator, layer, true);
                float blend = blendSeconds > 0f ? blendSeconds : 0f;
                float offset = fixedTimeOffset > 0f ? fixedTimeOffset : 0f;
                // 连闪同一片段时 CrossFade 到自身不会从头播，RootMotion 会沿第一次朝向走完。
                AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(layer);
                AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(layer);
                bool sameClip = current.shortNameHash == stateHash
                    || (animator.IsInTransition(layer) && next.shortNameHash == stateHash);
                if (sameClip)
                    animator.Play(stateHash, layer, 0f);
                else
                    animator.CrossFadeInFixedTime(stateHash, blend, layer, offset);
            }

            if (_motion != null)
            {
                _motion.SetPolicy(applyRootMotion ? MotionPolicy.RootMotion : MotionPolicy.SkillCurve);
                _motion.SetSkillSuppressGravity(suppressGravity);
            }

            SetRootMotionOwned(applyRootMotion);
            RefreshSpeedFromOwner();
            return _token;
        }

        /// <summary>
        /// 受击等反应动画：抢占 Token 播放，并在 clip 结束后自动 Release 回 Locomotion。
        /// Controller 无对应 State 时返回 0。
        /// </summary>
        public int PlayReaction(
            int stateHash,
            float blendSeconds = 0.05f,
            float speed = 1f,
            bool applyRootMotion = true,
            bool suppressGravity = false)
        {
            Animator animator = _anim.animator;
            if (animator == null || !HasAnimatorState(animator, stateHash))
                return 0;

            float len = DefaultReactionLength;
            if (TryGetClipLength(stateHash, out float cached) && cached > 0f)
                len = cached;

            int token = PlaySkill(stateHash, blendSeconds, 0f, speed, applyRootMotion, suppressGravity);
            float s = speed > 0f ? speed : 1f;
            _autoReleaseToken = token;
            _autoReleaseAt = _timeScale.PlayerTime + len / s;
            return token;
        }

        /// <summary>播放默认 Damage 反应（无 State 则空操作）。</summary>
        public int PlayDamageReaction(float blendSeconds = 0.05f)
        {
            return PlayReaction(DamageHash, blendSeconds);
        }

        /// <summary>
        /// Locomotion 一段跳：不占技能 Token。Controller 无 Jump State 时返回 false。
        /// </summary>
        public bool TryPlayLocomotionJump(float blendSeconds = 0.05f)
        {
            if (HasSkillOwner)
                return false;

            Animator animator = _anim.animator;
            if (animator == null || !HasAnimatorState(animator, JumpHash))
                return false;

            float blend = blendSeconds > 0f ? blendSeconds : 0.05f;
            animator.CrossFadeInFixedTime(JumpHash, blend, 0, 0f);
            RefreshSpeedFromOwner();
            return true;
        }

        /// <summary>驱动反应动画自动交回；由玩家 Update 调用。</summary>
        public void Tick()
        {
            if (_timeScale == null)
                return;
            Tick(_timeScale.PlayerTime);
        }

        /// <summary>驱动反应动画自动交回（显式传入玩家时间）。</summary>
        public void Tick(float playerTime)
        {
            if (_autoReleaseToken == 0)
                return;
            if (playerTime < _autoReleaseAt)
                return;

            int token = _autoReleaseToken;
            _autoReleaseToken = 0;
            Release(token, returnToLocomotion: true);
        }

        /// <summary>缓存 AnimName hash → clip 时长。</summary>
        public void CacheClipLength(int stateHash, float length)
        {
            if (stateHash == 0 || length <= 0f)
                return;
            _clipLengthByHash[stateHash] = length;
        }

        /// <summary>取缓存的 clip 时长。</summary>
        public bool TryGetClipLength(int stateHash, out float length)
        {
            return _clipLengthByHash.TryGetValue(stateHash, out length);
        }

        /// <summary>CrossFade 后按时间轴偏移 scrub。</summary>
        public void Scrub(float timeSinceTrigger)
        {
            Animator animator = _anim.animator;
            if (animator == null || timeSinceTrigger <= 0f)
                return;
            animator.Update(timeSinceTrigger - 0.001f);
        }

        /// <summary>仅当 token 仍为当前持有者时更新技能倍速，并立即刷 speed。</summary>
        public void SetSkillSpeed(int token, float skillSpeed)
        {
            if (token == 0 || token != _token)
                return;
            _skillSpeed = skillSpeed > 0f ? skillSpeed : 1f;
            RefreshSpeedFromOwner();
        }

        /// <summary>按 Owner 刷新 animator.speed（唯一对外写 speed 入口）。</summary>
        public void RefreshSpeedFromOwner()
        {
            float entityScale = _owner != null ? _owner.GetTimeScale() : 1f;
            float playerScale = _timeScale != null ? _timeScale.PlayerScale : 1f;
            RefreshSpeed(playerScale, entityScale);
        }

        /// <summary>写 animator.speed。</summary>
        public void RefreshSpeed(float playerScale, float entityScale)
        {
            Animator animator = _anim.animator;
            if (animator == null)
                return;

            float skill = HasSkillOwner ? _skillSpeed : 1f;
            animator.speed = skill * playerScale * entityScale;
        }

        /// <summary>
        /// 释放所有权。returnToLocomotion 为 true 时按 ExitPolicy.Locomotion 交回。
        /// </summary>
        public void Release(int token, bool returnToLocomotion)
        {
            if (token == 0 || token != _token)
                return;

            if (_autoReleaseToken == token)
                _autoReleaseToken = 0;

            _token = 0;
            _skillSpeed = 1f;
            ClearMotionSkillOwnership();

            if (returnToLocomotion)
                ReturnToLocomotion();
            else
                RefreshSpeedFromOwner();
        }

        /// <summary>强制交回 Locomotion。</summary>
        public void ForceLocomotion()
        {
            _autoReleaseToken = 0;
            _token = 0;
            _skillSpeed = 1f;
            ClearMotionSkillOwnership();
            ReturnToLocomotion();
        }

        void ClearMotionSkillOwnership()
        {
            SetRootMotionOwned(false);
            if (_motion != null)
            {
                _motion.SetPolicy(MotionPolicy.Locomotion);
                _motion.SetSkillSuppressGravity(false);
            }
        }

        void SetRootMotionOwned(bool owns)
        {
            _rootMotion?.SetTokenOwnsMotion(owns);
        }

        void ReturnToLocomotion()
        {
            Animator animator = _anim.animator;
            if (animator == null)
                return;

            int skillLayer = ResolvePlayLayer(animator);
            if (skillLayer > 0)
            {
                EnsureSkillLayerWeight(animator, skillLayer, false);
            }
            else
            {
                bool wantMove = HasMoveIntent();
                animator.SetFloat(MoveSpeedId, wantMove ? 1f : 0f);
                animator.SetBool(IsRunId, wantMove);
                animator.CrossFadeInFixedTime(IdleHash, DefaultLocomotionBlendSeconds, 0, 0f);
            }

            RefreshSpeedFromOwner();
        }

        int ResolvePlayLayer(Animator animator)
        {
            int layer = SkillLayer;
            if (layer <= 0)
                return 0;
            return layer < animator.layerCount ? layer : 0;
        }

        void EnsureSkillLayerWeight(Animator animator, int layer, bool active)
        {
            if (layer <= 0 || layer >= animator.layerCount)
                return;
            animator.SetLayerWeight(layer, active ? SkillLayerWeight : 0f);
        }

        static bool HasAnimatorState(Animator animator, int stateHash)
        {
            int layers = animator.layerCount;
            for (int i = 0; i < layers; i++)
            {
                if (animator.HasState(i, stateHash))
                    return true;
            }
            return false;
        }

        bool HasMoveIntent()
        {
            if (MoveIntentProvider == null)
                return false;
            Vector2 axis = MoveIntentProvider();
            float dead = MoveIntentDeadZone > 0f ? MoveIntentDeadZone : DefaultMoveDeadZone;
            return axis.sqrMagnitude > dead * dead;
        }

        /// <summary>当前绑定的玩家层时间。</summary>
        public float PlayerTime => _timeScale != null ? _timeScale.PlayerTime : 0f;

        void EnsureTimeScaleSubscription()
        {
            if (_subscribedTimeScale || _timeScale == null)
                return;
            _onTimeScaleChanged = RefreshSpeedFromOwner;
            _timeScale.OnTimeScaleChanged += _onTimeScaleChanged;
            _subscribedTimeScale = true;
        }

        void UnsubscribeTimeScale()
        {
            if (!_subscribedTimeScale || _onTimeScaleChanged == null || _timeScale == null)
            {
                _subscribedTimeScale = false;
                return;
            }

            _timeScale.OnTimeScaleChanged -= _onTimeScaleChanged;
            _subscribedTimeScale = false;
        }
    }

    public class AnimComponent : Component
    {
        public Animator animator { get; private set; }

        /// <summary>技能/Locomotion 动画唯一写入口。</summary>
        public CombatAnimDirector Director { get; private set; }

        /// <summary>位移裁决（Policy / 重力 / 唯一 cc.Move）。</summary>
        public MotionDirector Motion { get; private set; }

        public override void Awake(object initData)
        {
            GameObjectData data = (GameObjectData)initData;
            animator = data.animator;

            Motion = new MotionDirector();
            Motion.Bind(data.controller);

            Director = new CombatAnimDirector(this);
            Director.BindTimeScale(data.animTimeScale);
            Director.BindOwner(GetEntity<CombatEntity>());
            Director.BindMotion(Motion);

            if (animator != null)
            {
                var rm = animator.GetComponent<RootMotionDriver>();
                if (rm == null)
                    rm = animator.gameObject.AddComponent<RootMotionDriver>();
                rm.Bind(Motion);
                Director.BindRootMotion(rm);
            }
        }

        public override void OnDestroy()
        {
            Director?.Unbind();
            Director = null;
            Motion?.Unbind();
            Motion = null;
            animator = null;
        }

        public override void OnReset() => OnDestroy();

        /// <summary>按照固定时间长度进行混合。</summary>
        public void PlayFadeInFixedTime(int nameHash, float time = 0.2f, int layer = -1, float fixedTimeOffset = 0.0f)
        {
            animator?.CrossFadeInFixedTime(nameHash, time, layer, fixedTimeOffset);
        }

        /// <summary>按照动画长度百分比进行混合。</summary>
        public void PlayFade(int nameHash, float time = 0.2f, int layer = -1, float fixedTimeOffset = 0.0f)
        {
            animator.CrossFade(nameHash, time, layer, fixedTimeOffset);
        }

        /// <summary>调整动画播放速度（Animator 参数）。</summary>
        public void PlayFadeChangeSpeed(string name, float Speed)
        {
            string nameSpeed = name + "Speed";
            animator?.SetFloat(nameSpeed, Speed);
        }
    }
}
