# ACT 项目规范

给读代码、改玩法和接需求用。专题进度（冻结、技能升级、Buff 清单）在文末链接里，本文只定 **目录、分层和硬约定**。

---

## 这是什么项目

Unity ACT：本地玩家 + 敌人，技能走 XC 时间轴，战斗数值走 EGamePlay（自研 Entity/Component，**不是** Unity DOTS）。对照鸣潮 / 绝区零的手感，不用 Unity 全局 `Time.timeScale` 做魔女时间。

入口：`EGamePlayInit`（`DefaultExecutionOrder(100)`）创建 ECS 根、`CombatContext`、`ETTimerManager`，再 `PlayerManager.AddTruePlayer()`。

---

## 建议阅读顺序

1. `EGamePlayInit`：主循环（时间 → 输入 → `CombatContext.Update`）。
2. `ActPlayer` / `CombatEntity`：场景物体怎么挂上战斗实体。
3. `CombatTimeClock` + `Docs/ActTimeEffectsBacklog.md`：四根钟。
4. `ActSpellSession` → XC 时间轴 → `HitPipeline.Flush` → `DamageAction`。
5. `StatusComponent` + `CombatBuffPipeline` + `Docs/ActBuffLearningBacklog.md`。
6. 配表：`Docs/ActSkillConfigAndLeveling.md`，改数字见 `.cursor/rules/luban-config.mdc`。

---

## 仓库地图

| 路径 | 职责 |
|---|---|
| `Assets/Scripts/EGamePlay/` | 战斗内核：Entity、Action、Buff、伤害乘区、Tag、时间钟接口 |
| `Assets/Scripts/EGamePlay.Unity/` | 动画 / 电机等对 Unity 的适配 |
| `Assets/Scripts/ACTGame/` | ACT 落地：`CombatEntity`、技能轴、表现包、Locomotion、Rendering、Manager |
| `Assets/Scripts/XCSkillEditor/` | XC 技能轴运行时 + 编辑器 UI（含 `BuffBarTester`） |
| `Assets/Scripts/Flux/` | 时间轴编辑器运行时，少改 |
| `Tools/Config/Datas/` | Luban **源表**（xlsx） |
| `Assets/Resources/Config/Luban/` | 导出 JSON，禁止手改 |
| `Assets/Scripts/EGamePlay/Config/Luban/` | 导出 C#，禁止手改（`Buff.cs` 里 BuffDemoSetting 的 partial 除外） |
| `Docs/` | 专题约定与进度 |

命名空间：内核 `EGamePlay` / `EGamePlay.Combat`；ACT 层 `ACTGameEditor`；部分 UI 仍是 `XiaoCao`。新代码跟所在目录走，不要再开一套平行战斗对象。

---

## 分层

```
输入 ConfigurableInputManager
  → ActPlayer / AbilityActivationGate
  → ActSpellSession 占轴（XC 时间轴）
  → 盒体申报 HitPipeline → Flush
  → DamageAction / AddStatusAction
  → StatusComponent 列表 + CombatBuffPipeline.Dispatch
  → CombatStateDirector 写成唯一行为态
  → 表现 CombatPresentationDirector / Bridges（不要在结算里播特效）
```

- **EGamePlay.Combat** 只依赖 `ICombatUnit`，不要引用 `ActPlayer`。
- 表现、电机、Shader 留在 `ACTGame`。
- 流程问 Buff 列表和 Tag，不要写 `if (着火)` / `if (冻结)` 散落各处。冻结外观由 `Buff.Freeze` 叶子计数回调到 `NotifyFreezeChanged`。

---

## 逻辑硬约定

### 时间

四根钟，不要加第五根。选层用 `CombatTimeClock`（玩家层或世界层 × 实体 `TimeScale`）。

| 钟 | 用途 |
|---|---|
| 世界 `WorldDelta` | 敌人逻辑、物理、**Buff / 点燃计时**、对象池 |
| 玩家 `PlayerDelta` | 本地玩家走跑、技能轴、CD、动画 |
| 相机 `CameraDelta` | 跟镜头；断裂 / 顿帧里保持 1 |
| 实体 `GetTimeScale()` | 只乘在宿主（冻结=0、HitStop、单体减速） |

