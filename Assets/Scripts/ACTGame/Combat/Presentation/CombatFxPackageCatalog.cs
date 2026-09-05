using System.Collections.Generic;
using EGamePlay.Combat;
using UnityEngine;

namespace ACTGameEditor.Combat
{
    /// <summary>
    /// 表现包目录：Package 定义 + ActionPoint 默认路由。
    /// </summary>
    [CreateAssetMenu(fileName = "CombatFxPackageCatalog", menuName = "ACTGame/Combat Fx Package Catalog", order = 102)]
    public sealed class CombatFxPackageCatalog : ScriptableObject
    {
        public List<CombatFxPackageDefinition> Packages = new List<CombatFxPackageDefinition>(16);
        public List<CombatFxTriggerRuleDefinition> ActionPointRules = new List<CombatFxTriggerRuleDefinition>(8);

        static CombatFxPackageCatalog _active;

        /// <summary>当前生效目录；未指定时使用内置默认包。</summary>
        public static CombatFxPackageCatalog Active => _active != null ? _active : _active = CreateBuiltIn();

        /// <summary>由 EGamePlayInit 注入项目级目录；空列表时回填内置默认包。</summary>
        public static void SetActive(CombatFxPackageCatalog catalog)
        {
            _active = catalog;
            if (_active == null)
                return;
            if (_active.ActionPointRules == null)
                _active.ActionPointRules = new List<CombatFxTriggerRuleDefinition>(8);
            if (_active.Packages.Count == 0)
                _active.ResetToBuiltInDefaults();
            else if (_active.ActionPointRules.Count <= 2)
            {
                _active.ActionPointRules.Clear();
                _active.FillBuiltInRules();
            }
        }

        /// <summary>按 ID 查找包定义。</summary>
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

        /// <summary>内置默认目录（无 asset 时 fallback，亦用于 Create 菜单预填）。</summary>
        public static CombatFxPackageCatalog CreateBuiltIn()
        {
            var catalog = CreateInstance<CombatFxPackageCatalog>();
            catalog.name = "CombatFxPackageCatalog_BuiltIn";
            catalog.FillBuiltInPackages();
            catalog.FillBuiltInRules();
            return catalog;
        }

