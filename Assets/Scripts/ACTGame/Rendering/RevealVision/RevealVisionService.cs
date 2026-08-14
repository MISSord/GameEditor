using System;
using System.Collections.Generic;
using UnityEngine;

namespace ACTGameEditor
{
    /// <summary>
    /// 显现系统注册表 / 频道配置。不再“一开全图可见”，只做登记与频道过滤。
    /// </summary>
    public sealed class RevealVisionService : MonoBehaviour
    {
        static RevealVisionService _instance;
        static readonly List<RevealVisionSubject> Subjects = new(64);

        /// <summary>全局单例（场景中不存在时自动创建）。</summary>
        public static RevealVisionService Instance
        {
            get
            {
                if (_instance != null)
                    return _instance;

                _instance = FindObjectOfType<RevealVisionService>();
                if (_instance != null)
                    return _instance;

                var go = new GameObject(nameof(RevealVisionService));
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<RevealVisionService>();
                return _instance;
            }
        }

        [SerializeField]
        RevealChannel activeChannels = RevealChannel.All;

        /// <summary>当前允许显现的频道。</summary>
        public RevealChannel ActiveChannels => activeChannels;

        /// <summary>频道变化。</summary>
        public event Action<RevealChannel> OnChannelsChanged;

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
        }

        void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        /// <summary>设置允许频道。</summary>
        public void SetChannels(RevealChannel channels)
        {
            if (activeChannels == channels)
                return;

            activeChannels = channels;
            OnChannelsChanged?.Invoke(activeChannels);
        }

        /// <summary>频道是否允许。</summary>
        public bool IsChannelAllowed(RevealChannel channel)
        {
            if (channel == RevealChannel.None)
                return false;
            return (activeChannels & channel) != 0;
        }

        /// <summary>登记显现对象。</summary>
        public static void Register(RevealVisionSubject subject)
        {
            if (subject == null || Subjects.Contains(subject))
                return;
            Subjects.Add(subject);
        }

        /// <summary>取消登记。</summary>
        public static void Unregister(RevealVisionSubject subject)
        {
            Subjects.Remove(subject);
        }

        /// <summary>
        /// 收集球范围内、且频道匹配的对象（无 GC：写入 results）。
        /// </summary>
        public static void CollectInRadius(
            Vector3 center,
            float radius,
            RevealChannel channelMask,
            List<RevealVisionSubject> results)
        {
            results.Clear();
            float r2 = radius * radius;
            for (int i = Subjects.Count - 1; i >= 0; i--)
            {
                RevealVisionSubject s = Subjects[i];
                if (s == null)
                {
                    Subjects.RemoveAt(i);
                    continue;
                }

                if ((s.Channel & channelMask) == 0)
                    continue;

                Vector3 p = s.WorldPosition;
                if ((p - center).sqrMagnitude <= r2)
                    results.Add(s);
            }
        }

        /// <summary>
        /// 收集圆锥范围内对象（无 GC）。angleDegrees 为全锥角。
        /// </summary>
        public static void CollectInCone(
            Vector3 origin,
            Vector3 direction,
            float range,
            float angleDegrees,
            RevealChannel channelMask,
            List<RevealVisionSubject> results)
        {
            results.Clear();
            Vector3 dir = direction.sqrMagnitude > 1e-6f ? direction.normalized : Vector3.forward;
            float cosOuter = Mathf.Cos(Mathf.Max(0.1f, angleDegrees * 0.5f) * Mathf.Deg2Rad);
            float rangeSq = range * range;

            for (int i = Subjects.Count - 1; i >= 0; i--)
            {
                RevealVisionSubject s = Subjects[i];
                if (s == null)
                {
                    Subjects.RemoveAt(i);
                    continue;
                }

                if ((s.Channel & channelMask) == 0)
                    continue;

                Vector3 to = s.WorldPosition - origin;
                float distSq = to.sqrMagnitude;
                if (distSq > rangeSq)
                    continue;

                if (distSq <= 1e-8f)
                {
                    results.Add(s);
                    continue;
                }

                float nd = Vector3.Dot(to.normalized, dir);
                if (nd >= cosOuter)
                    results.Add(s);
            }
        }
    }
}
