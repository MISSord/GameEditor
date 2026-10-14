# 偏谐条与谐度破坏（对照鸣潮 3.0）

> 状态：**步骤 1–4 已接运行时**（攒条 / Ready / F 出 Kit 破坏技 / 真空锁零 / 普攻连招记忆 / 处决镜头与正式 HUD）。绝区零失衡硬直 / 连携窗 **运行时已删除**。14001 轴仍是占位，需 Flux 换处决动画。
> 关联：`ActCombatRoadmap.md`（§一指向本文）、`ActSquadDesign.md`（Z 不再因条满走 Chain）、`ActSkillKitConfig.md`（键位）、`ProjectConventions.md`。
> 数字仍走 Excel 再导出。技能轴走 Flux 预制体。禁止手改 json / `SkillDataScriptable`。

---

## 〇、一句话

敌人一条计量，打满只表示 **场上角色可以按 F 打谐度破坏**。怪不硬直、不换人、不拆普攻连段。F 结束后条 **锁在 0** 一段时间。

绝区零那套「满条跪下 + 窗内 Q/E 连携」**运行时已删除**（不再用开关屏蔽）。

---

## 一、名称（社区 vs 官方）

社区常说「谐振条」。3.0 官方口径：

| 词 | 意思 | 本文用法 |
|---|---|---|
| 偏谐 | 敌人这条计量本身 | 条 / 计量 |
| 失谐 | 条满、可打破坏技 | 相位 `Ready` |
| 谐度破坏技 | 场上按 F 打的那一下 | 破坏技 |
| 真空期 | F 结束后一段时间不能再攒 | 相位 `Vacuum`，**锁零** |

不要和这些混名：协奏（切人能量）、共鸣回路 / Forte（角色自己的能量条）、喧响（小队终结技条）。

---

## 二、为什么换掉绝区零失衡

本项目原先对照绝区零做了 **失衡条满 → Stagger → 导演 Punish → 窗内候场 Z 打 `ChainSkillId`**。高潮绑在 **换人** 上：单人没候场，Z 整次失败；场上角色没有「条满按一键结算」的键。

实机对照鸣潮 3.0 大怪之后，要的高潮是另一套：

- 满条 **不是** 硬直窗
- 结算键是 **场上 F**，不换人
- 不按 F，战斗照常
- F 插进普攻连段，结束后继续下一段，不回到 1

两套高潮不要并存。失衡硬直 / 连携窗 **运行时已删除**（`CombatChainSkill`、`BeginDazeStagger`、导演 Punish、计量 Opened/Draining/Recover 已去掉）。表名 `DazeSetting` / `DazeRatio` 继续当偏谐数字。不要把连携代码加回来。

---

## 三、实机对照（已拍）

测的是鸣潮 3.0 **大怪手动 F**，不是协奏切人，也不是角色共鸣回路。

| 项 | 结论 | 对方案的约束 |
|---|---|---|
| 不按 F | 怪照常走、照常出招 | 满条 **禁止** `BeginDazeStagger`、禁止导演 Punish |
| 小怪自动破坏 | 攻略里 1C 可能自动。没测清是「条满立刻打」还是「再砍一刀才打」 | 第一期 **全手动 F**。表可预留 `ExecuteMode`，现在不要做自动 |
| F 会不会被普攻轴吞掉 | **不会**。普攻过程中按 F 能进 | 占用轴时 `interruptOnly` 不能把 F 丢掉 |
| F 和连招 | 普攻 3 → F → F 结束再按普攻 = **普攻 4**，不是 1 | F 是插入演出，**不重置连招记忆** |
| 真空 | **锁零**：期间加值无效，到期再从 0 开攒 | 不是掉条、不是起身无敌、不是 Recover i-frame |

### 3.1 「那一刀」是什么（上次没写清）

不是角色大招，也不是普攻额外一段。攻略里杂兵（1C）条满后往往 **不用按 F**，系统自己打一下谐度破坏。没写清的是触发时机：

