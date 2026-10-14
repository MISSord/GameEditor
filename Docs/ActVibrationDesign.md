# ACT 削韧条（共振度）

> 状态：**第 0–4 步已接**（表 / 身份 / 段表扣条 + Stagger / 招架与 F 全局砍条 / 头顶 HUD + `StaggerBreak`）。对照鸣潮共振度（Vibration Strength）：白条扣光 → 瘫痪；**不是**偏谐，也不是已删除的绝区零失衡连携。
> 关联：`ActHarmonyBreakDesign.md`（偏谐 / F，已接）、`ActEnemyAiDesign.md`（导演 / Brain 已认 Stagger）、`ActSkillKitConfig.md`（角色表范式）、`ProjectConventions.md`。
> 数字只改 `Tools/Config/Datas/*.xlsx` 再导出。禁止手改 json / 生成 C# / `SkillDataScriptable`。

---

## 〇、一句话

精英 / 首领按 **怪物 Id** 配一条共振度。普通命中按段表扣白条；**黄闪招架成交**和 **谐度破坏命中** 用全局固定比例砍条（不跟角色、不跟技能走）。扣光后写入已空的 `Stagger` 槽，黄条当瘫痪计时器。瘫痪窗 **不加易伤**。偏谐继续涨条按 F，两条互不替换。

---

## 一、已拍板

| 项 | 结论 |
|---|---|
| 玩法 | 学鸣潮共振度：白条扣光 → 无法行动一段时间 → 黄条掉光起身回满 |
| 瘫痪易伤 | **不做。** 窗内伤害公式与 Intact 相同，不改 `Vulnerability`，不加 `VulnBonus` 列 |
| 怪物身份 | 新建怪物表，主键 **`MonsterId`**，范式对齐 `CharacterKit` 的 `CharacterId` |
| 韧条行 | `VibrationSetting` 主键也是 **`MonsterId`**（一对一）。缺行 = 无条，不回退第一行 |
| 招架削韧 | 全局固定值（目标 Max 的百分比）。不写进段表、不写进角色 Kit、不按怪物分行 |
| 谐度破坏削韧 | 同上，另一列全局固定百分比。F 的伤害仍走 14001 段表；削韧不跟这段倍率绑 |
| 普通命中削韧 | 仍走段表 `VibrationRatio`（普攻 / 战技 / 大招可以不同） |
| 计量形态 | 独立组件，不是 Buff，不是 Vital，不进 `IdStatuses` |
| 破韧行为 | `CombatStateDirector.EnterStagger`。不推 `State.MoveForbid`（那会进 Control 盖掉 Stagger） |
| 与偏谐 | 并存。`DazeSetting` / `DazeRatio` 继续只服务偏谐 |
| 与 ZZZ 失衡 | 不恢复 Punish / 连携 / QE。满偏谐仍然不跪 |
| 弹刀 | 不做鸣潮金圈。招架仍是绝区零黄闪 |

取消的旧草案：按杂兵/精英/首领三档共用 `VibrationSetting.Id`；招架 / F 削韧写在怪物行或段表；瘫痪窗默认 +20% 易伤。

---

## 二、不要和这些混名

| 机制 | 为什么不是这个 |
|---|---|
| 偏谐 / 失谐 / F | 从空涨满，按 F；怪不硬直。已接，见 `ActHarmonyBreakDesign.md` |
| 绝区零失衡连携 | 满条跪下 + 候场轮转。运行时已删除 |
| 角色 CharacterId / Kit | 玩家招式包。怪物不进 `CharacterSlot` |
| 协奏 / 共鸣回路 / 喧响 | 玩家或小队资源，不是敌人条 |
| 抗打断 / `State.UnStopped` | 这一刀能不能断当前轴。削韧是另一条数，轻刀也可磨 |
| HP 护盾 | Vital 吸收层。白条不是减伤盾 |
| 异常 Buff | 光噪 / 风蚀走叠层，不给 Meter 再开槽 |

---

## 三、现状钩子（不用新开平行状态机）

