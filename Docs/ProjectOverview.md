# ACTGameEditor 项目介绍

> 本地 Unity 6 第三人称动作战斗工程：把《绝区零》《鸣潮》的「可读招、有段落、能换人」战斗循环接到可扩展的运行时上。不是完整网游客户端，也不是引擎插件。当前可玩：**1 名主控 + 两人候场 + 若干敌人** 的技能编辑场景。

**对照基准**：绝区零为主（进攻权、黄/红预警、招架与支援、小队换人）；走跑电机对照鸣潮子集；敌人高潮 = 偏谐 + F 谐度破坏（[已完成/ActHarmonyBreakDesign.md](已完成/ActHarmonyBreakDesign.md)）。**绝区零失衡硬直 / 连携窗已屏蔽。**

**明确不学**：全局 `Time.timeScale` 魔女时间、鸣潮收缩金圈弹刀、双人同屏操作、空战型角色、锁头飞弹。

---

## 〇、快速上手（60 秒）

1. 环境：**Unity 6 + URP**，无第三方 SDK。
2. 打开 `Assets/Scenes/SkillEditor.unity`，点 Play。
3. 战斗键：`J` 普攻连段 / `K` 闪避 / `L` 战技 / `I` 大招 / `Q`·`E` 换人·支援·招架 / `F` 谐度破坏（敌人偏谐条满时）/ `Space` 跳。
4. 调试键（Play 中）：`F2` 刷杂兵 · `F3` 刷精英 · `F4` 测试小队 · `Tab` 锁定 · `F9` 技能组满级。
5. 两分钟路线：普攻连段 → 极限闪避（时空断裂）→ 黄闪时按 Q 招架撞刃 → 攒满偏谐按 F → 大招时停。

> 键位与招式包规则的权威文档：[ActSkillKitConfig.md](ActSkillKitConfig.md)。

---

## 一、它解决什么问题

商业 ACT 的手感通常散落在动画、时间缩放、伤害公式和 AI `if` 里。本项目把这些拆成几条硬管道，让「改一招」和「改一条规则」走固定入口：

| 你想改的 | 走哪 |
|---|---|
| 伤害倍率、CD、Buff、打断等级、失衡贡献 | Luban Excel → 导出 JSON |
| 何时出盒、位移、动画、连招窗、预警 Msg | Flux Sequence 预制体 → 导出 SkillData |
| 谁能出招、何时出招、出哪招 | 导演发牌 + 敌人 HFSM + 效用选招 |
| 顿帧、断裂、闪白、残影、震屏 | 表现包，不进伤害结算 |
| 冻结 / 眩晕 / 沉默 | Buff 列表 + Tag，不写 `if (异常名)` |

技能编辑器（Flux 工作台）和战斗运行时共用同一套轴语义：编辑器里调的盒和 Msg，进 Play Mode 就是同一条 XC 时间轴。

---

## 二、技术栈

| 层 | 选型 |
|---|---|
| 引擎 | Unity 6，URP |
| 战斗内核 | 自研 Entity/Component（`EGamePlay`），**不是** Unity DOTS / ECS |
| 技能轴 | XC 运行时 + Flux 时间轴编辑器 |
| 数值表 | Luban：xlsx 源表 → JSON + 生成 C# |
| 输入 | Unity Input System，`ConfigurableInputManager` |
| 异步 | 战斗 Tick 外优先 UniTask；热路径禁止无界协程和 LINQ |
| 物理 | 手动 `Physics.Simulate`，步长乘世界钟 |

命名空间：内核 `EGamePlay` / `EGamePlay.Combat`；ACT 落地 `ACTGameEditor`。内核只认 `ICombatUnit`，不引用 `ActPlayer`。

---

## 三、仓库地图

