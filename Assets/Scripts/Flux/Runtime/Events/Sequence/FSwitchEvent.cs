using UnityEngine;
using EGamePlay.Combat;

namespace Flux
{
    [FEvent("Sequence/FSwitchEvent", typeof(FInputTrack))]
    public class FSwitchEvent : FEvent
    {
        //[SerializeField]
        //[HideInInspector]
        //private int _toFrame;
        //public int ToFrame { get => _toFrame; set => _toFrame = value; }

        //[SerializeField]
        //[HideInInspector]
        //private int switchFrame;
        //public int SwitchFrame { get => switchFrame; set => switchFrame = value; }    
        
        [HideInInspector]
        [SerializeField]
        private int unMoveFrames;
        public int UnMoveFrames { get => unMoveFrames; set => unMoveFrames = value; }

        public EventTriggerType InputType = EventTriggerType.Finish;
 
        public override string Text
        {
            get
            {
                if( InputType == EventTriggerType.Finish)
                {
                    return InputType.ToString() + UnMoveFrames.ToString();
                }
                else
                {
                    return InputType.ToString();
                }
            }
        }
    }
}
