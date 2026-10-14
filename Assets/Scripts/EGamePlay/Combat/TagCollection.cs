using System;
using System.Collections.Generic;

namespace EGamePlay.Combat
{
    /// <summary>
    /// 全局 Tag intern：每个点分路径（含父节点）只分配一次稳定 index。
    /// 对齐 GAS：授予 <c>State.Fire.Normal</c> 时，<c>State.Fire</c> / <c>State</c> 的 Matching 计数一并增加。
    /// Asset 用 <c>Buff.*</c>，Granted 用 <c>State.*</c>，避免都叫 Buff。
    /// </summary>
    public class TagCollection : Singleton<TagCollection>
    {
        /// <summary>位图与计数数组容量。超出则拒绝登记并打错误。</summary>
        public const int MaxTagCount = 128;

        /// <summary>路径 → intern index。含父路径。</summary>
        public static Dictionary<string, int> TagToIndexDic;

        /// <summary>路径 → [自身, 父, …, 根]，供容器 Matching 计数。</summary>
        public static Dictionary<string, int[]> TagKeyValueDic;

        /// <summary>已 intern 的路径数（含父节点）。有效 id 为 1..TagCount。</summary>
        public static int TagCount { get; private set; }

        bool _isInitialized;
        /// <summary>下一个 intern id，从 1 起。0 留给未缓存哨兵。</summary>
        int _nextIndex;

        /// <summary>路径是否已 intern。</summary>
        public static bool TryGetIndex(string tagName, out int index)
        {
            if (TagToIndexDic != null && !string.IsNullOrEmpty(tagName))
                return TagToIndexDic.TryGetValue(tagName, out index);
            index = 0;
            return false;
        }

        /// <summary>已 intern 的 index。未登记返回 0（哨兵，不是合法 Tag）。</summary>
        public static int GetIndexOrInvalid(string tagName)
        {
            return TryGetIndex(tagName, out int index) ? index : 0;
        }

        /// <summary>自身 + 祖先 intern id。未登记返回 false。</summary>
        public static bool TryGetHierarchy(string tagName, out int[] hierarchy)
        {
            if (TagKeyValueDic != null && !string.IsNullOrEmpty(tagName))
                return TagKeyValueDic.TryGetValue(tagName, out hierarchy);
            hierarchy = null;
            return false;
        }

        /// <summary>登记 <see cref="AllTags"/> 的全部前缀，父节点共享 id。</summary>
        public void Initialize()
        {
            if (_isInitialized)
                return;

            TagKeyValueDic = new Dictionary<string, int[]>(AllTags.Count * 2);
            TagToIndexDic = new Dictionary<string, int>(AllTags.Count * 2);
            // 0 留给未缓存 / OnDestroy 哨兵；HasIndex(0) 必须恒为假。
            _nextIndex = 1;

            for (int i = 0; i < AllTags.Count; i++)
                RegisterPath(AllTags[i]);

            TagCount = _nextIndex - 1;
            _isInitialized = true;
            ValidateSharedParents();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            RunContainerSmokeTest();
#endif
            GameLog.CombatDebug($"Tag Library Initialized. Paths={TagToIndexDic.Count}, InternedIds={TagCount}");
        }

        void RegisterPath(string fullPath)
        {
            if (string.IsNullOrEmpty(fullPath))
                return;

            string[] parts = fullPath.Split('.');
            if (parts.Length == 0)
                return;

            int[] prefixIds = new int[parts.Length];
            string current = parts[0];
            for (int i = 0; i < parts.Length; i++)
            {
                if (i > 0)
                    current = current + "." + parts[i];

                int id = Intern(current);
                if (id < 0)
                    return;
                prefixIds[i] = id;

                if (TagKeyValueDic.ContainsKey(current))
                    continue;

                // [自身, 父, …, 根]：加叶子时 Matching 计数沿祖先上浮。
                int[] hierarchy = new int[i + 1];
                for (int j = 0; j <= i; j++)
                    hierarchy[j] = prefixIds[i - j];
                TagKeyValueDic[current] = hierarchy;
            }
        }

        int Intern(string path)
        {
            if (TagToIndexDic.TryGetValue(path, out int existing))
                return existing;
            if (_nextIndex >= MaxTagCount)
            {
                GameLog.CombatError($"Tag intern overflow: '{path}' needs index {_nextIndex}, max {MaxTagCount}");
                return -1;
            }

            int id = _nextIndex;
            _nextIndex++;
            TagToIndexDic[path] = id;
            return id;
        }

        void ValidateSharedParents()
        {
            AssertSharedParent("State.Freeze", "State.Dizziness", "State");
            AssertSharedParent("State.Fire.Normal", "State.Fire.Hight", "State.Fire");
            AssertSharedParent("Buff.Bind.Skill", "Buff.Bind.Form", "Buff.Bind");
        }

