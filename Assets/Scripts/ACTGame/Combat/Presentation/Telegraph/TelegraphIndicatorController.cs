using EGamePlay.Combat;
using UnityEngine;

namespace ACTGameEditor.Combat
{
    /// <summary>
    /// 敌人出手预警指示器池：宿主头顶的四角星闪光（ZZZ 黄/红光手感的美术占位）。
    /// 贴图运行期程序化生成，不依赖任何资产；换正式特效时只改本控制器内部。
    /// 走 unscaled 时间：时间断裂 / HitStop 中预警保持可读（与 CameraShakeController 同约定）。
    /// 热路径零 GC：固定池 + 结构体内态，无每帧分配。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TelegraphIndicatorController : MonoBehaviour
    {
        /// <summary>同屏预警上限。攻击 Token 预算远小于此，正常不会打满。</summary>
        public const int PoolSize = 8;

        const float PopInSeconds = 0.09f;
        const float SettleSeconds = 0.07f;
        const float FadeOutSeconds = 0.12f;
        const float PulseSpeed = 14f;
        const float PulseAmplitude = 0.06f;
        const float BaseScale = 0.9f;

        /// <summary>桥接令牌：槽位 + 代数，槽复用后旧令牌 Stop 不会误停新指示。</summary>
        public sealed class Token
        {
            public int Slot;
            public int Generation;
        }

        struct Slot
        {
            public GameObject Go;
            public SpriteRenderer Renderer;
            public ICombatUnit Unit;
            public float Height;
            public float Age;
            public float Duration;
            public int Generation;
            public bool Active;
        }

        static TelegraphIndicatorController _instance;
        static Sprite _crossSprite;

        readonly Slot[] _slots = new Slot[PoolSize];
        Transform _cameraTransform;

        /// <summary>全局实例：场景未预挂时自动创建（与 CameraShakeController 同模式）。</summary>
        public static TelegraphIndicatorController Instance
        {
            get
            {
                if (_instance != null)
                    return _instance;

                _instance = FindObjectOfType<TelegraphIndicatorController>();
                if (_instance != null)
                    return _instance;

                var go = new GameObject(nameof(TelegraphIndicatorController));
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<TelegraphIndicatorController>();
                return _instance;
            }
        }

        void Awake()
        {
            _instance = this;
            Sprite sprite = GetOrCreateCrossSprite();
            for (int i = 0; i < PoolSize; i++)
            {
                var go = new GameObject($"Telegraph_{i}");
                go.transform.SetParent(transform, false);
                var spriteRenderer = go.AddComponent<SpriteRenderer>();
                spriteRenderer.sprite = sprite;
                spriteRenderer.sortingOrder = 60;
                go.SetActive(false);
                _slots[i] = new Slot { Go = go, Renderer = spriteRenderer };
            }
        }

        void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        /// <summary>占一槽播放；宿主无效或池满返回 null（调用方按无表现处理）。</summary>
        public Token Play(ICombatUnit unit, Color color, float durationSeconds, float height)
        {
            if (unit == null || unit.IsDisposed || durationSeconds <= 0f)
                return null;

            for (int i = 0; i < PoolSize; i++)
            {
                if (_slots[i].Active)
                    continue;

                Slot slot = _slots[i];
                slot.Unit = unit;
                slot.Height = height;
                slot.Age = 0f;
                slot.Duration = durationSeconds;
                slot.Generation++;
                slot.Active = true;
                slot.Go.transform.position = unit.Position + Vector3.up * height;
                slot.Renderer.color = color;
                slot.Go.SetActive(true);
                _slots[i] = slot;
                return new Token { Slot = i, Generation = slot.Generation };
            }

            return null;
        }

        /// <summary>提前收尾：把剩余时长截进淡出窗，与自然到期统一走末尾淡出。</summary>
        public void Stop(Token token)
        {
            if (token == null || token.Slot < 0 || token.Slot >= PoolSize)
                return;

            Slot slot = _slots[token.Slot];
            if (!slot.Active || slot.Generation != token.Generation)
                return;

            slot.Duration = Mathf.Min(slot.Duration, slot.Age + FadeOutSeconds);
            _slots[token.Slot] = slot;
        }

        /// <summary>桥接 Stop 用：实例已销毁（退出 Play）时静默丢弃。</summary>
        public static void StopSafe(Token token)
        {
            if (_instance != null)
                _instance.Stop(token);
        }

        void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            Camera cam = Camera.main;
            _cameraTransform = cam != null ? cam.transform : null;

            for (int i = 0; i < PoolSize; i++)
            {
                if (_slots[i].Active)
                    TickSlot(i, dt);
            }
        }

