//using Sirenix.OdinInspector;
//using System;
//using System.Collections.Generic;
//using UnityEngine;

//namespace EGamePlay.Combat
//{
//    [Serializable]
//#if UNITY
//    public class TriggerConfig : System.Object
//#else
//    public class TriggerConfig : ET.Object
//#endif
//    {
//        private string Label = "被动触发";

//        [ToggleGroup("Enabled", "$Label")]
//        public bool Enabled;

//        #region 被动触发
//        private bool HideAutoTrigger = false; //TriggerType == EffectTriggerType.ExecuteTrigger;

//        [FoldoutGroup("Enabled/TriggerType", GroupName = "触发机制")]
//        [ToggleGroup("Enabled"), HideIf("HideAutoTrigger"), LabelText("被动事件")]
//        public EffectAutoTriggerType AutoTriggerType;

//        private bool ShowActionTrigger => !HideAutoTrigger && AutoTriggerType == EffectAutoTriggerType.Action;

//        [FoldoutGroup("Enabled/TriggerType")]
//        [ToggleGroup("Enabled"), ShowIf("ShowActionTrigger")]
//        public ActionPointType ActionPointType;

//        #endregion

//        //一般来说，一条就够（DynamicExpresso支持有和与或），但为了兼容多种情况，采用列表
//        [ToggleGroup("Enabled"), LabelText("状态判断")]
//        public List<string> StateCheckList = new List<string>();

//        /// <summary>触发时执行的效果 ID；为空时触发该技能下全部效果。</summary>
//        [ToggleGroup("Enabled"), LabelText("效果ID（空=全部）")]
//        [ListDrawerSettings(DefaultExpandedState = false, DraggableItems = true)]
//        public List<int> EffectIds = new List<int>();

//#if UNITY_EDITOR
//        private void DrawSpace()
//        {
//            GUILayout.Space(20);
//        }

//        private void BeginBox()
//        {
//        }

//        private void EndBox()
//        {
//        }
//#endif
//    }
//}