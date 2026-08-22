using UnityEngine;
using System.Collections;

namespace Flux
{
	/**
	 * @brief Base of all the classes that create a sequence.
	 * @sa CTimeline, CTrack, CEvent
	 */
	public abstract class FObject : MonoBehaviour
	{
		/**
		 * @brief id usually reference to the index of this element
		 * relative to it's parent, e.g. index of the timeline in the sequence,
		 * index of the track in the timeline, or of the event in the track.
		 */
		[SerializeField]
		[HideInInspector]
		private int _id = -1;

		/// @brief _id inspector
		public int GetId(){ return _id; }

		/**
		 * @brief Sets _id. It is used when the element is moved in a list to update it's position.
		 */
		internal void SetId( int id ) { _id = id; }

		/// @brief Sequence this flux object belongs to
		public abstract FSequence Sequence { get; }

		/// @brief To whom does this object belong to?
		public abstract Transform Owner { get; }

		/// @brief Called on the initialization step, use this to setup code that may be more intensive.
		/// @note It is only called once, either manually or when a sequence is played.
		/// @note It is called left to right, earlier events get called first.
		public abstract void Init();

		/// @brief Called when you stop the sequence. 
		/// @note It is called when the sequence is stopped, i.e. resetted.
		/// @note It is called right to left, later events get called first.
		public abstract void Stop();

		/// <summary>
		/// Rebuild 会从子节点找回对象后再 SetParent，父节点通常已经正确。
		/// 对 Prefab Asset 再赋值 parent 会触发 Unity 报错，因此仅在真正需要改层级时才设置。
		/// </summary>
		internal void SetParentIfNeeded(Transform newParent)
		{
			if (transform.parent == newParent)
				return;

#if UNITY_EDITOR
			if (!Application.isPlaying && UnityEditor.PrefabUtility.IsPartOfPrefabAsset(this)
				&& UnityEditor.SceneManagement.PrefabStageUtility.GetPrefabStage(gameObject) == null)
				return;
#endif
			transform.parent = newParent;
		}
	}
}