| 已有 | 现状 | 削韧怎么用 |
|---|---|---|
| `CombatStateDirector` Stagger 槽 | API / Gate / `MoveWeight` / Brain / 导演都认；偏谐不再写入 | 破韧 `EnterStagger`，黄条结束 `ExitStagger` |
| `PostReceiveDamage` | Poise 与偏谐已同拍 | 新组件同拍扣韧；Dodge / Immunity / Interrupt 跳过 |
| `CombatParry` | 成交另一次 `AddDaze` 涨偏谐 | 再读全局招架比例砍韧，不读段表 |
| `CombatHarmonyBreak` | F 走 Kit 破坏技 + 段表伤害 | 命中后再读全局 F 比例砍韧 |
| `CombatFxPackageId.StaggerBreak` | 破韧瞬间由 `ApplyVibrationBreak` 播 | 表现层订阅，不进 `DamageAction` |
| `Combat.DazeRecover` | HitResolver 已标 Immunity | 起身短 i-frame 复用，不是易伤 |
| `EnemyBrain` / 导演 | `IsStagger` 强制战术态、停发牌 | 接上即可，不要再写 `if (削韧)` |

玩家资源仍是 Vital（HP / Mana / 盾）+ Kit。`ResourceFormula` 注释里的「韧性」不要做成 AttributeType。

当前敌人生成写入 `CombatEntity.MonsterId`，Poise / 偏谐 / 共振度问怪物行。F3=201 已有白条：段表扣光进 Stagger；黄闪招架砍 `Max × 0.25`，F 命中砍 `Max × 0.30`（不跟角色、不跟技能）。头顶血条叠白/黄韧条；破韧播 `StaggerBreak`。`CombatEntity.CharacterId` 仍是玩家 RoleAttri / Kit 用的。

---

## 四、怪物表（学角色列表）

角色侧：`CharacterSetting.xlsx` → 工作表 `CharacterKit`，主键 `CharacterId`，缺行就是没有，不回退 0 号。

怪物侧照抄这个身份表，**不要**把敌人行塞进 `CharacterKit`。

### 4.1 源文件

`Tools/Config/Datas/MonsterSetting.xlsx`

登记进 `__tables__.xlsx`。Luban `mode=map`，键 `MonsterId`。

### 4.2 工作表 `Monster`

主键 **`MonsterId`**。读的永远是这只怪生成时写下的 Id。

| 字段 | 说明 |
|---|---|
| MonsterId | 主键。与玩家 `CharacterId` **分号段**，禁止复用 1 |
| Name | 策划名 |
| Rank | 枚举 `MonsterRank`：`Grunt` / `Elite` / `Boss`（先加 `__enums__.xlsx`） |
| DazeSettingId | 偏谐档，指向现有 `DazeSetting.Id`（1 杂兵 / 2 精英 / 3 首领）。0=不攒偏谐 |
| AntiInterruptBase | 站立抗打断。0=按 Rank 默认（杂兵 1 / 精英 3） |
| SuperArmorBonus | 有 `State.UnStopped` 时叠加。0=用代码默认 +3 |

第一期 **不** 把 BrainProfile 路径、MoveSet、HP 成长收进本表。AI 仍读现有 SO；属性仍可走 `RoleAttri`（缺行回退是旧行为，另立项再做 `MonsterAttri`）。本表只解决「这是哪只怪」以及偏谐档 / 抗打断从哪读。

**禁止** `MonsterId=0` 系统行做运行时回退。  
**禁止** 用 Rank 去查 `VibrationSetting`（韧条只认 MonsterId）。

### 4.3 号段（现役建议）

| 段 | 用途 | 第一期行 |
|---|---|---|
| 1–99 | 玩家 `CharacterId`，怪物不用 | — |
| 100–199 | 杂兵 | `101` 测试杂兵 |
| 200–299 | 精英 | `201` 测试精英 |
| 300–399 | 首领 | `301` 测试首领 |

调试刷怪（F2 / F3 / F4）改为写入 `MonsterId`，不要再只靠 `EnemyA` 模型位推断档位。

### 4.4 运行时字段

`CombatEntity`（及 `ActPlayer` Inspector）增加 `MonsterId`：

- 玩家 / 假玩家：`0`
- 敌人：表里的 Id

生成后（第 0–4 步已接）：