| 路径 | 职责 |
|---|---|
| `Assets/Scripts/EGamePlay/` | Entity、Action、Buff、伤害乘区、Tag、四根钟接口 |
| `Assets/Scripts/EGamePlay.Unity/` | 动画、电机等对 Unity 的适配 |
| `Assets/Scripts/ACTGame/` | `CombatEntity`、技能轴会话、小队、招架、失衡、导演 AI、Locomotion、相机、渲染 |
| `Assets/Scripts/XCSkillEditor/` | XC 轴运行时与部分编辑器 UI |
| `Assets/Scripts/Flux/` | 时间轴编辑器；技能工作台在 `Flux/Editor/Workbench` |
| `Tools/Config/Datas/` | Luban **源表**（xlsx） |
| `Assets/Editor/SkillSequences/` | 技能轴 **源**（Flux 预制体；玩家在 `MainPlayer/`，敌人在 `EmenyPlayer/`） |
| `Assets/Game/Config/SkillDataScriptable/` | 轴导出产物，禁止手改 |
| `Assets/Resources/Config/Luban/` | 表导出 JSON，禁止手改 |
| `Docs/` | 专题约定与进度（时间 / Buff / 技能 / AI / 小队 / 走跑） |

入口：`EGamePlayInit`（`DefaultExecutionOrder(100)`）。`Awake` 建 ECS 根、`CombatContext`、战局导演、`CombatSquad`、对象池；`Start` 注册输入后 `PlayerManager.AddTruePlayer()`（同一预制体 ×3 绑进三槽）。

---

## 四、运行时主循环

```
EGamePlayInit.Update
  TimeScaleEffectManager.Tick
  GameTimeManager.Tick                         // 合成四根钟
  ETTimerManager ← WorldDelta                  // Buff / 定时器
  ConfigurableInputManager                     // 采样；Z 在这里拦下，不进技能缓冲
  CombatEncounterDirector.Tick(WorldDelta)     // 发/收 Token、节奏、站位
  CombatContext.Update
      SpellSession / HitPipeline.Flush
      每个 CombatEntity.Update(CombatTimeClock)
          CombatStateDirector                  // 唯一写 CurState
          玩家：技能输入 → Enqueue
          敌人：HFSM → 选招 → Enqueue
          ActSpellComponent                    // Gate → Session → XC 轴
```

`FixedUpdate` 里世界未暂停才 `Physics.Simulate(fixedDelta × WorldScale)`。

行为态优先级（只允许 `CombatStateDirector` 写）：

**Dead > Control（硬控 / 失衡硬直）> Hit > Skill > Locomotion**

出招唯一口：`ActSpellComponent.Enqueue` → `AbilityActivationGate` → `ActSpellSession` 占轴 → XC 播完。禁止 AI 或 Buff 直接 `Animator.Play` 绕过门控。Enqueue 之后大脑停选招，直到轴结束（开招即承诺）。

命中与结算：

```
时间轴开盒
  → HitPipeline 入队 / Flush
  → DamageAction / AddStatusAction（乘区、段表、Buff Dispatch）
  → CombatStateDirector 更新行为态
  → CombatPresentationDirector 播表现包
```

表现、镜头、粒子 **不准** 写进 `DamageAction`。

---

## 五、四根钟

不用 `Time.timeScale` 做战斗减速。选层用 `CombatTimeClock`（玩家层或世界层 × 实体 `TimeScale`）。

| 钟 | 用途 |
|---|---|
| 世界 `WorldDelta` | 敌人逻辑、物理、Buff / 点燃计时、对象池、导演 |
| 玩家 `PlayerDelta` | 本地玩家走跑、技能轴、CD、动画 |
| 相机 `CameraDelta` | 跟镜头；断裂 / 顿帧里保持 1 |
| 实体 `GetTimeScale()` | 只乘在宿主（冻结=0、HitStop、单体减速） |

同一角色的动画、位移、技能轴、CD 必须同一根层钟，再乘实体倍率。**Buff 持续不乘实体钟**：冻住了点燃仍跳。

已接到角色上的时间效果：

- **时空断裂**（极限闪避）：世界慢，玩家仍能出招、转 CD。
- **SkillTimeStop**（大招时停）：世界停，发起者的轴继续。
- **HitStop**：攻受短脉冲，按段表 `HitReaction` 分轻重，不把无关单位拖死。
- **冻结**：实体钟=0 + 冰壳外观；跳伤仍走世界钟。

---

## 六、已落地的战斗玩法

### 6.1 主动技与段表

主动伤害 **只查段表** `(SkillId, SegmentIndex)` + 技能组等级。时间轴只填何时开盒、盒形状、**段号 ≥ 1**、HitGroup，不填倍率。

