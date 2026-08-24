using System;
using System.Collections.Generic;
using EGamePlay.Combat;
using UnityEngine;

namespace ACTGameEditor
{
    /// <summary>
    /// Idle 状态下的「输入 → 技能」映射表，替代 NormalActPlayer 中的硬编码 SkillId。
    /// </summary>
    [CreateAssetMenu(fileName = "IdleSkillMapping", menuName = "ACTGame/IdleSkillMapping")]
    public class IdleSkillMapping : ScriptableObject
    {
        [Serializable]
        public class Mapping
        {
            [Tooltip("监听输入类型")]
            public InputListernType InputType;
            [Tooltip("按下类型")]
            public PressType PressType;
            [Tooltip("回调类型")]
            public InputCallBackType InputCallBackType = InputCallBackType.Performed;
            [Tooltip("对应技能 ID")]
            public int SkillId;
            [Tooltip("技能优先级")]
            public int Sort;
            [Tooltip("必须满足的标签")]
            public List<string> RequiredTags = new List<string>();
            [Tooltip("不能有的标签")]
            public List<string> BlockedTags = new List<string>();
            [Tooltip("预输入有效时长（秒），0=使用 NormalActPlayer.InputTimeout")]
            [Range(0f, 2f)]
            public float InputTimeout;
        }

        [Tooltip("按优先级从高到低排序，先匹配到的优先释放")]
        public List<Mapping> Mappings = new List<Mapping>();
    }
}
