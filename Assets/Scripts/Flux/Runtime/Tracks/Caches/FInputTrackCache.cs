//#define FLUX_DEBUG
using UnityEngine;
using System.Collections.Generic;

namespace Flux
{
	/// @brief Cache for FSequenceTrack
	public class FInputTrackCache : FTrackCache {

		public FInputTrackCache(FInputTrack track )
			:base( track )
		{
		}

		protected override bool BuildInternal()
		{
#if FLUX_DEBUG
			Debug.LogWarning("Creating Sequence Preview");
#endif

			return true;
		}

		protected override bool ClearInternal()
		{
#if FLUX_DEBUG
			Debug.LogWarning("Destroying Sequence Preview");
#endif

			return true;
		}

		public override void GetPlaybackAt( float sequenceTime )
		{
            if (Input.anyKeyDown)
            {
#if FLUX_DEBUG
                Debug.Log("yns  anyKeyDown ");
#endif
            }
            if (Event.current != null)
            {
#if FLUX_DEBUG
                Debug.Log("yns  " + Event.current.keyCode);
#endif
            }

			Track.UpdateEventsEditor( (int)(sequenceTime * Track.Sequence.FrameRate), sequenceTime );
		}
	}
}