        /// <summary>Editor / CreateAssetMenu：重置为内置默认内容。</summary>
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
                DisplayName = "轻受击",
                ReferenceNote = "鸣潮/ZZZ 通用：受击 MPB 闪白。",
                Entries = { CombatFxPackageEntry.HitFlash(0.12f) },
            });

            Packages.Add(new CombatFxPackageDefinition
            {
                Id = CombatFxPackageId.HitTakenHeavy,
                DisplayName = "重受击",
                ReferenceNote = "重击/击飞段：加长闪白。",
                Entries = { CombatFxPackageEntry.HitFlash(0.2f) },
            });

            Packages.Add(new CombatFxPackageDefinition
            {
                Id = CombatFxPackageId.HitCausedLight,
                DisplayName = "轻命中",
                ReferenceNote = "鸣潮偏轻：短 HitStop + 镜头冲击。",
                Entries = { CombatFxPackageEntry.HitStop(0.08f, 0.08f, camera: true, timePriority: 10) },
            });

            Packages.Add(new CombatFxPackageDefinition
            {
                Id = CombatFxPackageId.HitCausedHeavy,
                DisplayName = "重命中",
                ReferenceNote = "ZZZ 风格：更长 HitStop + 强镜头。",
                Entries = { CombatFxPackageEntry.HitStop(0.14f, 0.05f, camera: true, timePriority: 20) },
            });

            Packages.Add(new CombatFxPackageDefinition
            {
                Id = CombatFxPackageId.HitCausedCrit,
                DisplayName = "暴击命中",
                ReferenceNote = "暴击：在重命中基础上略延长顿帧。",
                Entries = { CombatFxPackageEntry.HitStop(0.16f, 0.04f, camera: true, timePriority: 30) },
            });

            Packages.Add(new CombatFxPackageDefinition
            {
                Id = CombatFxPackageId.DodgePlain,
                DisplayName = "纯闪避",
                ReferenceNote = "仅残影。",
                Entries = { CombatFxPackageEntry.Afterimage() },
            });

            Packages.Add(new CombatFxPackageDefinition
            {
                Id = CombatFxPackageId.DodgeTimeFracture,
                DisplayName = "时空断裂闪避",
                ReferenceNote = "鸣潮极限闪避：世界减速 + 残影 + 灰屏。",
                Entries =
                {
                    CombatFxPackageEntry.TimeFracture(0.5f, 0.3f),
                    CombatFxPackageEntry.ScreenDesaturate(0.5f),
                    CombatFxPackageEntry.Afterimage(),
                },
            });

            Packages.Add(new CombatFxPackageDefinition
            {
                Id = CombatFxPackageId.DodgePerfect,
                DisplayName = "Perfect Dodge",
                ReferenceNote = "灰屏慢动作 + 残影 + 短断裂。",
                Entries =
                {
                    CombatFxPackageEntry.TimeFracture(0.6f, 0.15f),
                    CombatFxPackageEntry.ScreenDesaturate(0.6f),
                    CombatFxPackageEntry.Afterimage(),
                },
            });

            Packages.Add(new CombatFxPackageDefinition
            {
                Id = CombatFxPackageId.StaggerBreak,
                DisplayName = "破韧",
                ReferenceNote = "强 HitStop + 闪白。",
                Entries =
                {
                    CombatFxPackageEntry.HitStop(0.2f, 0.05f, camera: true, timePriority: 40),
                    CombatFxPackageEntry.HitFlash(0.25f),
                },
            });

            Packages.Add(new CombatFxPackageDefinition
            {
                Id = CombatFxPackageId.AnomalyBurst,
                DisplayName = "异常爆发",
                ReferenceNote = "HitParticle 待 Bridge。",
                Entries =
                {
                    new CombatFxPackageEntry
                    {
                        Kind = CombatFxKind.HitParticle,
                        TargetMode = CombatFxTargetMode.ActionTarget,
                        RespectGraphicsGate = true,
                    },
                    CombatFxPackageEntry.HitStop(0.12f, 0.08f, camera: true, timePriority: 15),
                },
            });

            Packages.Add(new CombatFxPackageDefinition
            {
                Id = CombatFxPackageId.SwitchIn,
                DisplayName = "切人入场",
                ReferenceNote = "短 HitStop。",
                Entries = { CombatFxPackageEntry.HitStop(0.06f, 0.2f, camera: false, timePriority: 5) },
            });

            Packages.Add(new CombatFxPackageDefinition
            {
                Id = CombatFxPackageId.UltimateCinematic,
                DisplayName = "终结技演出",
                ReferenceNote = "共鸣解放 / Ultimate：SkillTimeStop。",
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
                DisplayName = "死亡溶解",
                ReferenceNote = "溶解后 Despawn。",
                Entries = { CombatFxPackageEntry.DeathDissolve(1.2f) },
            });
        }

        void FillBuiltInRules()
        {
            CombatFxTriggerFlags causeBase = CombatFxTriggerFlags.LocalTruePlayerOnly
                | CombatFxTriggerFlags.SkipOnDodge
                | CombatFxTriggerFlags.SkipOnImmunity
                | CombatFxTriggerFlags.SkipOnInterrupt
                | CombatFxTriggerFlags.RequirePositiveDamage
                | CombatFxTriggerFlags.RequireSkillDamage;

            CombatFxTriggerFlags takenBase = CombatFxTriggerFlags.SkipOnDodge
                | CombatFxTriggerFlags.SkipOnImmunity
                | CombatFxTriggerFlags.SkipOnInterrupt
                | CombatFxTriggerFlags.RequirePositiveDamage;

            ActionPointRules.Add(new CombatFxTriggerRuleDefinition
            {
                TriggerKind = CombatFxTriggerKind.ActionPoint,
                ActionPoint = ActionPointType.PostReceiveDamage,
                PackageId = CombatFxPackageId.HitTakenLight,
                Flags = takenBase | CombatFxTriggerFlags.SkipOnCritical,
                MaxDamageSegment = 1,
            });

            ActionPointRules.Add(new CombatFxTriggerRuleDefinition
            {
                TriggerKind = CombatFxTriggerKind.ActionPoint,
                ActionPoint = ActionPointType.PostReceiveDamage,
                PackageId = CombatFxPackageId.HitTakenHeavy,
                Flags = takenBase | CombatFxTriggerFlags.SkipOnCritical,
                MinDamageSegment = 2,
            });

            ActionPointRules.Add(new CombatFxTriggerRuleDefinition
            {
                TriggerKind = CombatFxTriggerKind.ActionPoint,
                ActionPoint = ActionPointType.PostReceiveDamage,
                PackageId = CombatFxPackageId.HitTakenHeavy,
                Flags = takenBase | CombatFxTriggerFlags.RequireCritical,
            });

            ActionPointRules.Add(new CombatFxTriggerRuleDefinition
            {
                TriggerKind = CombatFxTriggerKind.ActionPoint,
                ActionPoint = ActionPointType.PostCauseDamage,
                PackageId = CombatFxPackageId.HitCausedCrit,
                Flags = causeBase | CombatFxTriggerFlags.RequireCritical,
            });

            ActionPointRules.Add(new CombatFxTriggerRuleDefinition
            {
                TriggerKind = CombatFxTriggerKind.ActionPoint,
                ActionPoint = ActionPointType.PostCauseDamage,
                PackageId = CombatFxPackageId.HitCausedHeavy,
                Flags = causeBase | CombatFxTriggerFlags.SkipOnCritical,
                MinDamageSegment = 2,
            });

            ActionPointRules.Add(new CombatFxTriggerRuleDefinition
            {
                TriggerKind = CombatFxTriggerKind.ActionPoint,
                ActionPoint = ActionPointType.PostCauseDamage,
                PackageId = CombatFxPackageId.HitCausedLight,
                Flags = causeBase | CombatFxTriggerFlags.SkipOnCritical,
                MaxDamageSegment = 1,
            });

            ActionPointRules.Add(new CombatFxTriggerRuleDefinition
            {
                TriggerKind = CombatFxTriggerKind.ActionPoint,
                ActionPoint = ActionPointType.PostReceiveDamage,
                PackageId = CombatFxPackageId.DodgeTimeFracture,
                Flags = CombatFxTriggerFlags.LocalTruePlayerOnly
                    | CombatFxTriggerFlags.RequireDodge
                    | CombatFxTriggerFlags.SkipOnImmunity
                    | CombatFxTriggerFlags.SkipOnInterrupt
                    | CombatFxTriggerFlags.RequireSkillDamage,
            });
        }
    }
}
