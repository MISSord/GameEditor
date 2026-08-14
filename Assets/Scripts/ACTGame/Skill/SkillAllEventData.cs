using EGamePlay.Combat;
using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ACTGameEditor
{
    [Serializable]
    public class SkillAllEventData : ScriptableObject
    {
        public int SkillId;
        [ShowInInspector]
        [LabelText("技能目标")]
        public ExecutionTargetInputType TargetInputType = ExecutionTargetInputType.TargetOrNull;
        [ShowInInspector]
        public List<SkillNewEventData> skillAllEventDatas = new List<SkillNewEventData>();

#if UNITY_EDITOR
        [Button("刷新保存数据")]
        public void SaveFlushAsset()
        {
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.AssetDatabase.SaveAssetIfDirty(this);
        }
#endif
    }
}
