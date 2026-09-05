using System;
using System.Collections.Generic;
using EGamePlay.Combat;
using UnityEngine;

namespace ACTGameEditor.Combat
{
    /// <summary>单个表现包定义：ID + 有序条目列表。</summary>
    [Serializable]
    public sealed class CombatFxPackageDefinition
    {
        public CombatFxPackageId Id;
        public string DisplayName;
        [TextArea(2, 4)]
        public string ReferenceNote;
        public List<CombatFxPackageEntry> Entries = new List<CombatFxPackageEntry>(2);
    }

    /// <summary>ActionPoint → Package 路由规则。</summary>
    [Serializable]
    public sealed class CombatFxTriggerRuleDefinition
    {
        public CombatFxTriggerKind TriggerKind = CombatFxTriggerKind.ActionPoint;
        public ActionPointType ActionPoint;
        public CombatFxPackageId PackageId;
        public CombatFxTriggerFlags Flags =
            CombatFxTriggerFlags.SkipOnDodge
            | CombatFxTriggerFlags.SkipOnImmunity
            | CombatFxTriggerFlags.SkipOnInterrupt
            | CombatFxTriggerFlags.RequirePositiveDamage;
        /// <summary>0 不限。技能段号下限（含）。段 ≥ 2 当重击。</summary>
        public int MinDamageSegment;
        /// <summary>0 不限。技能段号上限（含）。</summary>
        public int MaxDamageSegment;
    }
}
