//using System.Collections.Generic;

//namespace EGamePlay.Combat
//{
//    /// <summary>
//    /// 触发器状态判断 Helper，供 TriggerData 使用，不依赖 Component。
//    /// </summary>
//    public static class TriggerStateCheckHelper
//    {
//        /// <summary>
//        /// 根据 TriggerConfig.StateCheckList 编译 ExpressionCondition 列表。
//        /// </summary>
//        public static List<ExpressionCondition> CompileStateChecks(TriggerConfig config)
//        {
//            var list = new List<ExpressionCondition>();
//            if (config?.StateCheckList == null) return list;
//            foreach (var str in config.StateCheckList)
//            {
//                if (string.IsNullOrEmpty(str)) continue;
//                var cond = new ExpressionCondition { Expression = str };
//                cond.Compile();
//                list.Add(cond);
//            }
//            return list;
//        }

//        /// <summary>
//        /// 检查目标状态是否满足条件。
//        /// </summary>
//        public static bool CheckTargetState(List<ExpressionCondition> stateChecks, CombatEntity caster, Entity target)
//        {
//            if (stateChecks == null || stateChecks.Count == 0) return true;
//            var ctx = new ExpressionContext { Caster = caster, Target = target };
//            foreach (var item in stateChecks)
//            {
//                if (!item.Check(ctx)) return false;
//            }
//            return true;
//        }
//    }
//}
