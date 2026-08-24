using System;
using UnityEngine;

namespace Flux
{
    /// <summary>
    /// Editor-only hook so runtime animation events can resolve preview controllers
    /// without referencing editor assemblies.
    /// </summary>
    public static class FAnimationPreviewBridge
    {
#if UNITY_EDITOR
        public static Func<RuntimeAnimatorController, RuntimeAnimatorController> ResolvePreviewController;
#endif

        /// <summary>
        /// Returns the controller that should drive editor preview playback.
        /// </summary>
        public static RuntimeAnimatorController GetPreviewController(RuntimeAnimatorController source)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying && source != null && ResolvePreviewController != null)
            {
                RuntimeAnimatorController preview = ResolvePreviewController(source);
                if (preview != null)
                    return preview;
            }
#endif
            return source;
        }
    }
}
