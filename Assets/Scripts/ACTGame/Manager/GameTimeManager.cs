using System;
using UnityEngine;

namespace ACTGameEditor
{
    /// <summary>
    /// 时间层：不同系统使用不同时间缩放，UI 始终不受影响。
    /// </summary>
    public enum TimeLayer
    {
        /// <summary>世界：敌人、特效、物理、技能序列等。</summary>
        World,
        /// <summary>玩家：角色移动、输入、动画等。</summary>
        Player,
        /// <summary>相机：跟随、锁定等。</summary>
        Camera,
        /// <summary>UI：始终使用 unscaled，不受游戏时间影响。</summary>
        UI,
    }

    /// <summary>
    /// 自定义时间缩放管理器。不依赖 Time.timeScale，避免影响 UI 交互。
    /// 支持暂停、魔女时间、HitStop 等效果，各层可独立缩放。
    /// </summary>
    public static class GameTimeManager
    {
        private static float _worldScale = 1f;
        private static float _playerScale = 1f;
        private static float _cameraScale = 1f;

        private static float _worldDelta;
        private static float _playerDelta;
        private static float _cameraDelta;

        private static float _worldTime;
        private static float _playerTime;
        private static float _cameraTime;

        private static float _fixedWorldTime;

        /// <summary>时间流速变化时广播，便于动画、粒子等同步更新。仅在 scale 实际变化时触发。</summary>
        public static event Action OnTimeScaleChanged;

        /// <summary>世界层 scale，0=暂停，1=正常，魔女时间可设 0.3 等。</summary>
        public static float WorldScale
        {
            get => _worldScale;
            set
            {
                float v = Mathf.Max(0f, value);
                if (Mathf.Approximately(_worldScale, v)) return;
                _worldScale = v;
                OnTimeScaleChanged?.Invoke();
            }
        }

        /// <summary>玩家层 scale，魔女时间时可=1 使玩家相对加速。</summary>
        public static float PlayerScale
        {
            get => _playerScale;
            set
            {
                float v = Mathf.Max(0f, value);
                if (Mathf.Approximately(_playerScale, v)) return;
                _playerScale = v;
                OnTimeScaleChanged?.Invoke();
            }
        }

        /// <summary>相机层 scale。</summary>
        public static float CameraScale
        {
            get => _cameraScale;
            set
            {
                float v = Mathf.Max(0f, value);
                if (Mathf.Approximately(_cameraScale, v)) return;
                _cameraScale = v;
                OnTimeScaleChanged?.Invoke();
            }
        }

        /// <summary>世界是否暂停（WorldScale == 0）。</summary>
        public static bool IsWorldPaused => _worldScale <= 0f;

        /// <summary>本帧世界层 deltaTime。</summary>
        public static float WorldDelta => _worldDelta;

        /// <summary>本帧玩家层 deltaTime。</summary>
        public static float PlayerDelta => _playerDelta;

        /// <summary>本帧相机层 deltaTime。</summary>
        public static float CameraDelta => _cameraDelta;

        /// <summary>世界层累计时间。</summary>
        public static float WorldTime => _worldTime;

        /// <summary>玩家层累计时间。</summary>
        public static float PlayerTime => _playerTime;

        /// <summary>相机层累计时间。</summary>
        public static float CameraTime => _cameraTime;

        /// <summary>Fixed 模式下的世界累计时间（供 Flux AnimatePhysics 等使用）。</summary>
        public static float FixedWorldTime => _fixedWorldTime;

        /// <summary>
        /// 每帧 Update 开始时调用，更新各层 delta 和累计时间。
        /// 必须在其他使用 GameTimeManager 的系统之前调用。
        /// </summary>
        public static void Tick()
        {
            float rawDelta = Time.unscaledDeltaTime;
            _worldDelta = rawDelta * _worldScale;
            _playerDelta = rawDelta * _playerScale;
            _cameraDelta = rawDelta * _cameraScale;

            _worldTime += _worldDelta;
            _playerTime += _playerDelta;
            _cameraTime += _cameraDelta;
        }

        /// <summary>
        /// FixedUpdate 中调用，更新 Fixed 模式下的世界时间。
        /// </summary>
        public static void FixedTick()
        {
            float rawFixedDelta = Time.fixedDeltaTime;
            float step = rawFixedDelta * _worldScale;
            _fixedWorldTime += step;
        }

        /// <summary>按层获取本帧 deltaTime。</summary>
        public static float GetDelta(TimeLayer layer)
        {
            if (layer == TimeLayer.World) return _worldDelta;
            if (layer == TimeLayer.Player) return _playerDelta;
            if (layer == TimeLayer.Camera) return _cameraDelta;
            if (layer == TimeLayer.UI) return Time.unscaledDeltaTime;
            return _worldDelta;
        }

        /// <summary>按层获取 scale。</summary>
        public static float GetScale(TimeLayer layer)
        {
            if (layer == TimeLayer.World) return _worldScale;
            if (layer == TimeLayer.Player) return _playerScale;
            if (layer == TimeLayer.Camera) return _cameraScale;
            if (layer == TimeLayer.UI) return 1f;
            return _worldScale;
        }

        /// <summary>按层获取累计时间（Update 模式，基于 unscaledTime 与 scale 累积）。</summary>
        public static float GetTime(TimeLayer layer)
        {
            if (layer == TimeLayer.World) return _worldTime;
            if (layer == TimeLayer.Player) return _playerTime;
            if (layer == TimeLayer.Camera) return _cameraTime;
            if (layer == TimeLayer.UI) return Time.unscaledTime;
            return _worldTime;
        }

        /// <summary>获取经实体 scale 缩放后的本帧 deltaTime（用于按实体时间流速计算）。</summary>
        /// <param name="entityScale">实体 GetTimeScale() 返回值，1 表示正常。</param>
        public static float GetScaledDelta(float entityScale) => _worldDelta * Mathf.Max(0f, entityScale);

        /// <summary>获取经实体 scale 缩放后的 Fixed delta（用于 FixedUpdate 中按实体时间流速计算）。</summary>
        public static float GetScaledFixedDelta(float entityScale) =>
            Time.fixedDeltaTime * _worldScale * Mathf.Max(0f, entityScale);

        /// <summary>重置所有 scale 为 1（直接写，供 TimeScaleEffectManager 无效果时使用）。</summary>
        public static void ResetAllScales()
        {
            bool changed = !Mathf.Approximately(_worldScale, 1f) || !Mathf.Approximately(_playerScale, 1f) || !Mathf.Approximately(_cameraScale, 1f);
            _worldScale = 1f;
            _playerScale = 1f;
            _cameraScale = 1f;
            if (changed)
                OnTimeScaleChanged?.Invoke();
        }

        /// <summary>暂停：世界、玩家、相机均停止。效果计时冻结，符合崩坏3表现。</summary>
        public static void Pause()
        {
            TimeScaleEffectManager.AddPause();
        }

        /// <summary>恢复：移除暂停效果。</summary>
        public static void Resume()
        {
            TimeScaleEffectManager.RemovePause();
        }
    }
}
