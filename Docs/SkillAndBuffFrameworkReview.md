# 技能与 Buff 框架回顾与改进建议

本文档基于当前代码库梳理技能与 Buff 的完整流程，并给出可改进/优化点（含大改方向），便于后续完善框架。

---

## 一、当前流程梳理

### 1.1 技能流程（主动技能）

```
SpellComponent.Update
  → 每帧从 _spelllist 选最高优先级且 TriggerFormula 通过的技能
  → SpellWithTarget(ability, target) / SpellWithPoint(ability, point)
  → SpellActionAbility.TryMakeAction → SpellAction
  → SpellAction.SpellSkill()
       → PreProcess (PreSpell ActionPoint)
       → 按 SkillData.skillAllEventDatas 创建多个 XCNewEventsRunner（子轨道）
       → StartRuner 往每个 Runner 挂 XCTriggerEvent / XCAnimEvent / ... 
       → ActSkillRunner.StartUpdate()，Creator.SpellingExecution = runner
  → ActSkillRunner.Update
       → 遍历 SubRuners (XCNewEventsRunner).Update
  → XCNewEventsRunner.OnUpdate
       → 按 _currentFrame 驱动 _updateTrack 里各 XCEvent 的 OnTrigger / UpdateEvent / OnFinish
  → 碰撞/触发事件（如 XCTriggerEvent）回调
       → XCNewEventsRunner.OnTriggerEvent(CollisionAction) 
       → TriggerEffectList(effectIds, target) / RemoveTriggerEffectList(effectIds, target)
  → TriggerEffectList 内：
       → ability.Definition.EffectDatas + EffectProcessorTable.Execute(typeId, owner, ctx)
       → 或 InlineBuffEffects → SkillInlineBuffEffectProcessor.Execute(setting, caster, target)
  → SpellAction 检测 ActSkillRunner.State == Finish → PostProcess (PostSpell) → FinishAction
```

**配置来源**：Luban SkillDemoSetting（EffectIds、TriggerFormula 等）、AbilityDefinition（预构建 EffectDatas、InlineBuffEffects）、SkillAllEventData（事件轨道）、XC 事件里 EffectIds。

### 1.2 Buff 流程

```
AddStatusAction.ApplyAddStatus
  → statusComp.AttachStatus(statusId) → Entity.AddChild<Buff>(buffId)
  → Buff.Awake(initData)
       → Setting = SkillSettingMgr.GetBuffDemoSetting(BuffID)
       → 按 BuffType 挂 BuffTimeComponent / BuffTriggerComponent / BuffFrequencyComponent / BuffModifyComponent 等
  → buff.Caster = Creator; buff.ActivateBuff()
  → Buff.OnActivate：StatusComponent.TagContainer.AddTag(BuffTag)
  → BuffTriggerComponent.OnEnable：ListenActionPoint(ActionPointType, buff.OnEvent)
  → BuffTimeComponent：定时 OnTick/OnStart 回调 buff.OnEvent
  → buff.OnEvent(Entity) / OnEvent()
       → BuffStateCheckComponent.CheckTargetState（若有）
       → BuffModifyComponent.OnTriggerModify(target) → BuffModifyProcessorTable.ApplyOnTrigger(reg, buff, target)
  → 移除：RemoveStatus(buffId) / Buff.CheckIsCanRemove → DeactivateBuff → 下一帧回收
```

**配置来源**：Luban BuffDemoSetting（BuffType、BuffModifyList、TriggerFormula 等）、BuffModifySetting（EffectModifyType + Param 槽位）。

### 1.3 效果执行统一入口

- **技能轨道效果**：`EffectProcessorTable.Execute(EffectTypeId, owner, TriggerContext)`（Damage/Cure/AddStatus）或 `SkillInlineBuffEffectProcessor.Execute`（PlayerModify / SkillHpDamage / SkillResource）。
- **Buff 触发效果**：`BuffModifyProcessorTable.ApplyOnTrigger`（PlayerControll / PlayerModify / BuffHpDamage / BuffResource / CurveEffect 等），目标由外部传入。

---

## 二、存在的问题与风险

### 2.1 稳健性

| 问题 | 位置 | 建议 |
|------|------|------|
| `StatusComponent.RemoveStatus(buffId)` 直接用 `IdStatuses[buffId]` 索引 | StatusComponent.cs | 调用前必须 HasBuffId，或 RemoveStatus 内先 TryGetValue/ContainsKey 再操作，避免 KeyNotFoundException。 |
| `GetBuffById(BuffId)` 同样直接索引 | StatusComponent.cs | 改为 TryGetBuffById 或返回 null 的 safe get，避免未命中抛异常。 |
| Buff 内 `GetComponent<BuffTimeComponent>()` 等未判空 | Buff.cs OnEvent | 若某 Buff 未挂对应组件（配置错误）会 NRE，建议 TryGet 或提前在 Awake 缓存。 |
| SpellAction 未校验技能消耗（如能量） | SpellAction.SpellSkill | 若要做“释放前扣资源”，应在 PreProcess 后、创建 Runner 前做消耗校验与执行（见前文 ConsumeResourceAction 方案）。 |