```
Poise.Configure(Monster.AntiInterruptBase, Monster.SuperArmorBonus, Rank==Elite|Boss)
DazeMeter.Configure(Monster.DazeSettingId)   // 0 则不配偏谐
Vibration.Configure(MonsterId)               // 查 VibrationSetting，缺行或 CanBreak=0 则无条
```

`EnemyBrainComponent.ApplyDazeFromProfile` 里按 `_isElite ? Elite : Grunt` 的写法 **删掉**，改问 `MonsterId`。

---

## 五、`VibrationSetting`（主键 MonsterId）

同一份 `MonsterSetting.xlsx` 第二张工作表，或独立 `VibrationSetting.xlsx`。推荐 **同一文件第二张表**，避免怪物 Id 对不齐。

Luban `mode=map`，键 **`MonsterId`**。与 `Monster` **一对一**：

- 有怪物行、无韧条行 → 无条（杂兵默认这样）
- 有韧条行、`CanBreak=0` → 无条
- `Get` 缺 Id **禁止**回退表第一行（偏谐 `DazeSetting` 现在会回退杂兵档，韧条不要学）

| 字段 | 101 杂兵 | 201 精英 | 301 首领 | 说明 |
|---|---|---|---|---|
| MonsterId | 101 | 201 | 301 | 与怪物表同一主键 |
| MaxVibration | 0 | 100 | 160 | 内部点数；HUD 归一化 |
| CanBreak | 0 | 1 | 1 | 0=不显示、不扣、不破 |
| VibrationResist | 0 | 0 | 0.1 | 0~1，只乘在 **段表** 削韧上 |
| IdleRegenPerSec | 0 | 0 | 0 | 未破时每秒回复 Max 的比例；第一期可全 0 |
| BreakHoldSeconds | 0 | 0.15 | 0.15 | 破韧停条给特效 |
| ImmobilizeSeconds | 0 | 4.0 | 6.0 | 黄条时长（世界钟） |
| RecoverIFrame | 0 | 0.45 | 0.55 | 起身 `Combat.DazeRecover` 秒数 |

**不要**这些列：`VulnBonus`、`ParryVibrationPct`、`TuneBreakVibrationPct`、`HoldSeconds`（ZZZ 遗留）、`ChainCount`。

---

## 六、招架 / F：全局固定值

不跟角色绑定，不跟技能绑定，不跟怪物分行。所有人对所有可破目标都是同一套比例。

### 6.1 工作表 `VibrationRule`

同一 xlsx 第三张表。范式对齐 `SquadSetting`：**一行全局，`Id=1`**。

| 字段 | 建议现役 | 说明 |
|---|---|---|
| Id | 1 | 唯一行 |
| VibrationImpact | 100 | 段表削韧冲击力。`points = Impact × VibrationRatio × (1 − Resist)` |
| ParryVibrationPct | 0.25 | 黄闪招架成交：`Max × Pct` |
| TuneBreakVibrationPct | 0.30 | 谐度破坏 **命中**：`Max × Pct` |

缺行或 Id≠1：招架 / F 不削韧（不要回退代码常量当正式值；调试期可打日志）。

### 6.2 语义

- 百分比相对 **目标** `MaxVibration`，所以精英 25 点、首领 40 点，手感相对条长一致。
- 招架成交仍走现有 `CombatParry`；偏谐继续用 `DazeSetting.ParryDazeRatio`（那是偏谐，别混进本表）。
- F 的 HP 伤害仍查 14001 段表。`VibrationRatio` 在破坏技段上填 **0**，削韧只走 `TuneBreakVibrationPct`。
- 未破（Intact）才扣。Breaking / Immobilized / Recover 拒绝削韧，避免瘫痪中连破。
- 招架成功但目标 `CanBreak=0`：只结算招架硬直 / 偏谐，不报错。

---

## 七、普通命中：段表 `VibrationRatio`

`AbilitySetting` 段表 **新列** `VibrationRatio`。保留 `DazeRatio` 给偏谐。

现役普攻 `DazeRatio=0.55` 两刀满偏谐，**禁止**拿去当削韧。

公式：

```
points = VibrationRule.VibrationImpact × SkillDamage.VibrationRatio × (1 − VibrationResist)
```

建议第一期（精英 Max=100、Impact=100 时约等于「比例 × 100 点」）：

