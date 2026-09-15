using System.Collections.Generic;
using EGamePlay;
using EGamePlay.Combat;

namespace ACTGameEditor.Combat
{
    /// <summary>
    /// 从技能轴 <c>SkillInputEvents</c> 收集连招可达 SkillId。只在加载时调用，不进热路径。
    /// </summary>
    public static class SkillTimelineCombo
    {
        /// <summary>把 <paramref name="ioIds"/> 里每个入口沿轴窗边展开，结果写回同一集合。</summary>
        public static void ExpandFromTimelines(HashSet<int> ioIds)
        {
            if (ioIds == null || ioIds.Count == 0)
                return;

            var pending = new Queue<int>(ioIds.Count);
            foreach (int id in ioIds)
            {
                if (id > 0)
                    pending.Enqueue(id);
            }

            var visited = new HashSet<int>();
            while (pending.Count > 0)
            {
                int id = pending.Dequeue();
                if (id <= 0 || !visited.Add(id))
                    continue;

                ioIds.Add(id);
                SkillAllEventData data = ActSkillTimelineLoader.GetOrLoad(id);
                if (data?.skillAllEventDatas == null)
                    continue;

                for (int s = 0; s < data.skillAllEventDatas.Count; s++)
                {
                    SkillNewEventData sub = data.skillAllEventDatas[s];
                    List<XCSkillInputEventData> events = sub.SkillInputEvents != null
                        ? sub.SkillInputEvents.Events
                        : null;
                    if (events == null)
                        continue;

                    for (int e = 0; e < events.Count; e++)
                    {
                        List<SkillInputData> list = events[e].InputDataList;
                        if (list == null)
                            continue;
                        for (int i = 0; i < list.Count; i++)
                        {
                            int next = list[i].SkillId;
                            if (next > 0 && !visited.Contains(next))
                                pending.Enqueue(next);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 当前普攻轴上、指定 Attack 键点按的下一发连段。分类须为普攻/重击/冲刺攻。
        /// 多窗取最早一帧；同一窗取列表中第一条。没有则 0。
        /// </summary>
        public static int FindNextAttackClick(int fromSkillId, InputListernType attackCommand)
        {
            if (fromSkillId <= 0)
                return 0;

            SkillAllEventData data = ActSkillTimelineLoader.GetOrLoad(fromSkillId);
            if (data?.skillAllEventDatas == null)
                return 0;

            int bestId = 0;
            int bestStart = int.MaxValue;
            int bestEvent = int.MaxValue;
            int bestInput = int.MaxValue;

            for (int s = 0; s < data.skillAllEventDatas.Count; s++)
            {
                SkillNewEventData sub = data.skillAllEventDatas[s];
                List<XCSkillInputEventData> events = sub.SkillInputEvents != null
                    ? sub.SkillInputEvents.Events
                    : null;
                if (events == null)
                    continue;

                for (int e = 0; e < events.Count; e++)
                {
                    XCSkillInputEventData ev = events[e];
                    List<SkillInputData> list = ev != null ? ev.InputDataList : null;
                    if (list == null)
                        continue;

                    int start = ev.Range != null ? ev.Range.Start : int.MaxValue;
                    for (int i = 0; i < list.Count; i++)
                    {
                        SkillInputData edge = list[i];
                        if (edge == null || edge.SkillId <= 0 || edge.SkillId == fromSkillId)
                            continue;
                        if (!IsAttackClickEdge(edge, attackCommand))
                            continue;
                        if (!IsComboContinueSkill(edge.SkillId))
                            continue;

                        if (start > bestStart)
                            continue;
                        if (start == bestStart && e > bestEvent)
                            continue;
                        if (start == bestStart && e == bestEvent && i >= bestInput)
                            continue;

                        bestId = edge.SkillId;
                        bestStart = start;
                        bestEvent = e;
                        bestInput = i;
                    }
                }
            }

            return bestId;
        }

        /// <summary>普攻连段可跨 F 续的分类：基础 / 重击 / 冲刺攻。闪避、战技、破坏技不算。</summary>
        public static bool IsComboContinueSkill(int skillId)
        {
            if (skillId <= 0 || SkillSettingMgr.Instance == null)
                return false;
            SkillCategory cat = SkillSettingMgr.Instance.GetSkillCategory(skillId);
            return cat == SkillCategory.BasicAttack
                || cat == SkillCategory.HeavyAttack
                || cat == SkillCategory.DashAttack;
        }

        static bool IsAttackClickEdge(SkillInputData edge, InputListernType attackCommand)
        {
            InputListernType cmd = edge.ListernType;
            PressType press = edge.PressType;
            switch (cmd)
            {
                case InputListernType.LongButtonX:
                    cmd = InputListernType.ButtonX;
                    press = PressType.LongPress;
                    break;
                case InputListernType.LongButtonY:
                    cmd = InputListernType.ButtonY;
                    press = PressType.LongPress;
                    break;
                case InputListernType.LongButtonA:
                    cmd = InputListernType.ButtonA;
                    press = PressType.LongPress;
                    break;
                case InputListernType.LongButtonB:
                    cmd = InputListernType.ButtonB;
                    press = PressType.LongPress;
                    break;
            }

            return cmd == attackCommand && press == PressType.Click;
        }
    }
}