        void AssertSharedParent(string leafA, string leafB, string parent)
        {
            if (!TryGetIndex(parent, out int parentId))
            {
                GameLog.CombatError($"Tag intern missing parent '{parent}'");
                return;
            }

            if (!HierarchyContains(leafA, parentId) || !HierarchyContains(leafB, parentId))
                GameLog.CombatError($"Tag intern: '{leafA}' and '{leafB}' must share parent '{parent}' id={parentId}");
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        static void RunContainerSmokeTest()
        {
            var c = new GameplayTagContainer();
            var freezeSrc = TagSource.Manual(1);
            var fireSrc = TagSource.Manual(2);
            c.Push(freezeSrc, CombatTags.StateFreeze);
            if (!c.HasTag(CombatTags.StateFreeze) || !c.HasTag("State") || c.HasExactTag("State")
                || !c.HasExactTag(CombatTags.StateFreeze) || c.HasTag("State.Dizziness")
                || c.HasTag("Buff") || c.HasTag(CombatTags.BuffDebuff))
            {
                GameLog.CombatError("Tag smoke: Freeze must Matching State, Exact Freeze, not Dizziness/Buff.");
                return;
            }

            c.Push(fireSrc, "State.Fire.Normal");
            var requiredFire = new List<string> { "State.Fire" };
            if (!c.HasTag("State.Fire") || c.HasExactTag("State.Fire")
                || !c.CanSpellSkillWithTagLists(requiredFire, blocked: null))
            {
                GameLog.CombatError("Tag smoke: Fire.Normal must Matching State.Fire for RequiredTags.");
                return;
            }

            c.Pop(freezeSrc, CombatTags.StateFreeze);
            if (c.HasTag(CombatTags.StateFreeze) || !c.HasTag("State"))
            {
                GameLog.CombatError("Tag smoke: popping Freeze must keep State via Fire.Normal.");
                return;
            }

            c.Pop(fireSrc, "State.Fire.Normal");
            if (c.HasTag("State") || c.HasTag("State.Fire") || c.HasTag(CombatTags.StateFreeze))
                GameLog.CombatError("Tag smoke: container should be empty after paired Pop.");
        }
#endif

        static bool HierarchyContains(string tagName, int id)
        {
            if (!TryGetHierarchy(tagName, out int[] hierarchy) || hierarchy == null)
                return false;
            for (int i = 0; i < hierarchy.Length; i++)
            {
                if (hierarchy[i] == id)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// intern 源列表。只写叶子；<see cref="Initialize"/> 会自动登记父路径（<c>State</c>、<c>State.Fire</c>、<c>Buff.Bind</c>）。
        /// <c>State.*</c> = Granted；<c>Buff.*</c> = Asset。<c>Combat</c>/<c>Ai</c>/<c>Stance</c>/<c>Locomotion</c> 也是运行时状态。
        /// </summary>
        public List<string> AllTags = new List<string>()
        {
            "State.Dizziness",
            "State.Freeze",
            "State.Silence",
            "State.Weak",
            "State.Roll",
            "State.AttackDamageForbid",
            "State.MoveForbid",
            "State.SkillForbid",
            "State.UnStopped",
            "State.VibrationBreak",
            "Buff.Debuff",
            "Buff.Buff",
            "Buff.Bind.Skill",
            "Buff.Bind.Form",
            "Buff.Bind.HitsTaken",
            "Buff.Bind.HitsDealt",
            "Buff.Caster.KeepFirst",
            "State.Fire.Normal",
            "State.Fire.Hight",
            "State.Fire.Ignite",
            "State.Spectro.Frazzle",
            "State.Aero.Erosion",
            "State.Electro.Flare",
            "Stance.Form",
            "Locomotion.Airborne",
            "Combat.DazeRecover",
            "Ai.Parryable",
            "Ai.ParryBreak",
            "Combat.ParryWindow",
            "Combat.SwitchIFrame",
        };
    }

    /// <summary>
    /// 与战斗逻辑强相关的 Tag 名常量，集中管理以避免硬编码字符串。
    /// 须与 <see cref="TagCollection.AllTags"/> 中的条目保持一致（Asset-only 且尚未 intern 的 Outro 等除外）。
    /// 运行时 <see cref="GameplayTagContainer.HasTag(string)"/> 是 Matching（含子）。
    /// Buff 表两列：<c>BuffTag</c>=Asset（一律 <c>Buff.*</c>），<c>GrantedTags</c>=Granted（一律 <c>State.*</c>）。见 <see cref="IsAssetOnlyTag"/>。
    /// </summary>
    public static class CombatTags
    {
        /// <summary>禁造成伤害（缴械类）。写 GrantedTags（State.*）。</summary>
        public const string StateAttackDamageForbid = "State.AttackDamageForbid";
        /// <summary>禁移动。硬控互斥与门控认这个。写 GrantedTags 或 PlayerControll。</summary>
        public const string StateMoveForbid = "State.MoveForbid";
        /// <summary>禁技能。沉默只挂这个，仍可闪避。写 GrantedTags 或 PlayerControll。</summary>
        public const string StateSkillForbid = "State.SkillForbid";
        /// <summary>抗打断加值（默认 +3），不是布尔免疫。轴上短时霸体。</summary>
        public const string StateUnStopped = "State.UnStopped";
        /// <summary>削韧瘫痪窗。查询用叶子，不是 MoveForbid，不进 HardControlMutex。</summary>
        public const string StateVibrationBreak = "State.VibrationBreak";
        /// <summary>翻滚 i-frame。HitResolver 标 Dodge。轴上 Tag 或出招当下抢先 Push。</summary>
        public const string StateRoll = "State.Roll";
        /// <summary>冻结：停动画与位移（实体 TimeScale=0），点燃仍走世界钟。写 GrantedTags（State.*）。</summary>
        public const string StateFreeze = "State.Freeze";
        /// <summary>减益极性。只写 BuffTag（Asset），净化 DebuffOnly 认表，不要 Push 到角色。</summary>
        public const string BuffDebuff = "Buff.Debuff";
        /// <summary>增益极性。只写 BuffTag（Asset），净化 BuffOnly 认表。</summary>
        public const string BuffGain = "Buff.Buff";
        /// <summary>直到施加时那次技能轴结束。只写 BuffTag（Asset）。</summary>
        public const string BuffBindSkill = "Buff.Bind.Skill";
        /// <summary>直到持有者离开施加时的形态。只写 BuffTag（Asset）。</summary>
        public const string BuffBindForm = "Buff.Bind.Form";
        /// <summary>持有者被实际扣血 N 次后卸，N 用 BaseTimes。只写 BuffTag（Asset）。</summary>
        public const string BuffBindHitsTaken = "Buff.Bind.HitsTaken";
        /// <summary>持有者打出实际扣血 N 次后卸，N 用 BaseTimes。只写 BuffTag（Asset）。</summary>
        public const string BuffBindHitsDealt = "Buff.Bind.HitsDealt";
        /// <summary>换人退场卸。只写 BuffTag（Asset）。M4.3 挂钩。</summary>
        public const string BuffBindSwitchOut = "Buff.Bind.SwitchOut";
        /// <summary>ReApply 保持原施法者。只写 BuffTag（Asset）。不打则后上覆盖。</summary>
        public const string BuffCasterKeepFirst = "Buff.Caster.KeepFirst";
        /// <summary>延奏：换人时转移这条 Buff。只写 BuffTag（Asset），不要 Granted。M4.3。</summary>
        public const string BuffOutro = "Buff.Outro";
        /// <summary>处于非默认战斗形态（明心境、变身等）。</summary>
        public const string StanceForm = "Stance.Form";
        /// <summary>空中；Resolver 主要用 IsAirborne，此标签供技能边条件。</summary>
        public const string LocomotionAirborne = "Locomotion.Airborne";
        /// <summary>失衡起身无敌。HitResolver 标 Immunity，不是翻滚闪避。</summary>
        public const string CombatDazeRecover = "Combat.DazeRecover";
        /// <summary>敌人黄闪判定段：本段可被招架消伤。</summary>
        public const string AiParryable = "Ai.Parryable";
        /// <summary>招架成功时断轴。没有此 Tag 则只消本段伤害、轴继续。</summary>
        public const string AiParryBreak = "Ai.ParryBreak";
        /// <summary>招架演出 i-frame。成功已在按键成交时结算；有此 Tag 时后续伤害标免疫。</summary>
        public const string CombatParryWindow = "Combat.ParryWindow";
        /// <summary>红闪回避支援换入无敌。HitResolver 标 Immunity，不走翻滚闪避。</summary>
        public const string CombatSwitchIFrame = "Combat.SwitchIFrame";
        /// <summary>鸣潮光噪。ActionModify Filter 5 可认此 Tag。写 GrantedTags。</summary>
        public const string StateSpectroFrazzle = "State.Spectro.Frazzle";
        /// <summary>鸣潮风蚀。跳伤保层。写 GrantedTags。</summary>
        public const string StateAeroErosion = "State.Aero.Erosion";
        /// <summary>鸣潮电磁。跳伤减半层，并按层减攻。写 GrantedTags。</summary>
        public const string StateElectroFlare = "State.Electro.Flare";

        /// <summary>
        /// GAS Asset-only：描述 Buff 自己，前缀一律 <c>Buff.</c>，禁止进角色容器。
        /// 极性、Bind.*、Caster.KeepFirst、Outro。Granted 请用 <c>State.</c>。
        /// </summary>
        public static bool IsAssetOnlyTag(string tag)
        {
            if (string.IsNullOrEmpty(tag))
                return false;
            if (tag == BuffDebuff || tag == BuffGain
                || tag == BuffCasterKeepFirst || tag == BuffOutro)
                return true;
            return tag.StartsWith("Buff.Bind.", StringComparison.Ordinal);
        }
    }
}