| 段类型 | VibrationRatio |
|---|---|
| 普攻轻段 | 0.08 |
| 普攻后续 | 0.10 |
| 战技 | 0.20 |
| 大招段 | 0.35 |
| 入场 / 支援技 | 0.40 |
| 闪避反击 | 0.35（Tag 收口后再填） |
| 谐度破坏 14001 | **0**（走全局 TuneBreakPct） |

`Ratio=0` 或无行：本段不削韧。闪避 / 免疫 / 中断旗标的刀不削。

---

## 八、运行时相位

组件 `CombatVibrationComponent`，挂敌人实体。不是 TimeBuff。

| 相位 | 条 | 行为 | 削韧 |
|---|---|---|---|
| Intact | 白，Current=剩余共振度 | 怪正常；AI 正常抢牌 | 段表扣；招架 / F 按全局 Pct 扣 |
| Breaking | 停在 0，Hold 秒 | 已断轴；播 StaggerBreak | 拒绝 |
| Immobilized | 黄条=剩余时间/Duration | `EnterStagger`；Grant `State.VibrationBreak` | 拒绝。HP / 偏谐照常 |
| Recover | 回满 Max | `ExitStagger`；Grant `Combat.DazeRecover` | 拒绝（i-frame 连 HP 也免疫） |

黄条计时走 **世界钟**，不乘实体 TimeScale：冻结时点燃仍跳，瘫痪窗也不因实体钟=0 而暂停。四根钟不变。

破韧那一帧（`PostReceiveDamage` 后 Current≤0 且 Intact）：

1. `BreakActiveSkill`
2. `EnterStagger`（无视抗打断 / UnStopped）
3. GrantTag `State.VibrationBreak`（系统 Source，**不是** Buff，**不是** `MoveForbid`）
4. 相位 Breaking → Immobilized
5. 表现层播 `StaggerBreak`，不进 `DamageAction`

硬控（眩晕 / 冻结）仍可盖住外观（Control > Stagger）；黄条继续走。死亡清相位。

Tag：`TagCollection` intern `State.VibrationBreak`。查询瘫痪用这个叶子。不进 `HardControlMutex`。

**不做**：窗内 `Vulnerability` 修饰、按怪物的易伤列、白条存在时减伤。

---

## 九、命中接缝

普通盒：

```
HitPipeline.Flush
  → ApplySkillSegment（HP / 偏谐 DazeRatio）
  → PostReceiveDamage
      CombatPoise          打断比大小 → Hit
      CombatMeter          AddDaze
      CombatVibration      扣 VibrationRatio（本方案）
      FxRouter             HitCaused 包
```

招架成交（无盒或盒已标 Parry）：

```
CombatParry 成功
  → 敌人 Stun 硬直（现有）
  → AddDaze(Parry)                         // 偏谐，仍按 DazeSetting
  → ApplyVibration(Max × ParryVibrationPct) // 全局，与技能无关
```

谐度破坏命中：

```
14001 段表出独立伤害（VibrationRatio=0）
  → PostReceive 检测到 HarmonyBreak 来源
  → ApplyVibration(Max × TuneBreakVibrationPct)
```

不要把 TuneBreak 百分比写进 14001 段表，避免以后换破坏技轴就要改削韧。

---

## 十、HUD 与表现

敌人血条：

1. HP
2. 共振度：Intact 白条；Immobilized 黄条倒计时；Recover / 无条不画
3. 偏谐：保持现有（满条提示 F）

玩家无 `MonsterId`、无本组件，不画韧条。

破韧：`CombatFxPackageId.StaggerBreak`（已有，强 HitStop + 闪白 + 重震）。禁止在 `DamageAction` 里播。

---

## 十一、明确不要做

1. 瘫痪易伤 / 白条减伤。
2. 用 `DazeSetting` 三档冒充韧条，或复用 `DazeRatio`。
3. 削韧做成 Buff / Vital / 第二套异常积蓄。
4. 满条再走 Punish、连携、QE。
5. Push `State.MoveForbid` 冒充瘫痪。
6. 招架 / F 削韧按角色、按技能、按怪物分行。
7. `GetVibrationSetting` 缺 Id 回退第一行。
8. 第五根钟；`Time.timeScale` 做瘫痪。
9. 鸣潮金圈振刀。
10. 在 `DamageAction` 里改 `CurState` 或播破韧特效。