同一角色的动画、位移、技能轴、CD **同一根层钟**，再乘实体倍率。Buff 持续 **不乘** 实体钟（冻住了点燃仍跳）。禁止用 `Time.timeScale` 做战斗减速。细节与清单：`ActTimeEffectsBacklog.md`。

### 状态

`CombatStateDirector` 是行为态唯一写入口：Dead > Control > Hit > Skill > Locomotion。硬控认 `Buff.MoveForbid`；沉默只 `Buff.SkillForbid`（仍可闪避）；冻结另推 `Buff.Freeze`（实体钟=0 + 冰壳）。霸体是 Tag，跳过短硬直，不挡硬控 Buff。

### Buff

- 同一 BuffId 默认一条，重复走 `RepeatedAddition`（叠时 / 刷新 / 叠层 / 互斥）。
- 上 Buff 走 `AddStatusAction` + `PreGive/PreReceive` + `StatusApplyResolver`（免疫 / 抵抗）。测试面板 `AttachStatus` 是直挂，**绕过**免疫。
- 卸走统一 `RemoveStatus(id, BuffRemoveReason)`。效果锁中的添加要入队。
- Modify 槽位：`Assets/Scripts/EGamePlay/Combat/Buff/BuffModify/EffectModifyParamSlots.md`。
- 异常积蓄 / 削韧不要做成普通 TimeBuff。清单：`ActBuffLearningBacklog.md`。

### 主动技伤害

只查段表 `(SkillId, SegmentIndex)` + 技能组等级。时间轴只填何时开盒、盒形状、**段号 ≥ 1**、HitGroup，不填倍率。不要回退全局 BuffModify 当主动技伤害。详见 `ActSkillConfigAndLeveling.md`。

---

## 代码约定

热路径（`Update` / `FixedUpdate` / `LateUpdate`、HitFlush、Dispatch、伤害乘区）：

- 禁止 LINQ、禁止热路径 `GetComponent`、禁止装箱和临时 List（复用或调用方传入）。
- Animator / Shader 属性用缓存的 hash / `PropertyToID`。
- 物理用 `NonAlloc`。
- 新公共 API 写 `///`，注释写为什么。

战斗侧额外：

- 组合优于新继承层；数据在组件上，流程在 Action / Pipeline。
- 异步优先 UniTask，不要在战斗 Tick 里开无界协程。
- 调试 UI（如 `BuffBarTester`）可以 OnGUI，不要把 OnGUI 带进战斗热路径。

更细的 Unity 性能条目见仓库根目录 `.cursorrules`。

---

## 配置

数字只改 `Tools/Config/Datas/*.xlsx`，然后在 `Tools/` 执行 `gen_code_json.bat`（或 Unity **Tools/配置/生成技能配置**）。完整步骤：`.cursor/rules/luban-config.mdc`。

运行时读表：`SkillSettingMgr` → `Tables`。缺 Id 时部分 Get 会 **回退到表第一行**，测试代码必须校验 `setting.BuffId == 请求Id`。

---

## 明确不要做

1. 手改导出 JSON / 生成的 Luban C#。
2. 用全局 `timeScale` 做断裂、顿帧、冻结。
3. 把点燃 / Buff 计时绑到实体 TimeScale。
4. 在 Buff 回调里改 XC 技能轴；形态用 Form / 槽位表。
5. 在 `DamageAction` / Resolver 里直接播镜头和粒子；走 `CombatPresentationDirector`。
6. 为「一个新异常」新建平行的 Buff 运行时类型；先加 Tag + Modify + 表行。

---

## 专题文档

| 文档 | 内容 |
|---|---|
| `Docs/ActTimeEffectsBacklog.md` | 世界/玩家/相机/实体钟，断裂、HitStop、冻结 |
| `Docs/ActBuffLearningBacklog.md` | Buff 管线、护盾、控制、未做项 |
| `Docs/ActSkillConfigAndLeveling.md` | 主动技段表与技能组升级 |
| `Assets/Scripts/EGamePlay/Combat/Buff/BuffModify/EffectModifyParamSlots.md` | Modify 各类型 Param 槽位 |