### 2.2 性能

| 问题 | 位置 | 建议 |
|------|------|------|
| Buff 在 OnEvent 中多次 GetComponent | Buff.cs | 在 Awake 或首次使用时缓存 BuffTimeComponent、BuffFrequencyComponent、BuffModifyComponent 等，避免每帧/每次触发 GetComponent。 |
| SpellComponent 每帧对 _spelllist 中每个技能执行 TriggerFormula | SpellComponent.Update | 公式结果可短时缓存（如本帧内相同 skillId 不重复执行），或把“可释放”状态收敛到少量逻辑。 |
| AbilityDefinition.Load 依赖 EffectDefinitionManager + ScriptableObject | AbilityDefinition.cs | 若已决定“全配置表驱动”，可把 Effect 配置迁到 Luban，用 EffectSettingManager 替代 EffectDefinition，减少 AB 加载与 SO 依赖。 |
| XCNewEventsRunner 每帧遍历 _updateTrack 且多次访问 HasFinished/HasTriggered | XCNewEventsRunner | 可考虑按 Start 帧分桶或 early-out，减少无效迭代；列表较大时再考虑按帧索引。 |

### 2.3 架构与数据一致性

| 问题 | 说明 | 建议 |
|------|------|------|
| 技能效果与 Buff 效果两套入口 | 技能走 EffectDatas + EffectProcessorTable + SkillInlineBuffEffectProcessor；Buff 走 BuffModifyProcessorTable | 保持“目标由上层选定、效果层只执行”的约定；长期可考虑统一“效果描述”（如统一用 BuffModifySetting 或扩展的 EffectSetting 表）和单一执行管线。 |
| AbilityTriggerComponent 大量注释 | 被动技能触发未启用 | 若需要被动技能，可恢复并改为用 Definition.EffectDatas + 统一目标选择；或与 Buff 被动共用一套 TriggerConfig + 目标解析。 |
| BuffAbilityTriggerComponent 的 effectDatas 为 null | Buff 被动效果未接数据 | 若要做 Buff 被动，需 BuffDefinition/ Buff 配置 EffectIds 并预构建 EffectDatas，再在此处接入。 |
| TriggerContext 仅部分字段被使用 | EffectConfig / SourceAbility / TriggerSource / Target / DamageSegmentIndex | 保持结构稳定；若增加“目标选择策略”或多目标，可扩展 Context 而不改 Effect 层。 |

### 2.4 配置与加载

| 问题 | 说明 | 建议 |
|------|------|------|
| Effect 仍依赖 EffectDefinition (SO) | AbilityDefinition.Load 用 EffectDefinitionManager.GetOrLoad(effectId) | 迁移到表驱动：Luban Effect 表 + EffectSettingManager，AbilityDefinition 只从表构建 EffectDatas。 |
| AbilityDefinition 同时拉 ConfigObject (AB) 与 Luban Config | 混合 AB 与 Luban | 明确“主数据源”：若以 Luban 为主，ConfigObject 仅保留编辑器/展示用，或逐步废弃。 |
| SkillDemoSetting 与 BuffDemoSetting 分散 | 技能与 Buff 各一套表 | 若需“技能/Buff 共用效果描述”，可抽公共 EffectModify 表或统一 Effect 表，避免重复语义。 |

---

## 三、可改进/优化点（按优先级）

### P0：稳健性（建议先做）

1. **StatusComponent**  
   - `RemoveStatus`：内部改为 `if (!IdStatuses.TryGetValue(buffId, out var buff)) return;`，避免 KeyNotFound。  
   - `GetBuffById`：改为 `TryGetValue` 返回 bool + out Buff，或返回 null 的 safe get。

2. **Buff**  
   - 在 Awake 或首次触发时缓存 `BuffTimeComponent`、`BuffFrequencyComponent`、`BuffModifyComponent`（以及 `StatusComponent` 的 TagContainer），在 OnEvent/GetFloatNumeric 中使用缓存，避免在热路径 GetComponent。

3. **Spell 消耗**  
   - 若需要“释放必杀消耗能量”：在 SpellAction.SpellSkill 中 PreProcess 之后、创建 Runner 之前，按技能配置做资源校验与扣除（建议用 ConsumeResourceAction，便于扩展与监听）。

### P1：性能与可维护性

4. **Effect 配置表化**  
   - 新增 Luban Effect 表（或扩展现有表），字段覆盖 EffectTypeId、Damage/Cure/AddStatus 等参数。  
   - 新增 EffectSettingManager（或 SkillSettingMgr 扩展），AbilityDefinition.Load 只从表构建 EffectDatas，不再依赖 EffectDefinitionManager + SO。

5. **Buff 组件缓存**  
   - 所有 Buff 内热路径（OnEvent、GetFloatNumeric、CheckIsCanRemove）统一走缓存引用，避免 GetComponent。

