using UnityEngine;

namespace ACTGameEditor.Combat
{
    /// <summary>???????????? EGamePlayInit ???</summary>
    [CreateAssetMenu(fileName = "CombatFxPreset", menuName = "ACTGame/Combat Fx Preset", order = 101)]
    public sealed class CombatFxPreset : ScriptableObject
    {
        [Header("Hit Flash")]
        [Tooltip("Hit flash duration on target.")]
        public float HitFlashDuration = 0.12f;

        [Header("Hit Stop (local attacker)")]
        [Tooltip("HitStop duration.")]
        public float HitStopDuration = 0.08f;

        [Tooltip("HitStop world TimeScale.")]
        [Range(0f, 1f)]
        public float HitStopWorldScale = 0.08f;

        [Tooltip("Play camera impact on HitStop.")]
        public bool HitStopCameraImpact = true;

        [Header("Time Fracture")]
        [Tooltip("TimeFracture world TimeScale.")]
        [Range(0f, 1f)]
        public float TimeFractureWorldScale = 0.3f;

        static CombatFxPreset _active;

        public static CombatFxPreset Active => _active != null ? _active : _active = CreateInstance<CombatFxPreset>();

        public static void SetActive(CombatFxPreset preset) => _active = preset;
    }
}