- **立刻自动**：条一满马上打
- **下一刀自动**：条满只是亮着，你再砍中一下才打

大怪测的是手动 F，第一期全敌人都手动。杂兵以后要自动，用档位表加一列即可，不要写 `if (杂兵)`。

---

## 四、不要混进来的机制

| 机制 | 为什么不是这个 |
|---|---|
| 协奏满切人变奏 | 小队文档已否「能量条满才能延奏」；F **不换人** |
| 共鸣回路 / Empowered | 角色 Vital，走槽位 `EmpoweredSkillId`，不是敌人条 |
| 绝区零失衡连携 | 满条硬直 + 候场 Z 轮转。**运行时已删除** |
| 喧响 / 终结技 | 仍是 Ultimate 槽（A）。F 不产喧响；以后若要「高光涨条」，来源改绑这次 F，不绑连携 |
| 震谐 / 集谐 / 偏移 / 干涉 | 3.0 角色后置，禁止 `if (琳奈)` / `if (角色名)` |

---

## 五、目标循环

```
命中 / 招架涨条（复用现有段表 DazeRatio 与招架倍率，仅作计量）
  → 满：Ready（失谐）。UI 提示 F。怪 AI 不变，不进 Stagger
  → 不按 F：一直 Ready，战斗不受影响（第一期不超时灭条）
  → 范围内、场上角色按 F
        → Gate：大招 / 支援突击 / 已在播的破坏技 → 拒绝
        → 插入 Kit 破坏技（空列 = 这次失败，不换人、不回退 13001）
        → 当前普攻轴让路，连招记忆保留，记忆超时暂停
  → F 结束：Vacuum，Current = 0，Add 无效
  → 真空结束：Idle，从 0 再攒
```

单人调试：F 是场上角色自己的招，不依赖候场。Q/E「没人就失败」只适用于支援，不适用于破坏技。

第一期不做：Ready 超时灭条、杂兵 Auto、谐度破坏独立增幅公式、偏移/干涉。

---

## 六、和现有代码怎么接

计量继续挂 `CombatMeterComponent`（不是 TimeBuff、不进 `IdStatuses`）。**改相位语义**，不要再开第二条条。鸣潮异常走 Buff 叠层，不要给本组件再开元素积蓄槽（见 `ActBuffLearningBacklog.md`）。

### 6.1 建议相位

| 相位 | 行为 |
|---|---|
| `Idle` / `Charging` | 攒条。IdleRegen 可留（未满时慢慢掉） |
| `Ready` | 满条。可 F。怪正常。无易伤、无连携次数、无掉条硬直 |
| `Executing` | 正在播破坏技。可暂停真空计时，避免 F 播到一半进入真空 |
| `Vacuum` | `Current = 0`，拒绝一切 `AddDaze`。到期 → Idle |

**不要**再走已删除的：`Opened` → `BeginDazeStagger` → 导演 Punish → `IsChainWindow` → Z `SwitchReason.Chain`。

招架成功仍可给这条计量加值（当偏谐贡献）。`AddDaze` 涨条；满了进 Ready，不会 Open / Stagger。

### 6.2 伤害

第一期破坏技走自己的段表 + 独立来源标签（便于以后做成「只吃谐度破坏增幅、不吃攻击暴击」）。不要第一期分叉 `DamageAction` 公式。表现不进 `DamageAction`。

### 6.3 表现

- 满条：血条旁提示 **F**，不要提示「按 Q/E 连携」。Kit 空列不显示 F。范围内 F 脉冲更强。
- F 播独立包 `CombatFxPackageId.HarmonyBreak`（403）：实体钟 HitStop + 目标闪白 + 处决震屏。**不要**用 `ChainAttack` 冒充处决，**不要** `TimeFracture` 世界钟魔女时间。
- 镜头：`SkillCamera` 看向攻受连线偏目标侧，F 结束按同优先级取消。
- 真空：条锁 0 用暗色空槽，不把 F 留在条上。
- 四根钟不变，不要第五根钟。表现不进 `DamageAction`。

