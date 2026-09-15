# ACTGameEditor

项目全景（已落地玩法、分层、配表与编辑器）：**[Docs/ProjectOverview.md](Docs/ProjectOverview.md)**。

改战斗、技能、Buff、时间或配表前，先读 **[Docs/ProjectConventions.md](Docs/ProjectConventions.md)**（目录、分层、硬约定）。

- 配表：只改 `Tools/Config/Datas/*.xlsx`，再导出。规则：[.cursor/rules/luban-config.mdc](.cursor/rules/luban-config.mdc)
- 技能轴：只改 `Assets/Editor/SkillSequences/` 预制体，再 Flux 导出。禁止手改 `SkillDataScriptable`。规则：[.cursor/rules/skill-timeline.mdc](.cursor/rules/skill-timeline.mdc)
- 文档状态总索引（未完成项清单，开工前先看）：[Docs/ActUnfinishedIndex.md](Docs/ActUnfinishedIndex.md)
- 时间钟：[Docs/已完成/ActTimeEffectsBacklog.md](Docs/已完成/ActTimeEffectsBacklog.md)（已归档）
- Buff：[Docs/ActBuffLearningBacklog.md](Docs/ActBuffLearningBacklog.md)
- 主动技数字：[Docs/已完成/ActSkillConfigAndLeveling.md](Docs/已完成/ActSkillConfigAndLeveling.md)（已归档）
- 角色招式包表（槽位/被动/号段）：[Docs/ActSkillKitConfig.md](Docs/ActSkillKitConfig.md)
- 相机意图/构图：[Docs/ActCameraStage2Design.md](Docs/ActCameraStage2Design.md)
- 敌人 AI（导演 / HFSM / 选招）：[Docs/ActEnemyAiDesign.md](Docs/ActEnemyAiDesign.md)
- 战斗缺口与里程碑（偏谐 / 招架 / 小队；失衡连携已屏蔽）：[Docs/ActCombatRoadmap.md](Docs/ActCombatRoadmap.md)
- 偏谐条 / 谐度破坏（F；对照鸣潮 3.0）：[Docs/已完成/ActHarmonyBreakDesign.md](Docs/已完成/ActHarmonyBreakDesign.md)（已归档）
- 小队换人（三人队 / 支援 / 延奏）：[Docs/ActSquadDesign.md](Docs/ActSquadDesign.md)
- 角色选型与招式包模板（单手刀/剑）：[Docs/ActCharacterKitTemplate.md](Docs/ActCharacterKitTemplate.md)
- 走跑 / 移动动画（鸣潮子集：起步急停 Pivot）：[Docs/ActLocomotionWuWaPlan.md](Docs/ActLocomotionWuWaPlan.md)
- Unity 热路径性能：仓库根目录 `.cursorrules`