        void TickSlot(int i, float dt)
        {
            Slot slot = _slots[i];
            ICombatUnit unit = slot.Unit;
            if (unit == null || unit.IsDisposed)
            {
                // 宿主销毁（回池等异常路径）：当帧收掉；正常死亡已由 StopByEntity 先走淡出
                slot.Duration = Mathf.Min(slot.Duration, slot.Age);
            }
            else
            {
                slot.Go.transform.position = unit.Position + Vector3.up * slot.Height;
            }

            slot.Age += dt;
            float age = slot.Age;
            if (age >= slot.Duration)
            {
                slot.Active = false;
                slot.Unit = null;
                slot.Go.SetActive(false);
                _slots[i] = slot;
                return;
            }

            // 弹入 → 回稳 → 持留脉冲；透明度弹入淡入、末尾淡出
            float scale;
            float alpha;
            if (age < PopInSeconds)
            {
                float k = age / PopInSeconds;
                scale = Mathf.Lerp(0.55f, 1.18f, k);
                alpha = k;
            }
            else if (age < PopInSeconds + SettleSeconds)
            {
                scale = Mathf.Lerp(1.18f, 1f, (age - PopInSeconds) / SettleSeconds);
                alpha = 1f;
            }
            else
            {
                scale = 1f + Mathf.Sin(age * PulseSpeed) * PulseAmplitude;
                alpha = 1f;
            }

            float remain = slot.Duration - age;
            if (remain < FadeOutSeconds)
                alpha *= Mathf.Clamp01(remain / FadeOutSeconds);

            Transform t = slot.Go.transform;
            float s = scale * BaseScale;
            t.localScale = new Vector3(s, s, 1f);
            if (_cameraTransform != null)
                t.rotation = _cameraTransform.rotation;

            Color c = slot.Renderer.color;
            c.a = alpha;
            slot.Renderer.color = c;

            _slots[i] = slot;
        }

        /// <summary>
        /// 程序化四角星（ZZZ 闪光形状）：到最近坐标轴的距离小于臂宽即点亮，
        /// 臂宽随半径收窄形成尖角，中心叠一个亮核。纯白，染色走 SpriteRenderer.color。
        /// </summary>
        static Sprite GetOrCreateCrossSprite()
        {
            if (_crossSprite != null)
                return _crossSprite;

            const int size = 128;
            const float half = size * 0.5f;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "TelegraphCross",
            };

            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f - half) / half;
                    float v = (y + 0.5f - half) / half;
                    float r = Mathf.Sqrt(u * u + v * v);

                    float dAxis = Mathf.Min(Mathf.Abs(u), Mathf.Abs(v));
                    float arm = Mathf.Lerp(0.16f, 0.02f, Mathf.Clamp01(r));
                    float body = 1f - Mathf.InverseLerp(arm - 0.05f, arm + 0.05f, dAxis);
                    body *= 1f - Mathf.InverseLerp(0.82f, 1f, r);
                    float core = 1f - Mathf.InverseLerp(0.10f, 0.28f, r);

                    float a = Mathf.Clamp01(Mathf.Max(body, core));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            _crossSprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            _crossSprite.name = "TelegraphCross";
            return _crossSprite;
        }
    }
}
