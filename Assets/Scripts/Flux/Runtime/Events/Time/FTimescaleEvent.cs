using UnityEngine;
using ACTGameEditor;

namespace Flux
{
	//[FEvent("Time/Timescale")]
	public class FTimescaleEvent : FEvent {

		[SerializeField]
		private AnimationCurve _curve;
		public AnimationCurve Curve { get { return _curve; } set { _curve = value; } }

		[SerializeField]
		[Tooltip("Remove skill timescale effect at the end?")]
		private bool _clearOnFinish = true;
		public bool ClearOnFinish { get { return _clearOnFinish; } set { _clearOnFinish = value; } }

		private TimeScaleEffect _effect;

		protected override void SetDefaultValues ()
		{
			_curve = new AnimationCurve( new Keyframe[]{ new Keyframe(0, 1) } );
		}

		protected override void OnFrameRangeChanged( FrameRange oldFrameRange )
		{
			if( oldFrameRange.Length != FrameRange.Length )
			{
				FUtility.ResizeAnimationCurve( _curve, FrameRange.Length * Sequence.InverseFrameRate );
			}
		}

		protected override void OnTrigger( float timeSinceTrigger )
		{
			float s = Mathf.Clamp(_curve.Evaluate(timeSinceTrigger), 0f, 100f);
			_effect = TimeScaleEffectManager.AddEffect(TimeScaleEffectType.SkillTimescale, s, 1f, 1f, LengthTime + 0.1f, 20);
		}

		protected override void OnUpdateEvent( float timeSinceTrigger )
		{
			if (_effect != null)
				_effect.WorldScale = Mathf.Clamp(_curve.Evaluate(timeSinceTrigger), 0f, 100f);
		}

		protected override void OnStop()
		{
			RemoveEffect();
		}

		protected override void OnFinish()
		{
			if (_clearOnFinish)
				RemoveEffect();
		}

		private void RemoveEffect()
		{
			if (_effect != null)
			{
				TimeScaleEffectManager.RemoveEffect(_effect);
				_effect = null;
			}
		}
	}
}