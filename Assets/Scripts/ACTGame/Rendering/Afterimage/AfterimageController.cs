using System;
using UnityEngine;
using UnityEngine.Rendering;
using EGamePlay;
using EGamePlay.Combat;
using ACTGameEditor.Combat;

namespace ACTGameEditor
{
    /// <summary>
    /// 闪避残影：方案 A（PerRenderer），动态收集 modelRoot 下 SMR / MeshRenderer，3 槽 BakeMesh 快照。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AfterimageController : MonoBehaviour
    {
        const int MaxSlots = 8;
        const int MaxSources = 24;

        static readonly int GhostColorId = Shader.PropertyToID("_GhostColor");
        static readonly int AlphaId = Shader.PropertyToID("_Alpha");
        static readonly int EmissionBoostId = Shader.PropertyToID("_EmissionBoost");

        [SerializeField]
        Transform modelRoot;

        [SerializeField]
        Material ghostMaterial;

        [SerializeField]
        AfterimageProfile defaultProfile = new AfterimageProfile();

        [SerializeField]
        bool respectGraphicsFxGate = true;

        readonly GhostSource[] _sources = new GhostSource[MaxSources];
        readonly GhostSlot[] _slots = new GhostSlot[MaxSlots];
        readonly PendingCapture[] _pending = new PendingCapture[MaxSlots];
        MaterialPropertyBlock _mpb;

        Transform _afterimageRoot;
        ObjectFxController _objectFx;
        ActPlayer _actPlayer;
        int _sourceCount;
        int _slotCapacity;
        int _pendingCount;
        bool _playing;

        AfterimageProfile _activeProfile;

        /// <summary>是否正在播放（含待采样）。</summary>
        public bool IsPlaying => _playing;

        void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            _objectFx = GetComponent<ObjectFxController>();
            _activeProfile = defaultProfile ?? new AfterimageProfile();
            EnsureGhostMaterial();
            EnsureModelRoot();
            BuildGhostHierarchy();
        }

        void OnEnable()
        {
            var svc = FindObjectOfType<GraphicsFxService>();
            if (svc != null)
                svc.OnAnyChanged += OnFxChanged;
        }

        void OnDisable()
        {
            var svc = FindObjectOfType<GraphicsFxService>();
            if (svc != null)
                svc.OnAnyChanged -= OnFxChanged;
            StopAfterimage();
        }

        void OnFxChanged()
        {
            if (!IsAllowed() && _playing)
                StopAfterimage();
        }

        void Update()
        {
            if (!_playing)
                return;

            float dt = GetAfterimageDelta();

            for (int p = 0; p < _pendingCount; p++)
            {
                ref PendingCapture pending = ref _pending[p];
                if (!pending.Active)
                    continue;

                pending.Delay -= dt;
                if (pending.Delay > 0f)
                    continue;

                CaptureSlot(pending.SlotIndex, pending.BaseAlpha);
                pending.Active = false;
            }

            bool anyPending = false;
            for (int p = 0; p < _pendingCount; p++)
            {
                if (_pending[p].Active)
                {
                    anyPending = true;
                    break;
                }
            }

            bool anySlot = false;
            for (int s = 0; s < _slotCapacity; s++)
            {
                ref GhostSlot slot = ref _slots[s];
                if (!slot.Active)
                    continue;

                slot.Age += dt;
                float t = slot.Age / slot.Lifetime;
                if (t >= 1f)
                {
                    DeactivateSlot(s);
                    continue;
                }

                anySlot = true;
                float alpha = slot.BaseAlpha * (1f - t);
                ApplySlotVisuals(s, alpha, slot.GhostColor, slot.EmissionBoost);
            }

            _playing = anyPending || anySlot;
        }

        /// <summary>
        /// 绑定模型根并重建残影源与 Ghost 层级（换装后调用）。
        /// </summary>
        public void RefreshSources(Transform root)
        {
            if (root != null)
                modelRoot = root;

            StopAfterimage();
            BuildGhostHierarchy();
        }

        /// <summary>
        /// 播放一次闪避残影（默认 profile）。
        /// </summary>
        public void PlayAfterimage() => PlayAfterimage(null);

