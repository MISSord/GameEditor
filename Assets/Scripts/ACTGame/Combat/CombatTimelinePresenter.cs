using EGamePlay;
using EGamePlay.Combat;
using EGamePlay.Unity;
using UnityEngine;

namespace ACTGameEditor.Combat
{
    /// <summary>技能时间轴消息的表现落地：移动/转向/渲染/时停/音频等。</summary>
    public sealed class CombatTimelinePresenter : EGamePlay.Component, ICombatTimelinePresenter
    {
        CombatEntity _owner;
        AudioSource _audioSource;
        Renderer[] _cachedRenderers;
        long _unmoveTimerId;

        public override void Awake()
        {
            _owner = GetEntity<CombatEntity>();
#if UNITY
            Transform audioRoot = _owner.RootTransform != null ? _owner.RootTransform : _owner.ModelTrans;
            if (audioRoot != null)
            {
                _audioSource = audioRoot.GetComponent<AudioSource>();
                if (_audioSource == null)
                    _audioSource = audioRoot.gameObject.AddComponent<AudioSource>();
            }
#endif
        }

        public override void OnDestroy()
        {
            CancelUnmoveTimer();
            _cachedRenderers = null;
            _audioSource = null;
            _owner = null;
        }

        /// <inheritdoc/>
        public void ApplyPresentationMessage(string msgName, float floatMsg, bool boolMsg, string strMsg = null)
        {
            if (_owner == null || string.IsNullOrEmpty(msgName))
                return;

#if UNITY
            switch (msgName)
            {
                case var _ when msgName == PlayEventMsg.SetNoGravityT:
                    _owner.GetComponent<AnimComponent>()?.Motion?.SuppressGravityFor(floatMsg);
                    break;

                case var _ when msgName == PlayEventMsg.SetCanMove:
                    _owner.ChangeInputMoveState(boolMsg);
                    break;

                case var _ when msgName == PlayEventMsg.SetCanRotate:
                    _owner.ChangeInputRotateState(boolMsg);
                    break;

                case var _ when msgName == PlayEventMsg.SetUnMoveTime:
                    ApplyUnmoveForSeconds(floatMsg);
                    break;

                case var _ when msgName == PlayEventMsg.ActivePlayerRender:
                    ApplyRenderActive(boolMsg);
                    break;

                case var _ when msgName == PlayEventMsg.TimeStop:
                    ApplyTimeStop(floatMsg);
                    break;

                case var _ when msgName == PlayEventMsg.PlayAudio:
                    PlayTimelineAudio(strMsg, floatMsg);
                    break;
            }
#endif
        }

#if UNITY
        void ApplyUnmoveForSeconds(float durationSeconds)
        {
            _owner.ChangeInputMoveState(false);
            CancelUnmoveTimer();
            if (durationSeconds <= 0f)
                return;

            long tillMs = TimeHelper.ClientNow() + (long)(durationSeconds * 1000f);
            _unmoveTimerId = ETTimerManager.Instance.NewOnceTimer(tillMs, () =>
            {
                _unmoveTimerId = 0;
                _owner?.ChangeInputMoveState(true);
            });
        }

        void CancelUnmoveTimer()
        {
            if (_unmoveTimerId == 0)
                return;
            ETTimerManager.Instance?.Remove(_unmoveTimerId);
            _unmoveTimerId = 0;
        }

        void ApplyRenderActive(bool active)
        {
            if (_cachedRenderers == null || _cachedRenderers.Length == 0)
            {
                Transform root = _owner.ModelTrans != null ? _owner.ModelTrans : _owner.RootTransform;
                _cachedRenderers = root != null ? root.GetComponentsInChildren<Renderer>(true) : null;
            }

            if (_cachedRenderers == null)
                return;

            for (int i = 0; i < _cachedRenderers.Length; i++)
            {
                if (_cachedRenderers[i] != null)
                    _cachedRenderers[i].enabled = active;
            }
        }

        void ApplyTimeStop(float durationSeconds)
        {
            if (durationSeconds <= 0f)
                return;

            TimeScaleEffectManager.AddEffect(
                TimeScaleEffectType.SkillTimescale,
                0f,
                1f,
                1f,
                durationSeconds,
                20);
        }

        void PlayTimelineAudio(string audioId, float volume)
        {
            if (string.IsNullOrEmpty(audioId) || _audioSource == null)
                return;

            var pool = RunTimePoolManager.Instance;
            if (pool == null)
                return;

            AudioClip clip = pool.GetAudioClip(audioId, isHit: false);
            if (clip == null)
                return;

            float vol = volume > 0f ? volume : 1f;
            _audioSource.PlayOneShot(clip, vol);
        }
#endif
    }
}