### 6.4 距离与失败

破坏技需要目标在范围内（`CombatHarmonyBreak.MaxRange` = 14m）。超距 / Kit 空列 / Gate 拒绝 = 这次 F 失败，条仍停在 Ready，不要误清条、不要换人。

---

## 七、输入与招式包

F 是 **情境键**，和 Z 一类，不是面键：

- **不要**第 5 个 `CombatButton`
- **不要**进 `CharacterSlot`
- InputAction 增 `Execute` / 谐度破坏，默认键盘 **F**
- 占用轴时仍要能进（`interruptOnly` 不能把 F 丢掉）
- 大招 / 支援突击 / 已在播的破坏技期间：Gate 拒绝，避免套娃
- 受击 / 硬控 / 死亡：不能 F（问 Tag / `CombatStateDirector`，不要 `if (异常名)`）

Kit 建议新列 `HarmonyBreakSkillId`（**不要**把 `ChainSkillId` 偷偷改语义，13001 连携轴先留着不用）。`SkillCategory` 新枚举 `HarmonyBreak`。空列 = 这次 F 失败。

Q/E Reason **删除失衡 Chain**。仍是：

`黄闪 DefensiveAssist > 红闪 EvasiveAssist > 支援窗 QuickAssist > Manual`

A 仍是终结技。F 不产喧响 / 大招能量。

---

## 八、连招记忆（普攻 3 → F → 普攻 4）

现在若把 F 当普通 `Enqueue`，会换掉当前轴，结束后 Idle，再按 X 就是槽位普攻 1。实机要的是插入，不是重置。

落地约定：

1. 破坏技 **不重置** 普攻连招记忆。
2. F **入队前**记下当前（或刚结束的）普攻 SkillId，以及它窗边下一发普攻 Id（例如 11002 → 11003）。窗边来自轴 `SkillInputEvents` 的 Attack×Click，与现有连招收集同一套，不进 Excel。快照必须在 `LaunchRunner` Break 当前轴之前。
3. F 期间连招预输入寿命 **暂停**（玩家钟仍走轴，但记忆超时与 InputBuffer 过期不减）。
4. F 结束后 **不自动** 打下一发；下一次 Attack×Click 若记忆仍有效，走下一发，否则走 Idle 槽位 1。F 轴 `IsMainFinish` 后摇期间也不要用槽位 1 顶掉记忆。
5. **会清记忆的**：闪避、受击进 Hit、硬控、换人、死亡。只有破坏技不清。战技 / 大招出手也会清（不接在「普攻下一段」上）。
6. 记忆只对 `SkillCategory.BasicAttack`（可含同一 Attack 键上的 Heavy/Dash 连段）。
7. 实现落点在 `IAttackPlayer` / `NormalActPlayer` 槽位提交，不要在 `DamageAction` 里改连招。

现役 CharacterId=1 普攻链是 **11001→11002→11003**（11003 轴没有下一发）。所以当前可验收的是 **1→F→2**、**2→F→3**；在 3 上按 F 再普攻会回到 1。要 3→F→4 需在 11003 轴加第四段窗边。

运行时字段在 `NormalActPlayer`（`HarmonyComboMemoryTimeout` 默认 0.45s，玩家钟，F 期间暂停）。

---

## 九、真空锁零

| 要 | 不要 |
|---|---|
| F 结束立刻 `Current = 0` | 慢慢掉条冒充真空 |
| Vacuum 内 `AddDaze` 直接 return | 加值攒着，真空结束瞬间爆满 |
| 到期从 0 开攒 | 起身无敌 Tag（那是旧失衡 Recover） |
| 时长走档位表 `VacuumSeconds` | 写死在代码里按 Boss 名分支 |