---

## 十二、落地顺序

| 步 | 内容 | 验收 |
|---|---|---|
| 0 表 | `__enums__` 加 `MonsterRank`；`MonsterSetting.xlsx` 三张表；段表加 `VibrationRatio`；导出 | **已接**：json / 生成 C# 有 MonsterId 键；现役 101/201/301 |
| 1 身份 | `CombatEntity.MonsterId`；刷怪写入；Poise / 偏谐改问怪物行 | **已接**：F2=101 无韧条仍可被打断；F3=201 有白条要等第 2 步组件 |
| 2 组件 | `CombatVibrationComponent` + PostReceive 扣段表 | **已接**：精英白条能磨光，怪停 4s，黄条掉光起身；无易伤 |
| 3 全局砍条 | 招架读 `ParryVibrationPct`；F 命中读 `TuneBreakVibrationPct` | **已接**：换角色 / 换招，招架与 F 削韧点数不变（相对该怪 Max） |
| 4 表现 | StaggerBreak 自动播；HUD 白/黄；起身 i-frame | **已接**：破韧顿帧+闪白+重震；头顶 Intact 白 / Immobilized 黄；Recover 不画条；起身 `Combat.DazeRecover` |

代码落点（开工时）：

| 文件 | 改动 |
|---|---|
| 新 `CombatVibrationComponent.cs` | **已接**：相位、扣段表、世界钟黄条、`State.VibrationBreak` / 起身 i-frame |
| `CombatEntity` / `ActPlayer` | **已接** `MonsterId`；Awake 按怪物表配 Poise / 偏谐 / 共振度。招架与 F 第 3 步 |
| `EnemyBrainComponent` | **已接**：偏谐 / Poise 改问怪物表；AI 档问 Rank |
| `CombatParry` | **已接**：成交读 `VibrationRule.ParryVibrationPct`，不乘抗性 |
| `CombatHarmonyBreak` 或 PostReceive | **已接**：F 第一次有效命中读 `TuneBreakVibrationPct`（14001 多段不叠砍） |
| `TagCollection` | intern `State.VibrationBreak` |
| 敌人血条 UI | **已接**：`UIBar.SetVibrationFill` 运行时克隆偏谐条；Intact 白 / Immobilized 黄 |
| `CombatFxPackageCatalog` | **已接**：`ApplyVibrationBreak` → `StaggerBreak`；旧 catalog 缺包则 Ensure |

改表后必须在 `Tools/` 执行 `cmd /c gen_code_json.bat`。

---

## 十三、验收脚本

| # | 操作 | 期望 |
|---|---|---|
| 1 | 只普攻打 201 | 白条匀速掉，怪仍出招；掉光才进 Stagger |
| 2 | 白条未空时打带 UnStopped 的敌招 | 不断招（Poise），白条仍掉 |
| 3 | 白条掉光 | 无视霸体断轴；导演停牌 |
| 4 | 瘫痪中继续打 | 有伤害；**伤害不加易伤**；偏谐仍涨；不再破韧 |
| 5 | 黄条掉光 | 起身 i-frame，白条回满，AI 重新抢牌 |
| 6 | 黄闪招架成功 | 砍 `Max × 0.25`；偏谐另算；换角色数值不变 |
| 7 | 满偏谐按 F | 独立伤害 + 砍 `Max × 0.30`；不换人、不拆普攻记忆 |
| 8 | 打 101 杂兵 | 无韧条；仍可被 InterruptLevel 打断 |
| 9 | 冻结叠在瘫痪上 | Control 盖外观；黄条不因实体钟=0 而停 |
| 10 | 缺 `VibrationSetting` 行的新 MonsterId | 无条，不回退 201 |
| 11 | 打爆 201 韧条 | 顿帧 + 闪白 + 重震；头顶白条掉空再变黄倒计时 |
| 12 | 黄条掉光起身 | 条消失约 0.45s，期间砍不到立刻再跪；随后白条回满 |

---

## 十四、回看

落地或放弃后：改本文勾选 + `Docs/ActUnfinishedIndex.md` 状态列。偏谐文档不要把 Stagger 再写成「偏谐写入」；本文才是 Stagger 槽的玩法入口。
