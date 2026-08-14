using UnityEngine;

namespace EGamePlay.Combat
{
    public enum AnimationEnem
    {
        Idle,
        Run,
        Jump,
        Attack,
        Skill1,
        Skill2,
        Skill3,
        Stun,
        Damage,
        Dead,
        Walk,
    }

    public class AnimComponent : Component
    {
        public Animator animator { get; private set; }

        public override void Awake(object initData)
        {
            GameObjectData data = (GameObjectData)initData;
            animator = data.animator;
        }

        public override void OnDestroy()
        {
            animator = null;
        }

        /// <summary>
        /// 按照固定时间长度进行混合
        /// </summary>
        /// <param name="name">动画名字</param>
        /// <param name="time">混合百分比</param>
        /// <param name="layer">图层</param>
        /// <param name="fixedTimeOffset">从哪里开始播放（单位为秒）</param>
        public void PlayFadeInFixedTime(int nameHash, float time = 0.2f, int layer = -1, float fixedTimeOffset = 0.0f)
        {
            //还有个参数为normalizedTransitionTime，这个是指从哪里开始过渡
            //如0.5，则当开始过渡时，会直接从过渡的50%开始过渡，这个参数未来研究看看
            animator?.CrossFadeInFixedTime(nameHash, time, layer, fixedTimeOffset);
        }


        /// <summary>
        /// 按照动画长度百分比进行混合
        /// </summary>
        /// <param name="name">动画名字</param>
        /// <param name="time">混合百分比</param>
        /// <param name="layer">图层</param>
        /// <param name="fixedTimeOffset">从哪里开始播放（单位为秒）</param>
        public void PlayFade(int nameHash, float time = 0.2f, int layer = -1, float fixedTimeOffset = 0.0f)
        {
            animator.CrossFade(nameHash, time, layer, fixedTimeOffset);
        }

        /// <summary>
        /// 调整动画播放速度
        /// </summary>
        /// <param name="name"></param>
        /// <param name="Speed"></param>
        /// <param name="time"></param>
        public void PlayFadeChangeSpeed(string name, float Speed)
        {
            //这个要与动画器中的参数配合
            string nameSpeed = name + "Speed";
            animator?.SetFloat(nameSpeed, Speed);
        }
    }
}