- 轻重手感读段表 `HitReaction`（`Light` / `Heavy`）。
- 断招只比两个 int：出手 `InterruptLevel` ≥ 目标抗打断才进 Hit 并断招。`Buff.UnStopped` 给抗打断加值，不是布尔免疫；硬控 Buff 仍无视抗打断。
- 玩家连招（如 11001–11003）共用一个升级组；敌人招在 **12000 号段**，不进玩家槽表。

### 6.2 输入与招式包

场上角色只有 **4 个战斗键**，候场另有 **2 个 Z**：

| 逻辑 | 默认键 | 作用 |
|---|---|---|
| Attack（X） | J | 普攻；Hold 走蓄力行（有配才吞点按） |
| Dodge（Y） | K | 闪避（技能轴，不是走跑状态） |
| Ultimate（A） | I | 只出终结技 |
| Skill（B） | L | 战技 / EX |
| Assist1 / Assist2（Z） | Q / E | 换人 / 黄闪招架 / 红闪回避 / 支援（连携已屏蔽） |
| Jump | Space | 不进招式包 |

Z **不进** `InputBuffer`。`ConfigurableInputManager` 调 `CombatSquad.TrySwitchRelative`。招架不是场上按 L。

槽位主键 `(CharacterId, Button, Press, FormId, Priority)`，空配置就是没有，不回退系统默认技能。同键 Click / Hold 互斥。任意槽位可配 `EmpoweredSkillId` 做资源/Tag 预检改写。连招链不进 Excel，运行时从轴 `SkillInputEvents` 收集。

现役三槽仍是 **同一套预制体 ×3**，先跑通换人，角色分模后置。

### 6.3 小队换人（M4.0–M4.2）

场上只操作一人；候场隐藏并暂停。同屏最多再留 **1 个退场中** 的人把当前轴打完。

换人优先级：

`黄闪防御支援 > 红闪回避支援 > 支援窗快速支援 > Manual`（失衡连携已屏蔽）

| 模式 | 行为 |
|---|---|
| Manual | Warp 进场后 Idle，**不打一刀** |
| 快速支援 | 受击或轴上 `AssistCue` 开窗，换入打入场技 |
| 黄闪 | 来刀开窗，候场 Z 成交吸附，打 Kit 防御支援轴 |
| 红闪 | 换入带无敌帧闪过，不走招架成交 |
| 连携 | **已屏蔽**（原失衡窗 Z）。高潮见偏谐文档 |

团队 **支援点 3 点**：黄/红各耗 1，快速支援不耗。没点则黄/红 Z **整次失败**，不降级成 Manual。点数挂小队实体，开战灌满，惰性回复。

尚未做：延奏 `Buff.Outro` 转移、喧响团队大招条。现役 CharacterId=1 的 Quick/Evasive 列若为 0，红闪与支援窗内 Z 失败是配表预期。

### 6.4 偏谐与原失衡

**偏谐步骤 1–4 已接**：命中攒条，满了亮 F；场上按 F 打 `CharacterKit.HarmonyBreakSkillId`（空列不触发）。结束后真空锁零约 5s。普攻连招记忆已接。处决走独立表现包与构图镜头，HUD 只提示 F。见 [已完成/ActHarmonyBreakDesign.md](已完成/ActHarmonyBreakDesign.md)。

原失衡硬直仍关（`DazeGameplayEnabled=false`）。Q/E 不因条满连携。表 `DazeSetting` / `DazeRatio` 继续当偏谐数字。

属性异常积蓄 → 紊乱 **未做**；仍计划复用同一计量组件，不要再写第二套条。

### 6.5 黄红闪与预警

敌人开招可用轴 Msg `AiTelegraph`（或选招侧占位）发 **黄闪（可招架）/ 红闪（必须闪）**。学绝区零，不做鸣潮金圈。预警视觉目前是占位十字/色块，正式特效包还没接 `CombatTelegraph.Shown`。

极限闪避走表现包 `DodgeTimeFracture` / `DodgePerfect`（断裂 + 残影 + 灰屏）。闪避反击（极限闪后按 X）的 Tag 授予尚未收口。

### 6.6 敌人 AI

