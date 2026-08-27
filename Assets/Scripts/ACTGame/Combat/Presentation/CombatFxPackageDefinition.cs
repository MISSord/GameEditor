using System;
using System.Collections.Generic;
using EGamePlay.Combat;
using UnityEngine;

namespace ACTGameEditor.Combat
{
    /// <summary>??????ID + ?????</summary>
    [Serializable]
    public sealed class CombatFxPackageDefinition
    {
        public CombatFxPackageId Id;
        public string DisplayName;
        [TextArea(2, 4)]
        public string ReferenceNote;
        public List<CombatFxPackageEntry> Entries = new List<CombatFxPackageEntry>(2);
    }

    /// <summary>ActionPoint ? Package ??????</summary>
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
    }
}
