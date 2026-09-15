using System.Collections.Generic;

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
    }
}
