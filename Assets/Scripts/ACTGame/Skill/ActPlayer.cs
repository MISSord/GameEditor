using ACTGameEditor.Combat;
using ACTGameEditor.Locomotion;
using EGamePlay;
using EGamePlay.Combat;
using EGamePlay.Unity;
using System.Collections.Generic;
using UnityEngine;

namespace ACTGameEditor
{
    public interface ICameraTarget
    {
        Transform GetCameraTarget();
        Transform GetPlayerTransform();
        Vector3 GetPlayerPos();
        Vector3 GetCameraTargetPos();
    }

    public interface IAttackPlayer
    {
        //添加输入记录
        void AddInputRecord(InputListernType cmd, PressType type, InputCallBackType inputCallBackType);
        void ChangeInputMoveState(bool state);
        /// <summary>连招窗按白名单解析预输入，只消费赢家通道。</summary>
        bool TryResolveEdges(List<SkillInputData> edges, out int skillId, out int sort);

        /// <summary>
        /// 战斗 Tick 内、出手队列消费前：提交 Idle/硬打断槽位。
        /// 必须在当帧 Sample 之后调用，保证和 ActSpell 同一帧。
        /// </summary>
        void TickSkillInput();
    }

    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(Rigidbody))]
    public class ActPlayer : MonoBehaviour, ICameraTarget
    {
        private Transform _modelShow;
        private Transform _cameraTarget;
        [HideInInspector]
        public Transform UINode;

        /// <summary>飘字锚在脚底到头顶的插值（胸口）。UINode 在头顶给血条用，不能直接当出生点。</summary>
        const float DamageTextChestLerp = 0.42f;
        [HideInInspector]
        public AgentTag Agent;
        public CombatEntity Combat { get; private set; }

        /// <summary>对象池资源键（bundle|asset）；空则死亡后不回池。</summary>
        public string PoolResPath { get; private set; }

        /// <summary>玩家层时间源（与 AnimDirector 共用）。</summary>
        protected IAnimTimeScaleSource PlayerTimeSource { get; private set; }

        /// <summary>当前玩家层逻辑时间。</summary>
        protected float PlayerTime => PlayerTimeSource != null
            ? PlayerTimeSource.PlayerTime
            : GameTimeManager.PlayerTime;

        [Tooltip("角色配置 ID，对应 RoleAttriSetting 表的 CharacterId")]
        public int CharacterId;
        [Tooltip("角色等级，用于属性计算：基础值 + 等级 * 增长值")]
        public int Level = 1;

        [Header("Rendering")]
        [Tooltip("身体 SkinnedMesh 使用的材质（需 ACT/Character 才有遮挡描边等 Pass）")]
        [SerializeField]
        Material characterBodyMaterial;

        [Header("Death")]
        [Tooltip("HP 归零后是否播放噪声溶解（默认仅 enemy 阵营开启）")]
        [SerializeField]
        bool playDeathDissolve = true;
        [Tooltip("死亡溶解时长（秒），驱动 CharacterRenderFX._Dissolve 0→1")]
        [SerializeField]
        float deathDissolveDuration = 1.2f;

        /// <summary>是否播放死亡溶解。</summary>
        public virtual bool UseDeathDissolve => playDeathDissolve && Agent == AgentTag.enemy;

        /// <summary>死亡溶解时长。</summary>
        public virtual float DeathDissolveDuration => deathDissolveDuration;

        /// <summary>记录生成所用对象池键，死亡回收时使用。</summary>
        public void SetPoolResPath(string bundle, string asset)
        {
            PoolResPath = RunTimePoolManager.GetResPath(bundle, asset);
        }

        static readonly int IdleHash = Animator.StringToHash("Idle");
        static readonly int MoveSpeedHash = Animator.StringToHash("MoveSpeed");
        static readonly int IsRunHash = Animator.StringToHash("IsRun");

        /// <summary>
        /// 复位碰撞、渲染与动画残留，供对象池取出 / 回收复用。
        /// </summary>
        public void RestoreForReuse()
        {
            CharacterController controller = GetComponent<CharacterController>();
            if (controller != null)
                controller.enabled = true;

            Collider[] colliders = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                    colliders[i].enabled = true;
            }

            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                    renderers[i].enabled = true;
            }

            ParticleSystem[] particles = GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < particles.Length; i++)
                particles[i]?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            TrailRenderer[] trails = GetComponentsInChildren<TrailRenderer>(true);
            for (int i = 0; i < trails.Length; i++)
                trails[i]?.Clear();

            AudioSource[] audios = GetComponentsInChildren<AudioSource>(true);
            for (int i = 0; i < audios.Length; i++)
                audios[i]?.Stop();

            GetComponent<AfterimageController>()?.StopAfterimage();
            GetComponent<CharacterRenderFX>()?.ResetFX();

            if (_modelShow == null)
                _modelShow = transform.Find("ActTest");

            Animator animator = _modelShow != null ? _modelShow.GetComponent<Animator>() : null;
            if (animator != null)
            {
                animator.enabled = true;
                animator.SetFloat(MoveSpeedHash, 0f);
                animator.SetBool(IsRunHash, false);
                animator.Play(IdleHash, 0, 0f);
            }
        }

        /// <summary>
        /// 飘字世界锚点：胸口附近。绝区零/鸣潮都把数字贴在受击躯干上，而不是血条头顶。
        /// </summary>
        public Vector3 GetDamageTextAnchor()
        {
            Vector3 feet = transform.position;
            if (UINode != null)
                return Vector3.Lerp(feet, UINode.position, DamageTextChestLerp);
            return feet + Vector3.up * 1.05f;
        }

        /// <summary>装配战斗实体。人机不要传 isTruePlayer。</summary>
        public void Init(bool isTruePlayer = false)
        {
            _modelShow = transform.Find("ActTest");
            _cameraTarget = transform.Find("CinemachineCameraTarget");
            UINode = transform.Find("UINode");

            ApplyCharacterBodyMaterial();

            if (Level <= 0)
                Level = SkillSettingMgr.DefaultRoleLevel;

            GameObjectData data = new GameObjectData
            {
                agent = Agent,
                animator = _modelShow.GetComponent<Animator>(),
                controller = transform.GetComponent<CharacterController>(),
                playerSetting = AssetBundleManager.Instance.LoadAssetSync<PlayerMoveSettingSo>("config_prefab", "MoveSetting"),
                inputMoveBinder = CombatLocomotionInstaller.Default,
                animTimeScale = GameTimeAnimTimeScaleSource.Default,
                CharacterId = CharacterId,
                Level = Level,
                isTruePlayer = isTruePlayer,
            };

            PlayerTimeSource = data.animTimeScale ?? GameTimeAnimTimeScaleSource.Default;

            Combat = CombatContext.Instance.AddCombatUnit<CombatEntity>(data);
            CombatContext.Instance.Object2Entities[gameObject] = Combat;
            Combat.ModelTrans = _modelShow;
            Combat.RootTransform = transform;
            Combat.CurAgent = Agent;
            Combat.Position = transform.position;
            Combat.Rotation = transform.localRotation;
            Combat.AttackPlayer = this;

            EnsureCombatPresentation();
            StartCallBack();
        }

        void EnsureCombatPresentation()
        {
            CharacterRenderFX renderFx = GetComponent<CharacterRenderFX>();
            if (renderFx != null)
            {
                if (GetComponent<CharacterIceShell>() == null)
                    gameObject.AddComponent<CharacterIceShell>();
                if (GetComponent<AfterimageController>() == null)
                    gameObject.AddComponent<AfterimageController>();
                renderFx.BindModel(_modelShow);
            }

            CombatParticleClockDriver particleClock = GetComponent<CombatParticleClockDriver>();
            if (particleClock == null)
                particleClock = gameObject.AddComponent<CombatParticleClockDriver>();
            particleClock.Bind(Combat, _modelShow);
        }

        //与Init对应，销毁该实体
        public void Dispose()
        {
            DisposeCallBack();
            _cameraTarget = null;
            _modelShow = null;
            UINode = null;
            if (Combat != null)
            {
                Entity.Destroy(Combat);
                Combat = null;
            }
        }


        #region 复写方法
        protected virtual void StartCallBack(){}
        protected virtual void DisposeCallBack() { }
        #endregion
        
        public Transform GetCameraTarget()
        {
            return this._cameraTarget;
        }

        public Vector3 GetCameraTargetPos()
        {
            return this._cameraTarget.position;
        }

        public Transform GetPlayerTransform()
        {
            return this.transform;
        }

        public Vector3 GetPlayerPos()
        {
            return this.transform.position;
        }

        /// <summary>
        /// 将身体蒙皮网格统一为 ACT/Character 材质（武器 MeshRenderer 不改）。
        /// FBX 嵌套覆盖易失效，运行时赋值保证描边 Pass 存在。
        /// </summary>
        protected void ApplyCharacterBodyMaterial()
        {
            if (characterBodyMaterial == null || _modelShow == null)
                return;

            // 仅 SkinnedMesh：Beta_Surface / Beta_Joints；武器一般是 MeshRenderer
            var skins = _modelShow.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int i = 0; i < skins.Length; i++)
            {
                SkinnedMeshRenderer skin = skins[i];
                if (skin == null)
                    continue;

                Material[] mats = skin.sharedMaterials;
                if (mats == null || mats.Length == 0)
                {
                    skin.sharedMaterial = characterBodyMaterial;
                    continue;
                }

                for (int m = 0; m < mats.Length; m++)
                    mats[m] = characterBodyMaterial;
                skin.sharedMaterials = mats;
            }

            var renderFx = GetComponent<CharacterRenderFX>();
            if (renderFx != null)
                renderFx.BindModel(_modelShow);

            var objectFx = GetComponent<ObjectFxController>();
            objectFx?.RefreshDependents();

            var afterimage = GetComponent<AfterimageController>();
            afterimage?.RefreshSources(_modelShow);

            var particleClock = GetComponent<CombatParticleClockDriver>();
            particleClock?.Bind(Combat, _modelShow);
        }

        /// <summary>Locomotion 一段跳：不走路由技能轴，不占 Token。土狼/缓冲由电机消耗。</summary>
        public virtual void TryJump()
        {
            if (Combat == null || !Combat.IsCanJump)
                return;

            Combat.GetComponent<InputMoveComponent>()?.TryJump();
        }
    }
}