三层，都不进伤害结算：

1. **导演** `CombatEncounterDirector`：谁能打（Token 预算，同屏真伤轴大约 1～2 只）、Tempo、站位槽。难度只改预算/冷却/拍卖周期，不改伤害表。
2. **HFSM** `EnemyBrainComponent`：Engage / Recover / Relax 等战术生活；强制态（死、硬控、失衡）优先同步。
3. **效用选招** `EnemySkillSelector`：合法招里选一次 `Enqueue`，然后把轴打完。

已落地：杂兵/精英档、车轮战与徘徊、突进、红闪、连段、Special、假前摇、轴上预警。屏外公平（看不见的怪不准偷袭）和 Boss 相位切招池尚未做。敌人动画仍借用玩家剪辑，观感是已知缺口。

### 6.7 Buff 与控制

- 流程问 Buff 列表和 Tag，经 `CombatBuffPipeline.Dispatch`。
- 同一 BuffId 默认一条，重复走叠时/刷新/叠层/互斥。硬控（`Buff.MoveForbid`）跨 Id 再走 `HardControlMutex`：高 Priority 覆盖。冻结 Priority 高于眩晕，不会被短眩晕顶掉。
- 沉默只 `Buff.SkillForbid`，仍可闪避；硬控不可闪。
- 上 Buff 走 `AddStatusAction` + 免疫/抵抗；测试面板直挂会绕过免疫。
- 被动 = 常驻 Buff（`PassiveSkillBuffComponent` + 表 `PassiveBuffIds`）。
- 护盾已落 Vital 多段；协奏/延奏类转移未做。
- 无敌帧 / 霸体走轴上 Tag（`Buff.Roll`、`Buff.UnStopped`），不用 Buff 模拟。

---

## 七、位移、镜头与表现

### 7.1 走跑

逻辑位移已经按鸣潮子集写好：镜头相对摇杆、走/慢跑/快跑、Shift 锁存疾跑、松开粘性、锁敌绕圈、快跑反向 Pivot、跳跃土狼/缓冲/落地顿。水平位移三通道互斥：`Locomotion` / `RootMotion` / `SkillCurve`（`MotionDirector`）。技能占 Token 时走跑让路。闪避仍是技能轴，结束按方向可接疾跑。

动画层还是短 CrossFade 循环片，**带过渡的步态机（起步/急停/八向/锁定侧移）未开工**。争议动画资源已从仓库清除，新片需要自有资产再接到 Controller。计划见 `ActLocomotionWuWaPlan.md`。

### 7.2 相机

Stage 0/1 已落地：`CameraManager` 意图栈 + 优先级仲裁 + LateUpdate 唯一写 Transform；锁定与自由视角互斥切换；震屏是基准之外的叠加层。断裂/顿帧时相机钟保持 1，镜头不跟着世界停。

Stage 2（锁定与自由视角权重混合、自动构图、分曲线接管）是设计备忘，未排期。

### 7.3 表现包

`CombatPresentationDirector` 按 `CombatFxPackageId` 播一组有序效果（HitStop、闪白、震屏、残影、时停、溶解……）。目录可挂在 `EGamePlayInit` 上。

已占用的包包括：轻/重受击与命中、玩家被打震屏、普通闪避残影、极限闪避断裂、招架成功撞刃、破衡、切人入场/退场、连携、大招时停、死亡溶解。异常爆发包名为占位，紊乱系统未接。

### 7.4 画面实验

URP 上还有一组与战斗结算解耦的画面开关（调试键挂在编辑器场景，不进热路径）：扫描揭示、深度视觉、近距 dither、玩家雾、残影材质等。它们是表现沙盒，不是关卡系统。

---

## 八、两套配置，不要混改

**数字**（倍率、CD、Tag、`InterruptLevel`、Buff、Kit/槽位、小队窗、失衡档）：

1. 改 `Tools/Config/Datas/*.xlsx`
2. 在 `Tools/` 跑 `gen_code_json.bat`（或 Unity **Tools/配置/生成技能配置**）

禁止手改 `Assets/Resources/Config/Luban/*.json` 和生成的 `Assets/Scripts/EGamePlay/Config/Luban/*.cs`。

