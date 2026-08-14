using UnityEngine;

namespace Flux
{
	[FEvent("GamoObject/FObjectEvent", typeof(FTrack))]
	public class FObjectEvent : FEvent
	{
        [SerializeField]
        private bool _active = true;

        public GameObject _ownerGO = null;

        protected override void OnTrigger(float timeSinceTrigger)
        {
            if (_ownerGO == null)
            {
                _ownerGO = Owner.gameObject;
            }
        }

        protected override void OnUpdateEvent(float timeSinceTrigger)
        {
            if (_ownerGO.activeSelf != _active)
            {
                _ownerGO.SetActive(_active);
            }
        }

        protected override void OnFinish()
        {
            _ownerGO.SetActive(!_active);
        }

    }
}