        /// <summary>
        /// 播放一次闪避残影。
        /// </summary>
        public void PlayAfterimage(AfterimageProfile profile)
        {
            if (!IsAllowed())
                return;

            EnsureModelRoot();
            if (_sourceCount <= 0)
                CollectSources();

            if (_sourceCount <= 0 || ghostMaterial == null)
                return;

            AfterimageProfile p = profile ?? defaultProfile;
            if (p == null)
                p = new AfterimageProfile();

            _activeProfile = p;

            if (_afterimageRoot == null || _sourceCount <= 0)
                BuildGhostHierarchy();

            StopAfterimage();

            int count = Mathf.Min(p.SnapshotCount, _slotCapacity);
            _pendingCount = count;
            _playing = count > 0;

            for (int i = 0; i < count; i++)
            {
                _pending[i] = new PendingCapture
                {
                    Active = true,
                    SlotIndex = i,
                    Delay = p.SnapshotDelays != null && i < p.SnapshotDelays.Length ? p.SnapshotDelays[i] : i * 0.04f,
                    BaseAlpha = p.SnapshotAlphas != null && i < p.SnapshotAlphas.Length ? p.SnapshotAlphas[i] : 0.35f,
                };

                _slots[i].Lifetime = p.Lifetime;
                _slots[i].GhostColor = p.GhostColor;
                _slots[i].EmissionBoost = p.EmissionBoost;
            }
        }

        /// <summary>停止并隐藏所有残影。</summary>
        public void StopAfterimage()
        {
            _playing = false;
            _pendingCount = 0;

            for (int p = 0; p < _pending.Length; p++)
                _pending[p].Active = false;

            for (int s = 0; s < _slotCapacity; s++)
                DeactivateSlot(s);
        }

        bool IsAllowed()
        {
            if (respectGraphicsFxGate && !GraphicsFxService.Query(GraphicsFxId.Afterimage))
                return false;

            if (_objectFx != null && !_objectFx.IsAllowed(ObjectFxFlags.Afterimage))
                return false;

            return true;
        }

        void EnsureModelRoot()
        {
            if (modelRoot != null)
                return;

            var renderFx = GetComponent<CharacterRenderFX>();
            if (renderFx != null)
            {
                Transform t = transform.Find("ActTest");
                if (t != null)
                    modelRoot = t;
            }

            if (modelRoot == null)
                modelRoot = transform;
        }

        void EnsureGhostMaterial()
        {
            if (ghostMaterial != null)
                return;

            Shader shader = Shader.Find("ACT/Ghost");
            if (shader == null)
                return;

            ghostMaterial = new Material(shader) { name = "ActGhost_Runtime" };
        }

        void BuildGhostHierarchy()
        {
            CollectSources();

            if (_afterimageRoot != null)
                Destroy(_afterimageRoot.gameObject);

            if (modelRoot == null)
                return;

            var rootGo = new GameObject("AfterimageRoot");
            _afterimageRoot = rootGo.transform;
            _afterimageRoot.SetParent(modelRoot, false);
            _afterimageRoot.localPosition = Vector3.zero;
            _afterimageRoot.localRotation = Quaternion.identity;
            _afterimageRoot.localScale = Vector3.one;

            _slotCapacity = AfterimageProfile.DefaultSnapshotCount;
            for (int s = 0; s < _slotCapacity; s++)
            {
                var slotGo = new GameObject($"GhostSlot_{s}");
                slotGo.transform.SetParent(_afterimageRoot, false);
                _slots[s] = new GhostSlot
                {
                    Root = slotGo.transform,
                    Parts = new GhostPart[_sourceCount],
                };

                for (int i = 0; i < _sourceCount; i++)
                {
                    ref GhostSource src = ref _sources[i];
                    var partGo = new GameObject($"Ghost_{src.Name}");
                    partGo.transform.SetParent(slotGo.transform, false);

                    var filter = partGo.AddComponent<MeshFilter>();
                    var renderer = partGo.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial = ghostMaterial;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                    renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                    renderer.enabled = false;

                    Mesh baked = null;
                    if (src.Skinned != null)
                    {
                        baked = new Mesh { name = $"GhostBake_{s}_{src.Name}" };
                        baked.MarkDynamic();
                    }

                    _slots[s].Parts[i] = new GhostPart
                    {
                        Transform = partGo.transform,
                        Filter = filter,
                        Renderer = renderer,
                        BakedMesh = baked,
                    };
                }
            }
        }