6. **SpellComponent**  
   - TriggerFormula 结果按 skillId 做短时缓存（同一帧内相同技能不重复计算），降低公式执行次数。  
   - `Entity.GetComponent<AbilityComponent>()` 在 Update 内每帧调用，可改为在 Awake 或首次使用时缓存 AbilityComponent 引用。

### P2：架构与扩展

7. **目标选择统一**  
   - 抽象一层“目标选择器”（TargetSelector）：输入（Caster、SkillTarget、HitTarget、可选范围等），输出 Entity 或 List<Entity>。  
   - 技能/Buff 在触发效果前调用 TargetSelector，再把结果写入 TriggerContext.Target（或 Targets），效果层只读 Context，不参与选择。

8. **被动技能与 Buff 被动统一**  
   - 若启用 AbilityTriggerComponent：用 Definition.EffectDatas + 统一目标选择。  
   - 若启用 Buff 被动：为 Buff 配置 EffectIds 或等效表，预构建 EffectDatas，BuffAbilityTriggerComponent 使用该数据，与技能共用 EffectProcessorTable 或统一执行入口。

9. **TriggerContext 扩展**  
   - 若支持多目标：增加 `List<Entity> Targets` 或 `Entity[] Targets`，Effect 层按需遍历。  
   - 若支持“目标点”：增加 `Vector3 TargetPoint`，由技能/Buff 在目标选择层写入。

---

## 四、大改方向建议（若愿意大改）

### 4.1 统一“效果描述”与“效果执行”

- **现状**：技能效果 = EffectIds → EffectData (Damage/Cure/AddStatus) + InlineBuffEffects (BuffModifySetting)；Buff 效果 = BuffModifySetting + EffectModifyType。  
- **大改**：  
  - 所有“效果”统一为一种描述结构（例如扩展的 **EffectSetting**：type + 参数槽位），技能与 Buff 都引用同一张表或同一套 ID。  
  - 单一执行管线：**EffectExecutor.Execute(EffectSetting, TriggerContext)**，内部按 type 分派到 Damage/Cure/AddStatus/Resource/Modify 等，不再区分“技能效果”与“Buff 效果”两套入口。  
- **收益**：配表一致、逻辑复用、扩展新效果类型只需加 type 与分支。

### 4.2 技能与 Buff 的“执行体”抽象

- **现状**：技能 = SpellAction → ActSkillRunner → XCNewEventsRunner + XCEvent；Buff = Buff 实体 + 组件 + OnEvent。  
- **大改**：  
  - 抽象 **IAbilityRunner** 或 **ISkillExecutionContext**：提供 Owner、InputTarget、当前时间/帧、触发效果接口（TriggerEffects(effectIds, target)）、目标选择接口。  
  - 技能与 Buff 的“触发效果”都通过同一套接口，内部再转 EffectExecutor，便于测试与复用。  
- **收益**：Buff 被动与技能被动可共用同一套“触发 + 目标选择 + 效果执行”流程。

### 4.3 配置与运行时彻底分离

- **现状**：AbilityDefinition、Buff 的 Setting、EffectDefinition 混合了“加载时配置”与“运行时引用”。  
- **大改**：  
  - **纯配置层**：全部来自 Luban（技能、Buff、效果、Trigger、目标选择参数），无 SO 依赖。  
  - **运行时层**：CombatEntity、Ability、Buff 只持有“配置 ID”和由配置预计算好的运行时结构（如 EffectDatas、ModifyRegistrations），不直接依赖 AB/SO。  
- **收益**：热更、测试、多服一致性好；编辑器可单独用 SO/Asset 做预览而不影响运行时。

### 4.4 事件与 Action 统一

- **现状**：Action 有 SpellAction、DamageAction、CureAction、AddStatusAction、CollisionAction 等；事件有 ActionPoint、XC 事件轨道。  
- **大改**：  
  - 明确“事件”只负责时机与上下文（何时、谁、对谁）；“Action”只负责对已确定目标执行一次行为（伤害、治疗、加 Buff、消耗资源等）。  
  - 资源消耗、打断、驱散等也做成独立 Action（ConsumeResourceAction、InterruptAction 等），由事件或技能流程触发，便于监听与扩展。  
- **收益**：行为可组合、可监听、易加日志与回放。

---

## 五、建议实施顺序（在不影响主流程的前提下）

1. **先做 P0**：StatusComponent 安全访问、Buff 组件缓存、可选 Spell 消耗。  
2. **再做 P1**：Effect 表驱动（替代 SO）、SpellComponent 公式缓存。  
3. **按需做 P2**：目标选择器、被动技能/Buff 被动统一、TriggerContext 多目标/目标点。  
4. **大改**：在 P0/P1 稳定后，再选 4.1～4.4 中一两条作为长期重构方向，分阶段落地。

以上内容可直接作为后续迭代与重构的参考；具体改哪些文件、如何改，可按你当前排期拆成小步提交。
