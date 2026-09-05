using ACTGameEditor.Combat;
using EGamePlay.Combat;
using UnityEngine;

namespace ACTGameEditor
{
    /// <summary>
    /// 把宿主战斗钟写到模型上的 ParticleSystem.simulationSpeed。
    /// 冻结时为 0；时空断裂时跟世界/玩家层。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CombatParticleClockDriver : MonoBehaviour
    {
        [SerializeField]
        Transform modelRoot;

        CombatEntity _owner;
        ParticleSystem[] _cached;
        float _lastSpeed = float.NaN;

        /// <summary>绑定战斗实体并刷新粒子缓存。</summary>
        public void Bind(CombatEntity owner, Transform root = null)
        {
            _owner = owner;
            if (root != null)
                modelRoot = root;
            _cached = null;
            _lastSpeed = float.NaN;
        }

        void LateUpdate()
        {
            if (_owner == null || _owner.IsDisposed)
                return;

            float speed = CombatTimeClock.GetSimulationSpeed(_owner);
            if (_cached != null && Mathf.Abs(speed - _lastSpeed) <= 0.0001f)
                return;

            _lastSpeed = speed;
            EnsureCache();
            if (_cached == null)
                return;

            for (int i = 0; i < _cached.Length; i++)
                CombatTimeClock.ApplySimulationSpeed(_cached[i], speed);
        }

        void EnsureCache()
        {
            if (_cached != null)
                return;
            Transform root = modelRoot != null ? modelRoot : transform;
            _cached = root.GetComponentsInChildren<ParticleSystem>(true);
        }
    }
}