Executing 期间不要提前进真空，避免 F 自己的段还在打却已经锁零。

---

## 十、表

已加：`SkillCategory.HarmonyBreak`、`CharacterKit.HarmonyBreakSkillId`、`DazeSetting.VacuumSeconds`、现役 14001。`ExecuteMode` / RoleAttri 增幅仍后置。

`Kit.ChainSkillId` / 13001 先留着不用。空 `HarmonyBreakSkillId` 这次 F 失败。

改表仍是 xlsx → `Tools/gen_code_json.bat`。破坏技轴走 Flux，不要手改 `.asset`。

---

## 十一、绝区零失衡：运行时已删除

原先对照绝区零做了满条 Stagger + 导演 Punish + 窗内 Z 打 `ChainSkillId`。偏谐落地后这套 **从运行时拿掉**，不是再挂一个 `DazeGameplayEnabled=false`。

已删除：`CombatChainSkill`、`BeginDazeStagger` / `EndDazeStagger`、导演 `NotifyBreakMeterOpened/Closed` 与 `EncounterTempo.Punish`、计量相位 Opened / Draining / Recover、连携次数 / 易伤 / 起身无敌 Tag 推送、Q/E `SwitchReason.Chain` 推断。角色不再 `AttachAbility(ChainSkillId)`。

### 11.1 现在不应再发生

- 满条跪下、导演停发牌（Punish）
- Q/E 因条满打 13001
- 血条下旧失衡黄条语义（现条是偏谐，满条提示 F）

命中 / 招架 `AddDaze` **会**涨偏谐条。F6 打满进 Ready，不破衡。

### 11.2 不要动（不是这套玩法）

- 段表 `HitReaction` 轻重（受击表现，不是失衡窗）
- `CombatStateDirector` 的 Stagger 槽位本身（硬控仍可写 Control；偏谐不再写入）
- `DazeSetting.xlsx` / `SkillDamage.DazeRatio` / `ParryDazeRatio`（当偏谐数字；Hold/Duration/Vuln/ChainCount/RecoverIFrame 运行时已不读）
- `Kit.ChainSkillId` 列与 `SkillCategory.Chain`（表保留，运行时不读、不挂技能）
- 黄闪招架、红闪回避、支援窗、手动换人
- 表现包 `ChainAttack=402` 的枚举值（避免 Id 腾挪）

不要把连携 / Punish / 满条 Stagger 加回来。

---

## 十二、落地顺序

| 步 | 内容 | 验收 |
|---|---|---|
| 0 | 删除失衡运行时（已做） | 砍满不跪、Q/E 不再因条满连携 |
| 1 | Ready 相位 + UI 提示 F，怪仍行动 | **已接** |
| 2 | 情境键 F + Kit 破坏技 + 真空锁零 | **已接**：范围内 F 出 `HarmonyBreakSkillId`；0 失败；结束后锁 0 |
| 3 | 连招记忆 | **已接**：F 入队前快照下一发；结束后 Attack×Click 续段；闪避/受击/硬控/换人/死亡清记忆 |
| 4 | 轴 / 镜头 / 正式 UI | **已接**：独立处决包 + 构图镜头；HUD 按相位着色、只提示 F。14001 轴仍占位 |
| 后置 | 杂兵 Auto、增幅公式、偏移干涉 | 实机再对 |

---

## 十三、禁止

- 用 A / B / Z 冒充 F
- 满条进 Stagger 冒充失谐
- 把已删除的失衡连携 / Punish 加回来
- Kit 判断方法列、`if (角色名)`
- 手改 json / `SkillDataScriptable`
- 第五根钟、把表现写进 `DamageAction`
- 把破坏技配进 `CharacterSlot` 当第 5 战斗键

---

## 十四、回看

偏谐是 **敌人计量 + 场上 F**，不是换人技。失衡硬直连携已从运行时删除。黄红闪和支援仍走 Q/E。A 仍是终结技。
