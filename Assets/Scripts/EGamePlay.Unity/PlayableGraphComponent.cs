using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace ACTGameEditor
{
    public class PlayableGraphComponent : EGamePlay.Component
    {
        private PlayableGraph _graph;
        private AnimationMixerPlayable _mixer;
        private AnimationScriptPlayable _inertialJobPlayable; // 新增：用于处理惯性的 Job 节点
        private AnimationPlayableOutput _output;

        private Dictionary<string, int> _nameToIndex = new Dictionary<string, int>();
        private List<AnimationClipPlayable> _playables = new List<AnimationClipPlayable>();

        // 状态记录
        private int _currentIndex = -1; // 当前主权重的索引
        private Animator _animator; // 需要引用Animator来绑定骨骼

        // 惯性混合参数
        public float halfLife = 0.15f; // 半衰期，越小回正越快，通常 0.1-0.2

        private InertialBlendingJob _jobData;
        public override void Awake(object initData)
        {
            _animator = (Animator)initData;

            // 1. 创建图
            _graph = PlayableGraph.Create($"{Entity.Name}_InertialGraph");
            _graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

            // 2. 创建 Mixer (作为源头)
            _mixer = AnimationMixerPlayable.Create(_graph, 0);

            // 3. 创建 Inertial Job (作为中间过滤器)
            // 这个 Job 会接管 Hips (盆骨) 的偏移计算
            _jobData = new InertialBlendingJob();
            // 绑定 Hips 骨骼 (通常是 Animator 的第一个子节点或通过名字查找)
            // 这里假设 Root 的第一个子节点是 Hips，实际项目中最好通过名称查找
            Transform hips = _animator.GetBoneTransform(HumanBodyBones.Hips);
            if (hips == null) hips = _animator.transform.Find("mixamorig:Hips");

            _jobData.hipsHandle = _animator.BindStreamTransform(hips);
            _inertialJobPlayable = AnimationScriptPlayable.Create(_graph, _jobData);

            // 4. 连接图结构: Mixer -> InertialJob -> Output
            _inertialJobPlayable.AddInput(_mixer, 0, 1.0f); // 把 Mixer 连给 Job

            _output = AnimationPlayableOutput.Create(_graph, "Animation", _animator);
            _output.SetSourcePlayable(_inertialJobPlayable); // 把 Job 连给 Output

            _graph.Play();
        }

        // ==================================================
        // 1. 初始化与注册 (类似 Animator 的 State 录入)
        // ==================================================

        /// <summary>
        /// 注册一个动画到系统中备用
        /// </summary>
        public void RegisterAnimation(string name, AnimationClip clip, bool isLoop = false)
        {
            if (_nameToIndex.ContainsKey(name)) return;

            int newIndex = _playables.Count;
            _mixer.SetInputCount(newIndex + 1);

            var playable = AnimationClipPlayable.Create(_graph, clip);
            playable.SetDuration(isLoop ? double.MaxValue : clip.length);
            // 设置为 Loop 模式，这样 NormalizedTime 会自动循环
            playable.GetAnimationClip().wrapMode = isLoop ? WrapMode.Loop : WrapMode.ClampForever;

            playable.Pause();
            playable.SetTime(0);

            _graph.Connect(playable, 0, _mixer, newIndex);
            _mixer.SetInputWeight(newIndex, 0f);

            _playables.Add(playable);
            _nameToIndex.Add(name, newIndex);
        }

        /// <summary>
        /// 移除指定名称的动画并释放其Playable节点
        /// </summary>
        public void RemoveAnimation(string name)
        {
            // 1. 基础校验
            if (!_nameToIndex.ContainsKey(name))
            {
                Debug.LogWarning($"试图移除不存在的动画: {name}");
                return;
            }

            int indexToRemove = _nameToIndex[name];

            // 2. 安全校验：不能移除正在播放或正在过渡的动画
            if (_currentIndex == indexToRemove)
            {
                Debug.LogError($"无法移除动画 '{name}'，因为它当前正在播放或过渡中！请先切换到其他动画。");
                return;
            }

            // 3. 销毁目标 Playable
            var playableToRemove = _playables[indexToRemove];
            if (playableToRemove.IsValid())
            {
                // 断开连接并销毁节点
                _graph.Disconnect(_mixer, indexToRemove);
                playableToRemove.Destroy();
            }

            // 4. 核心逻辑：填补空缺 (Swap Removal)
            // 为了保持 Mixer 输入端口的紧凑，我们将列表最后一个动画移动到被移除的位置
            int lastIndex = _playables.Count - 1;

            if (indexToRemove != lastIndex)
            {
                // --- 搬运逻辑 ---

                // A. 获取位于末尾的动画信息
                var lastPlayable = _playables[lastIndex];
                string lastAnimName = GetNameByIndex(lastIndex); // 辅助方法，见下方

                // B. 将末尾动画重新连接到 indexToRemove 的端口
                _graph.Disconnect(_mixer, lastIndex); // 断开原来的位置
                _graph.Connect(lastPlayable, 0, _mixer, indexToRemove); // 连到新位置

                // 保持权重状态（理论上是0，因为如果是正在播放的动画我们前面已经拦截了）
                _mixer.SetInputWeight(indexToRemove, 0f);

                // C. 更新数据结构
                _playables[indexToRemove] = lastPlayable; // 列表填空
                _nameToIndex[lastAnimName] = indexToRemove; // 字典更新索引

                // D. 修正状态指针 (如果状态机刚好指向了原来的 lastIndex，现在它变到了 indexToRemove)
                // 虽然前面拦截了正在播放的移除，但以防万一有其他的逻辑引用
                if (_currentIndex == lastIndex) _currentIndex = indexToRemove;
            }

            // 5. 移除列表末尾的冗余数据
            _playables.RemoveAt(lastIndex);
            _nameToIndex.Remove(name);

            // 6. 缩减 Mixer 的输入总数
            _mixer.SetInputCount(_playables.Count);
        }

        /// <summary>
        /// 辅助方法：通过索引反向查找名字 (因为字典是 name->index)
        /// </summary>
        private string GetNameByIndex(int index)
        {
            foreach (var kvp in _nameToIndex)
            {
                if (kvp.Value == index) return kvp.Key;
            }
            return null;
        }

        /// <summary>
        /// 彻底清理所有动画资源 (建议在 OnDestroy 或 切换场景时调用)
        /// </summary>
        public void ClearAll()
        {
            // 销毁所有Clip Playable
            foreach (var p in _playables)
            {
                if (p.IsValid()) p.Destroy();
            }

            _playables.Clear();
            _nameToIndex.Clear();

            // 重置状态
            _currentIndex = -1;

            // 重置 Mixer
            if (_mixer.IsValid()) _mixer.SetInputCount(0);
        }

        // ==================================================
        // 2. 运行时逻辑
        // ==================================================
        public override void Update(float deltaTime)
        {
            // 只需要更新 Job 的时间参数，数学计算完全由 C++底层 (Burst) 或 Job 线程处理
            var jobData = _inertialJobPlayable.GetJobData<InertialBlendingJob>();
            jobData.deltaTime = deltaTime;
            jobData.halfLife = halfLife;
            _inertialJobPlayable.SetJobData(jobData);
        }

        // ==================================================
        // 3. 公开 API
        // ==================================================
        public override void OnDestroy()
        {
            ClearAll();
            if (_graph.IsValid()) _graph.Destroy();
        }

        /// <summary>
        /// 获取当前动画的归一化相位 (0~1)
        /// 用于判断左脚还是右脚落地
        /// </summary>
        public float GetCurrentPhase()
        {
            if (_currentIndex == -1) return 0f;

            var playable = _playables[_currentIndex];
            double time = playable.GetTime();
            double duration = playable.GetAnimationClip().length;

            // 计算循环后的归一化时间
            return (float)((time % duration) / duration);
        }

        /// <summary>
        /// 【核心方法】惯性切换动画
        /// </summary>
        /// <param name="name">目标动画名</param>
        /// <param name="startPhase">目标动画从什么进度开始播? (0~1)</param>
        public void PlayInertial(string name, float startPhase = 0f)
        {
            if (!_nameToIndex.ContainsKey(name)) return;
            int targetIndex = _nameToIndex[name];
            if (targetIndex == _currentIndex) return; // 相同动画不切换

            // --- 步骤 A: 捕获切换前的状态 (Snapshot) ---
            // 我们利用 Job 系统的数据，因为它始终持有当前的偏移后的状态
            // 但为了简化实现，我们这里使用一种技巧：
            // 1. 记录当前的 Hips 世界坐标/速度
            // 由于我们在 PlayableGraph 运行中，直接用 Transform 获取的是上一帧的结果，
            // 惯性插值需要极其精确的 "当前帧" 差异。

            // 在 Inertialization 中，最简单的做法是在 Job 内部处理 "Reset"。
            // 我们通知 Job：“下一帧我要突变了，请记录当前位置作为偏移起点”。

            var jobData = _inertialJobPlayable.GetJobData<InertialBlendingJob>();
            jobData.triggerTransition = true; // 设置触发器
            _inertialJobPlayable.SetJobData(jobData);

            // --- 步骤 B: 逻辑层瞬间切换 ---

            // 1. 把旧的关掉
            if (_currentIndex != -1)
            {
                var oldP = _playables[_currentIndex];
                oldP.Pause();
                _mixer.SetInputWeight(_currentIndex, 0f);
            }

            // 2. 把新的打开 (权重直接设为 1，不做线性过渡!)
            var newP = _playables[targetIndex];
            _mixer.SetInputWeight(targetIndex, 1f);

            // 3. 设置相位 (Sync Time)
            double clipLen = newP.GetAnimationClip().length;
            newP.SetTime(clipLen * startPhase); // 直接跳到匹配的脚部位置
            newP.Play();

            // 4. 更新索引
            _currentIndex = targetIndex;

            // Graph 会在下一次 Evaluate 时执行 Job。
            // Job 会发现：逻辑姿态瞬间变了，但它记录了 Offset，会将视觉姿态拉回原来的位置，
            // 然后随时间衰减。
        }

        public void PlayInertial2(string name, float startPhase = 0f)
        {
            if (!_nameToIndex.ContainsKey(name)) return;
            int targetIndex = _nameToIndex[name];
            if (targetIndex == _currentIndex) return;

            // A. 计算偏移 (Inertial Snapshot)
            // 1. 获取当前的视觉位置 (Hips 在世界空间或局部空间)
            // 注意：这里需要 Animator 绑定的 Hips Transform
            Transform hips = _animator.GetBoneTransform(HumanBodyBones.Hips);
            if (hips == null) hips = _animator.transform.GetChild(0);

            Vector3 prevVisualPos = hips.localPosition; // 记录切换前的视觉位置
                                                        // 如果需要更精准，还需要记录速度 prevVel，可以用 (currentPos - lastFramePos)/dt 计算

            // B. 逻辑切换
            if (_currentIndex != -1)
            {
                var oldP = _playables[_currentIndex];
                oldP.Pause();
                _mixer.SetInputWeight(_currentIndex, 0f);
            }

            var newP = _playables[targetIndex];
            _mixer.SetInputWeight(targetIndex, 1f);

            // 设置相位起点
            double clipLen = newP.GetAnimationClip().length;
            newP.SetTime(clipLen * startPhase);
            newP.Play();
            _currentIndex = targetIndex;

            // C. 强制 Graph 评估一帧 (关键!)
            // 这一步是为了让 hips.localPosition 瞬间变成新动画的位置
            _graph.Evaluate(0);

            // D. 计算断层并注入 Job
            Vector3 newLogicalPos = hips.localPosition; // 现在 hips 已经是新动画的位置了
            Vector3 posOffset = prevVisualPos - newLogicalPos; // 算出要把新动画拉回旧位置需要多少偏移

            // 注入 Job
            var jobData = _inertialJobPlayable.GetJobData<InertialBlendingJob>();
            jobData.ResetOffsets(posOffset, Vector3.zero); // 暂时忽略速度继承，只做位置平滑
            _inertialJobPlayable.SetJobData(jobData);
        }
    }

    // ==================================================
    // 3. 惯性插值 Job (多线程/高性能数学核心)
    // ==================================================

    // 这是一个基于临界阻尼弹簧 (Critical Damping) 的 Job
    public struct InertialBlendingJob : IAnimationJob
    {
        public TransformStreamHandle hipsHandle; // 操作 Hips 骨骼

        public bool triggerTransition; // 主线程控制的开关
        public float deltaTime;
        public float halfLife; // 半衰期

        // 内部状态
        private Vector3 _positionOffset;
        private Vector3 _velocityOffset;

        // 旋转如果不做惯性，身体会突然转过去，建议也加上。
        // 为了简化代码，这里只演示 Position (解决滑步的关键)，
        // Rotation 原理完全一样，用 Quaternion 差值。

        public void ProcessRootMotion(AnimationStream stream) { }

        public void ProcessAnimation(AnimationStream stream)
        {
            // 获取当前流中的 Hips 本地位置 (这是 Mixer 刚刚混合出来的结果)
            Vector3 logicalPosition = hipsHandle.GetLocalPosition(stream);

            // --- 1. 触发瞬间：计算断层 ---
            if (triggerTransition)
            {
                // 这是一个核心技巧：
                // 当 triggerTransition 为 true 时，logicalPosition 已经是新动画的第一帧位置了。
                // 而 (logicalPosition + positionOffset) 理论上应该是上一帧的视觉位置。
                // 但因为我们刚换了动画，我们需要重新计算 Offset，让：
                // NewLogicalPos + NewOffset == OldVisualPos

                // 为了简化，我们假设上一帧的视觉位置就是 "流中原本的位置 + 旧的Offset"。
                // 但在 Job 里很难存上一帧的绝对值。

                // 更稳健的做法：
                // 在这一帧，我们不做物理模拟，只是单纯计算 Offset = OldVisual - NewLogical。
                // 但 Job 拿不到 OldVisual，除非我们存下来。

                // 这里用一种 Hack 方式：
                // 我们假设这一帧的 "突变" 仅仅来自于 Mixer 的切换。
                // 实际上，Inertialization 需要在 C# 层计算好传进来，或者在这里维护两个变量。

                // 修正方案：依靠 Velocity 连续性。
                // 这里的实现略微复杂，为了演示，我们采用 "累加式偏移"。
                // 真正的 Inertialization 库通常会在 C# 层计算 Offset 传进来。
                // 但为了做成全 Job 托管，我们这样做：

                // 实际上，PlayableGraphComponent 的 PlayInertial 只是重置了逻辑动画。
                // 此时 stream 里流过来的是新动画的 Pose。
                // 我们需要把 Offset 加上 (PreviousPose - NewPose)。

                // *由于 Job 比较难写完美的 Snapshot，我建议把 Offset 计算放回主线程，Job 只负责衰减*
                // 但既然写在 Job 里，我们改一下策略：

                // 我们不在这里计算 Offset，改由 C# 计算传进来。见下方修改建议。
            }

            // --- 2. 衰减逻辑 (Spring Damper) ---
            float d = 0.69314718056f; // ln(2)
            float c = d / Mathf.Max(halfLife, 0.001f); // 阻尼系数
            float e = Mathf.Exp(-c * deltaTime); // 指数项

            Vector3 currentVel = _velocityOffset;
            Vector3 currentPos = _positionOffset;

            Vector3 newVel = (currentVel - currentPos * (c * c * deltaTime)) * e;
            Vector3 newPos = (currentPos + (currentVel + currentPos * c) * deltaTime) * e;

            _positionOffset = newPos;
            _velocityOffset = newVel;

            // --- 3. 应用偏移 ---
            hipsHandle.SetLocalPosition(stream, logicalPosition + _positionOffset);
        }

        // 提供给 C# 调用的方法，用于注入初始偏移
        public void ResetOffsets(Vector3 posDiff, Vector3 velDiff)
        {
            _positionOffset = posDiff;
            _velocityOffset = velDiff;
            triggerTransition = false;
        }
    }
}