**时间轴**（盒、动画、位移、Msg、GrantTag、连招窗）：

1. 改 `Assets/Editor/SkillSequences/` 下对应 SkillId 预制体
2. Flux 保存 / 技能工作台导出，覆盖 `SkillDataScriptable`

禁止手改导出的 `.asset`。Unity 菜单 **技能工作台** 从列表打开轴、Unpack 编辑、保存回预制体并导出。

号段习惯：玩家技能 11000 段，敌人 12000 段，连携等系统技 13000 段。新建轴默认在 Sequence 根目录，已整理的在 `MainPlayer/`、`EmenyPlayer/`。

---

## 九、技能编辑器怎么用（给人）

1. 打开技能工作台，按 Id 打开 Sequence。
2. 在 Flux 里改轨：动画、位移、碰撞盒、`FPlayMsgEvent`（如 `AiTelegraph`、`AssistCue`）、输入窗。
3. 保存导出。运行时 `ActSkillTimelineLoader` 读 SO，不再读预制体。
4. Play Mode 用编辑器场景里的调试键刷怪、看 Buff 条、打技能组满级等（具体绑在 `SkillEditorScene`，`#if UNITY_EDITOR`）。

AI / 协作者不要手改巨大预制体 YAML，也不要开 Unity 代点；需要改轴时按仓库约定写出「技能编辑器待改」清单。

---

## 十、热路径约定（摘要）

`Update` / `FixedUpdate` / `LateUpdate`、HitFlush、Buff Dispatch、伤害乘区：

- 禁止 LINQ、热路径 `GetComponent`、装箱、每帧 `new List`
- Animator / Shader 属性用缓存 hash / `PropertyToID`
- 物理用 NonAlloc
- 导演容量固定（16），无每帧分配

更细条目见仓库根目录 `.cursorrules`。

---

## 十一、现在能感到什么、还缺什么

**已经能感到的：**

- 普攻连段、闪避、极限闪避断裂、顿帧分轻重
- 敌人轮流出手，黄刀可招架、红刀必须闪
- 三人槽换人：手动无斩击；黄闪换人撞刃；合轴退场（失衡连携已关）
- 敌人偏谐条可攒满，亮 F；按 F 打 Kit 破坏技，结束后条锁零一会儿
- 冻结停动作不停跳伤；硬控互斥
- 技能轴可在编辑器里改并立刻进战斗

**明显还缺的：**

| 项 | 状态 |
|---|---|
| 属性异常 / 紊乱 | 未做 |
| 延奏 Buff 转移、喧响大招条 | 未做 |
| 走跑步态机与自有移动动画 | 电机有、片子未接 |
| 相机自动构图 | 未排期 |
| 屏外公平、Boss 相位 | 未做 |
| 敌人专用动画、预警正式特效 | 占位 |
| 养成 UI、多角色分模 | 后置 |
| 弹道 / 空战 | 明确不做 |

专题进度以各文档文内勾选为准；`ActCombatRoadmap.md` 的总表有滞后，以代码和 `ActSquadDesign` / `ActEnemyAiDesign` / `ActSkillKitConfig` 的状态栏为准。**未完成项统一索引：[ActUnfinishedIndex.md](ActUnfinishedIndex.md)**。

---

## 十二、建议阅读顺序

1. 本文（全景）
2. [ProjectConventions.md](ProjectConventions.md)（硬约定，改代码前必读）
3. `EGamePlayInit` → `ActPlayer` / `CombatEntity`
4. [已完成/ActTimeEffectsBacklog.md](已完成/ActTimeEffectsBacklog.md)（四根钟，已归档；P3 时缓场方案未接）
5. `ActSpellSession` → HitPipeline → `DamageAction`
6. 按需求看：[ActSkillKitConfig.md](ActSkillKitConfig.md)、[ActSquadDesign.md](ActSquadDesign.md)、[ActEnemyAiDesign.md](ActEnemyAiDesign.md)、[ActBuffLearningBacklog.md](ActBuffLearningBacklog.md)

配表步骤：[.cursor/rules/luban-config.mdc](../.cursor/rules/luban-config.mdc)。改轴步骤：[.cursor/rules/skill-timeline.mdc](../.cursor/rules/skill-timeline.mdc)。
