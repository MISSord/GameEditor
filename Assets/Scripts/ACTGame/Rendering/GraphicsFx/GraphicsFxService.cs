using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ACTGameEditor
{
    /// <summary>
    /// 图形效果运行时开关服务：默认值来自 Config，覆盖写入 PlayerPrefs，变更时广播。
    /// </summary>
    public sealed class GraphicsFxService : MonoBehaviour
    {
        const string PrefsPrefix = "ACT.GfxFx.";

        static GraphicsFxService _instance;

        /// <summary>全局单例（场景中需存在或自动创建）。</summary>
        public static GraphicsFxService Instance
        {
            get
            {
                if (_instance != null)
                    return _instance;

                _instance = FindObjectOfType<GraphicsFxService>();
                if (_instance != null)
                    return _instance;

                var go = new GameObject(nameof(GraphicsFxService));
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<GraphicsFxService>();
                return _instance;
            }
        }

        [SerializeField]
        GraphicsFxConfig config;

        [SerializeField]
        bool autoApplyOnAwake = true;

        readonly Dictionary<GraphicsFxId, bool> _overrides = new();

        /// <summary>任意开关变更时触发（参数为变更的 Id）。</summary>
        public event Action<GraphicsFxId> OnChanged;

        /// <summary>任意开关变更后触发，便于全量刷新。</summary>
        public event Action OnAnyChanged;

        /// <summary>当前绑定的配置资源。</summary>
        public GraphicsFxConfig Config => config;

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
            LoadPrefs();

            if (autoApplyOnAwake)
                GraphicsFxApplier.ApplyAll(this);

            EnsureCameraPostFx();
        }

        void EnsureCameraPostFx()
        {
            if (GetComponent<CameraPostFxController>() == null)
                gameObject.AddComponent<CameraPostFxController>();
        }

        void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Volume / Light / Feature 绑定随场景变化，需重绑后重 Apply
            GraphicsFxApplier.InvalidateCache();
            GraphicsFxApplier.ApplyAll(this);
            OnAnyChanged?.Invoke();
        }

        /// <summary>
        /// 绑定配置资源（可在运行时替换）。
        /// </summary>
        public void SetConfig(GraphicsFxConfig newConfig, bool apply = true)
        {
            config = newConfig;
            if (apply)
                GraphicsFxApplier.ApplyAll(this);
        }

        /// <summary>
        /// 查询效果是否开启。
        /// </summary>
        public bool IsEnabled(GraphicsFxId id)
        {
            if (_overrides.TryGetValue(id, out bool value))
                return value;

            return config != null ? config.GetDefault(id) : true;
        }

        /// <summary>
        /// 设置开关并持久化，随后 Apply + 广播。
        /// </summary>
        public void SetEnabled(GraphicsFxId id, bool enabled, bool persist = true)
        {
            if (_overrides.TryGetValue(id, out bool cur) && cur == enabled)
            {
                // 仍可能与 default 相同但未 Apply；仅值未变则跳过
                if (IsEnabled(id) == enabled)
                    return;
            }

            _overrides[id] = enabled;
            if (persist)
                PlayerPrefs.SetInt(PrefsKey(id), enabled ? 1 : 0);

            GraphicsFxApplier.Apply(this, id);
            OnChanged?.Invoke(id);
            OnAnyChanged?.Invoke();
        }

        /// <summary>
        /// 重置为 Config 默认并清除 PlayerPrefs。
        /// </summary>
        public void ResetToDefaults()
        {
            foreach (GraphicsFxId id in Enum.GetValues(typeof(GraphicsFxId)))
                PlayerPrefs.DeleteKey(PrefsKey(id));

            _overrides.Clear();
            GraphicsFxApplier.ApplyAll(this);
            OnAnyChanged?.Invoke();
        }

        /// <summary>
        /// 静态便捷查询（无 Instance 时默认 true）。
        /// </summary>
        public static bool Query(GraphicsFxId id)
        {
            if (_instance == null)
            {
                var found = FindObjectOfType<GraphicsFxService>();
                if (found == null)
                    return true;
            }

            return Instance.IsEnabled(id);
        }

        void LoadPrefs()
        {
            _overrides.Clear();
            foreach (GraphicsFxId id in Enum.GetValues(typeof(GraphicsFxId)))
            {
                string key = PrefsKey(id);
                if (!PlayerPrefs.HasKey(key))
                    continue;
                _overrides[id] = PlayerPrefs.GetInt(key, 1) != 0;
            }
        }

        static string PrefsKey(GraphicsFxId id) => PrefsPrefix + id;
    }
}
