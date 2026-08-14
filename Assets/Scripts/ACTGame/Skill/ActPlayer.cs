using EGamePlay;
using EGamePlay.Combat;
using UnityEngine;
using XiaoCao;

namespace ACTGameEditor
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(Rigidbody))]
    public class ActPlayer : MonoBehaviour, ICameraTarget
    {
        private Transform _modelShow;
        private Transform _cameraTarget;
        [HideInInspector]
        public Transform UINode;
        [HideInInspector]
        public AgentTag Agent;
        public CombatEntity Combat { get; private set; }

        [Tooltip("角色配置 ID，对应 RoleAttriSetting 表的 CharacterId")]
        public int CharacterId;
        [Tooltip("角色等级，用于属性计算：基础值 + 等级 * 增长值")]
        public int Level = 1;

        [Header("Rendering")]
        [Tooltip("身体 SkinnedMesh 使用的材质（需 ACT/Character 才有遮挡描边等 Pass）")]
        [SerializeField]
        Material characterBodyMaterial;

        public void Init()
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
                CharacterId = CharacterId,
                Level = Level,
            };

            Combat = CombatContext.Instance.AddCombatEntity(data);
            CombatContext.Instance.Object2Entities.Add(gameObject, Combat);
            Combat.ModelTrans = _modelShow;
            Combat.RootTransform = transform;
            Combat.CurAgent = Agent;
            Combat.Position = transform.position;
            Combat.Rotation = transform.localRotation;
            Combat.AttackPlayer = this;

            StartCallBack();
        }

        //与Init对应，销毁该实体
        public void Dispose()
        {
            DisposeCallBack();
            _cameraTarget = null;
            _modelShow = null;
            UINode = null;
            Entity.Destroy(Combat);
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
        }
    }
}