        void CollectSources()
        {
            _sourceCount = 0;
            if (modelRoot == null)
                return;

            var renderers = modelRoot.GetComponentsInChildren<Renderer>(true);
            for (int r = 0; r < renderers.Length && _sourceCount < MaxSources; r++)
            {
                Renderer renderer = renderers[r];
                if (!IsGhostSource(renderer))
                    continue;

                if (renderer is SkinnedMeshRenderer smr)
                {
                    _sources[_sourceCount++] = new GhostSource
                    {
                        Name = smr.gameObject.name,
                        Transform = smr.transform,
                        Skinned = smr,
                    };
                    continue;
                }

                if (renderer is MeshRenderer mr)
                {
                    MeshFilter mf = mr.GetComponent<MeshFilter>();
                    if (mf == null || mf.sharedMesh == null)
                        continue;

                    _sources[_sourceCount++] = new GhostSource
                    {
                        Name = mr.gameObject.name,
                        Transform = mr.transform,
                        StaticMesh = mf.sharedMesh,
                    };
                }
            }
        }

        static bool IsGhostSource(Renderer r)
        {
            if (r == null || !r.enabled || !r.gameObject.activeInHierarchy)
                return false;

            if (r is ParticleSystemRenderer or LineRenderer or TrailRenderer or SpriteRenderer)
                return false;

            return r is SkinnedMeshRenderer or MeshRenderer;
        }

        void CaptureSlot(int slotIndex, float baseAlpha)
        {
            AfterimageProfile profile = _activeProfile ?? defaultProfile ?? new AfterimageProfile();
            ref GhostSlot slot = ref _slots[slotIndex];
            slot.Active = true;
            slot.Age = 0f;
            slot.BaseAlpha = baseAlpha;
            slot.Lifetime = profile.Lifetime;
            slot.GhostColor = profile.GhostColor;
            slot.EmissionBoost = profile.EmissionBoost;

            for (int i = 0; i < _sourceCount; i++)
            {
                ref GhostSource src = ref _sources[i];
                ref GhostPart part = ref slot.Parts[i];
                if (part.Renderer == null)
                    continue;

                if (src.Skinned != null && part.BakedMesh != null)
                {
                    src.Skinned.BakeMesh(part.BakedMesh);
                    part.Filter.sharedMesh = part.BakedMesh;
                }
                else if (src.StaticMesh != null)
                {
                    part.Filter.sharedMesh = src.StaticMesh;
                }

                CopyWorldTransform(src.Transform, part.Transform);
                part.Renderer.enabled = true;
            }

            ApplySlotVisuals(slotIndex, baseAlpha, slot.GhostColor, slot.EmissionBoost);
        }

        static void CopyWorldTransform(Transform src, Transform dst)
        {
            dst.SetPositionAndRotation(src.position, src.rotation);
            dst.localScale = src.lossyScale;
        }

        void ApplySlotVisuals(int slotIndex, float alpha, Color ghostColor, float emissionBoost)
        {
            ref GhostSlot slot = ref _slots[slotIndex];
            _mpb.Clear();
            _mpb.SetColor(GhostColorId, ghostColor);
            _mpb.SetFloat(AlphaId, alpha);
            _mpb.SetFloat(EmissionBoostId, emissionBoost);

            for (int i = 0; i < _sourceCount; i++)
            {
                MeshRenderer r = slot.Parts[i].Renderer;
                if (r != null && r.enabled)
                    r.SetPropertyBlock(_mpb);
            }
        }

        void DeactivateSlot(int slotIndex)
        {
            ref GhostSlot slot = ref _slots[slotIndex];
            slot.Active = false;
            slot.Age = 0f;

            if (slot.Parts == null)
                return;

            for (int i = 0; i < slot.Parts.Length; i++)
            {
                if (slot.Parts[i].Renderer != null)
                    slot.Parts[i].Renderer.enabled = false;
            }
        }

        struct GhostSource
        {
            public string Name;
            public Transform Transform;
            public SkinnedMeshRenderer Skinned;
            public Mesh StaticMesh;
        }

        struct GhostPart
        {
            public Transform Transform;
            public MeshFilter Filter;
            public MeshRenderer Renderer;
            public Mesh BakedMesh;
        }

        float GetAfterimageDelta()
        {
            _actPlayer ??= GetComponent<ActPlayer>();
            CombatEntity combat = _actPlayer != null ? _actPlayer.Combat : null;
            if (combat != null && !combat.IsDisposed)
                return CombatTimeClock.GetLayerDelta(combat);
            return GameTimeManager.PlayerDelta;
        }

        struct GhostSlot
        {
            public Transform Root;
            public GhostPart[] Parts;
            public bool Active;
            public float Age;
            public float Lifetime;
            public float BaseAlpha;
            public Color GhostColor;
            public float EmissionBoost;
        }

        struct PendingCapture
        {
            public bool Active;
            public int SlotIndex;
            public float Delay;
            public float BaseAlpha;
        }
    }
}
