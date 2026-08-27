using System.Collections.Generic;
using EGamePlay.Combat;
using UnityEngine;

namespace ACTGameEditor.Combat
{
    /// <summary>
    /// ??????Package ?? + ActionPoint ?????
    /// </summary>
    [CreateAssetMenu(fileName = "CombatFxPackageCatalog", menuName = "ACTGame/Combat Fx Package Catalog", order = 102)]
    public sealed class CombatFxPackageCatalog : ScriptableObject
    {
        public List<CombatFxPackageDefinition> Packages = new List<CombatFxPackageDefinition>(16);
        public List<CombatFxTriggerRuleDefinition> ActionPointRules = new List<CombatFxTriggerRuleDefinition>(8);

        static CombatFxPackageCatalog _active;

        public static CombatFxPackageCatalog Active => _active != null ? _active : _active = CreateBuiltIn();

        public static void SetActive(CombatFxPackageCatalog catalog)
        {
            _active = catalog;
            if (_active != null && _active.Packages.Count == 0)
                _active.ResetToBuiltInDefaults();
        }

        public bool TryGetPackage(CombatFxPackageId id, out CombatFxPackageDefinition definition)
        {
            for (int i = 0; i < Packages.Count; i++)
            {
                if (Packages[i].Id == id)
                {
                    definition = Packages[i];
                    return true;
                }
            }

            definition = null;
            return false;
        }

        public static CombatFxPackageCatalog CreateBuiltIn()
        {
            var catalog = CreateInstance<CombatFxPackageCatalog>();
            catalog.name = "CombatFxPackageCatalog_BuiltIn";
            catalog.FillBuiltInPackages();
            catalog.FillBuiltInRules();
            return catalog;
        }

        [ContextMenu("Reset To Built-In Defaults")]
        public void ResetToBuiltInDefaults()
        {
            Packages.Clear();
            ActionPointRules.Clear();
            FillBuiltInPackages();
            FillBuiltInRules();
        }

