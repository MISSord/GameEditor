# ACTGameEditor

改战斗、技能、Buff、时间或配表前，先读 **[Docs/ProjectConventions.md](Docs/ProjectConventions.md)**（目录、分层、硬约定）。

- 配表：只改 `Tools/Config/Datas/*.xlsx`，再导出。规则：[.cursor/rules/luban-config.mdc](.cursor/rules/luban-config.mdc)
- 技能轴：只改 `Assets/Editor/SkillSequences/` 预制体，再 Flux 导出。禁止手改 `SkillDataScriptable`。规则：[.cursor/rules/skill-timeline.mdc](.cursor/rules/skill-timeline.mdc)
- 时间钟：[Docs/ActTimeEffectsBacklog.md](Docs/ActTimeEffectsBacklog.md)
- Buff：[Docs/ActBuffLearningBacklog.md](Docs/ActBuffLearningBacklog.md)
- 主动技数字：[Docs/ActSkillConfigAndLeveling.md](Docs/ActSkillConfigAndLeveling.md)
- 相机意图/构图：[Docs/ActCameraStage2Design.md](Docs/ActCameraStage2Design.md)
- 敌人 AI（导演 / HFSM / 选招）：[Docs/ActEnemyAiDesign.md](Docs/ActEnemyAiDesign.md)
- 战斗缺口与里程碑（失衡/异常/招架/连携）：[Docs/ActCombatRoadmap.md](Docs/ActCombatRoadmap.md)
- Unity 热路径性能：仓库根目录 `.cursorrules`