        void FillBuiltInPackages()
        {
            Packages.Add(new CombatFxPackageDefinition
            {
                Id = CombatFxPackageId.HitTakenLight,
                DisplayName = "???",
                ReferenceNote = "??????",
                Entries = { CombatFxPackageEntry.HitFlash(0.12f) },
            });

            Packages.Add(new CombatFxPackageDefinition
            {
                Id = CombatFxPackageId.HitTakenHeavy,
                DisplayName = "???",
                ReferenceNote = "????",
                Entries = { CombatFxPackageEntry.HitFlash(0.2f) },
            });

            Packages.Add(new CombatFxPackageDefinition
            {
                Id = CombatFxPackageId.HitCausedLight,
                DisplayName = "???",
                ReferenceNote = "? HitStop + ??",
                Entries = { CombatFxPackageEntry.HitStop(0.08f, 0.08f, camera: true) },
            });

            Packages.Add(new CombatFxPackageDefinition
            {
                Id = CombatFxPackageId.HitCausedHeavy,
                DisplayName = "???",
                ReferenceNote = "? HitStop + ???",
                Entries = { CombatFxPackageEntry.HitStop(0.14f, 0.05f, camera: true) },
            });

            Packages.Add(new CombatFxPackageDefinition
            {
                Id = CombatFxPackageId.HitCausedCrit,
                DisplayName = "????",
                ReferenceNote = "????",
                Entries = { CombatFxPackageEntry.HitStop(0.16f, 0.04f, camera: true) },
            });

            Packages.Add(new CombatFxPackageDefinition
            {
                Id = CombatFxPackageId.DodgePlain,
                DisplayName = "???",
                ReferenceNote = "?? Afterimage ? Bridge",
                Entries =
                {
                    new CombatFxPackageEntry
                    {
                        Kind = CombatFxKind.Afterimage,
                        TargetMode = CombatFxTargetMode.Owner,
                        RespectGraphicsGate = true,
                    },
                },
            });

            Packages.Add(new CombatFxPackageDefinition
            {
                Id = CombatFxPackageId.DodgeTimeFracture,
                DisplayName = "??????",
                ReferenceNote = "???? 0.5s",
                Entries = { CombatFxPackageEntry.TimeFracture(0.5f, 0.3f) },
            });

            Packages.Add(new CombatFxPackageDefinition
            {
                Id = CombatFxPackageId.DodgePerfect,
                DisplayName = "Perfect Dodge",
                ReferenceNote = "?? + ?? + ??",
                Entries =
                {
                    CombatFxPackageEntry.TimeFracture(0.6f, 0.15f),
                    new CombatFxPackageEntry
                    {
                        Kind = CombatFxKind.ScreenDesaturate,
                        TargetMode = CombatFxTargetMode.None,
                        Duration = 0.6f,
                        RespectGraphicsGate = true,
                    },
                    new CombatFxPackageEntry
                    {
                        Kind = CombatFxKind.Afterimage,
                        TargetMode = CombatFxTargetMode.Owner,
                        RespectGraphicsGate = true,
                    },
                },
            });

            Packages.Add(new CombatFxPackageDefinition
            {
                Id = CombatFxPackageId.StaggerBreak,
                DisplayName = "??",
                ReferenceNote = "? HitStop + ??",
                Entries =
                {
                    CombatFxPackageEntry.HitStop(0.2f, 0.05f, camera: true),
                    CombatFxPackageEntry.HitFlash(0.25f),
                },
            });

            Packages.Add(new CombatFxPackageDefinition
            {
                Id = CombatFxPackageId.AnomalyBurst,
                DisplayName = "????",
                ReferenceNote = "HitParticle ? Bridge",
                Entries =
                {
                    new CombatFxPackageEntry
                    {
                        Kind = CombatFxKind.HitParticle,
                        TargetMode = CombatFxTargetMode.ActionTarget,
                        RespectGraphicsGate = true,
                    },
                    CombatFxPackageEntry.HitStop(0.12f, 0.08f, camera: true),
                },
            });

            Packages.Add(new CombatFxPackageDefinition
            {
                Id = CombatFxPackageId.SwitchIn,
                DisplayName = "????",
                ReferenceNote = "? HitStop",
                Entries = { CombatFxPackageEntry.HitStop(0.06f, 0.2f, camera: false) },
            });

            Packages.Add(new CombatFxPackageDefinition
            {
                Id = CombatFxPackageId.UltimateCinematic,
                DisplayName = "?????",
                ReferenceNote = "SkillTimeStop",
                Entries =
                {
                    new CombatFxPackageEntry
                    {
                        Kind = CombatFxKind.SkillTimeStop,
                        TargetMode = CombatFxTargetMode.None,
                        Duration = 2f,
                        PlayerScale = 1f,
                        RespectGraphicsGate = true,
                    },
                },
            });

            Packages.Add(new CombatFxPackageDefinition
            {
                Id = CombatFxPackageId.DeathDissolve,
                DisplayName = "????",
                ReferenceNote = "??? Despawn",
                Entries = { CombatFxPackageEntry.DeathDissolve(1.2f) },
            });
        }

        void FillBuiltInRules()
        {
            ActionPointRules.Add(new CombatFxTriggerRuleDefinition
            {
                TriggerKind = CombatFxTriggerKind.ActionPoint,
                ActionPoint = ActionPointType.PostReceiveDamage,
                PackageId = CombatFxPackageId.HitTakenLight,
                Flags = CombatFxTriggerFlags.SkipOnDodge
                    | CombatFxTriggerFlags.SkipOnImmunity
                    | CombatFxTriggerFlags.SkipOnInterrupt
                    | CombatFxTriggerFlags.RequirePositiveDamage,
            });

            ActionPointRules.Add(new CombatFxTriggerRuleDefinition
            {
                TriggerKind = CombatFxTriggerKind.ActionPoint,
                ActionPoint = ActionPointType.PostCauseDamage,
                PackageId = CombatFxPackageId.HitCausedLight,
                Flags = CombatFxTriggerFlags.LocalTruePlayerOnly
                    | CombatFxTriggerFlags.SkipOnDodge
                    | CombatFxTriggerFlags.SkipOnImmunity
                    | CombatFxTriggerFlags.SkipOnInterrupt
                    | CombatFxTriggerFlags.RequirePositiveDamage,
            });
        }
    }
}
