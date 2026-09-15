# ACT 时间效果：对照与落地清单

对照鸣潮（极限闪避、共鸣解放）和绝区零（Perfect Dodge、命中顿帧、异常冻结），以及当前 `GameTimeManager` / `TimeScaleEffectManager` / `CombatContext` / `AnimComponent` / `BuffTimeComponent`。

本文只记 **时间层怎么接到角色上**。Buff 条目见 `ActBuffLearningBacklog.md`。主动技数字见 `ActSkillConfigAndLeveling.md`。

勾选表示运行时已按本文接线，不是「枚举里有这个名字」。

> 状态：P0–P2 已接。**P3 时缓场是方案，未接运行时。**

---

## 结论

项目已经有四根钟，不要再加第五根：

| 钟 | 用途 |
|---|---|
| 世界 `WorldDelta` | 敌人逻辑、物理、Buff / 点燃、对象池回收 |
| 玩家 `PlayerDelta` | 本地玩家走跑、技能轴、CD、动画 |
| 相机 `CameraDelta` | 跟镜头；断裂/顿帧里保持 1 |
| 实体 `GetTimeScale()` | 只乘在宿主身上：冻结 / HitStop / **时缓场（P3）** |

原则：**同一角色的动画、位移、技能轴、CD 必须同一根层钟**，再乘实体倍率。Buff 持续不乘实体倍率（点燃不被冻结暂停）。

对照两作：

- 极限闪避 = **遭遇内**敌人慢、**玩家仍能出招**（不要拧整根世界钟）。
- 大招时停 = 世界停、**发起者的轴继续**。
- 范围时缓（相里要式）= 圈内敌人慢，圈外照常。
- 冻结 = 停身体，不停异常跳伤。
- HitStop = 攻受短顿，不要把整场叠死。

---

## 已经对齐（不必再学一遍）

- [x] 不用 Unity `Time.timeScale`，UI 走 unscaled
- [x] 世界 / 玩家 / 相机三层 + 暂停冻效果计时
- [x] Buff / ETTimer 走 `WorldDelta`，不乘实体 TimeScale
- [x] 镜头在断裂、HitStop 里 CameraScale=1
- [x] 表现包目录里已有 TimeFracture / HitStop 轻重 / Dodge / SkillTimeStop；P1 已接到伤害段与极限闪避

---

## 明确不学

1. **用 Unity 全局 timeScale 做魔女时间**  
   UI 和编辑器会一起停。继续用 `GameTimeManager`。

2. **把点燃绑在实体 TimeScale 上**  
   冻结会暂停跳伤，和鸣潮 / 绝区零相反。

3. **用 min(WorldScale) 做技能加速**  
   合成从 1 取 min，大于 1 写不进去。轴加速继续用 `skillSpeed`。

4. **用 `WorldScale` 冒充范围时缓 / 魔女时间球**  
   场外怪、点燃、导演发牌、对象池会一起慢。圈和遭遇名单走实体钟（P3）。

---

## P0 接线（同一角色同一根钟）

时空断裂、HitStop、SkillTimeStop **已经能触发**。穿帮来自层没接到角色上。

| 项 | 要对齐的行为 | 状态 |
|---|---|---|
| 敌人 `animator.speed` 乘 `WorldScale` | 断裂时怪的 clip 和轴一起慢 | [x] |
| 本地玩家技能轴 / CD 走 `PlayerDelta` | 断裂窗口里仍能出招、转 CD | [x] |
| 敌人技能轴 / CD / 位移 走 `WorldDelta` | 和动画同一根钟 | [x] |
| SkillTimeStop 发起者走玩家钟 | 世界=0 时大招轴不停 | [x] |
| 受击槽 / 反应自动交回用层累计时间 | 怪的硬直跟世界钟 | [x] |

实现入口：`CombatTimeClock`（按 `UsesPlayerCombatClock` 选层）× 实体 `GetTimeScale()`。

本地玩家恒为玩家钟。`SkillTimeStop` 播放期间给发起者加一层 hold，效果移除时去掉。

---

## P1 顿帧模型与闪避窗口

| 项 | 要对齐的行为 | 状态 |
|---|---|---|
| HitStop 作用域 | 攻受短脉冲，不要全局把无关单位拖死 | [x] |
| 合成规则 | 同类型刷新；Priority 真正参与，避免连打 min 到接近 0 | [x] |
| 轻重段 | `HitCausedHeavy` / Crit / Stagger 接到段表 `HitReaction`，不要每下同一套 Light | [x] |
| 极限闪避 | 闪过攻击再播 `DodgeTimeFracture`，不要只靠轴上手工 Msg | [x] |

实现要点：

- HitStop 全局层保持 1，只给攻击者 + 受击者写实体 `TimeScale`（`CombatTimeClock.HitStopSourceId`）。周围单位不跟着爬。
- 同类型只留一份：新 Priority 更低则拒绝；否则替换并刷新时长。连打不会 `min` 叠到接近 0。
- 段表 `HitReaction`：`Light` → `HitCausedLight`；`Heavy` → `HitCausedHeavy`；暴击另走 `HitCausedCrit`，受击闪白走 Heavy。不要用段号当轻重档。
- 极限闪避：翻滚带 `Buff.Roll` 时 `CombatHitResolver` 把伤害标成 Dodge，本地玩家 `PostReceiveDamage` 自动播 `DodgeTimeFracture`（0.5s 世界 0.3）。轴上不必再摆 Msg。**P3 要把这条从 `WorldScale` 改成遭遇时缓场**；改之前这条仍是全局世界钟。
- **未做**：`StaggerBreak` 没有削韧条，不会自动播。
- 若 `EGamePlayInit` 挂了自定义 Catalog 且 ActionPoint 规则超过 2 条，不会自动覆盖；需要的话在资源上 `Reset To Built-In Defaults`。

---

## P2 表现层与冻结

| 项 | 要对齐的行为 | 状态 |
|---|---|---|
| 冻结 Buff → `EntityTimeScale=0` | 停动画与位移，点燃仍走世界钟 | [x] |
| 允许实体 scale 为 0 | 现在下限 0.0001，停不死 | [x] |
| 技能粒子 `simulationSpeed` 跟宿主钟 | 刀光不要比人快 | [x] |
| Afterimage / ScreenDesaturate Bridge | Perfect Dodge 灰屏+残影 | [x] |
| HitFlash 走层钟或世界钟 | 慢镜头里闪白不要先亮完 | [x] |

实现要点：

- `Buff.Freeze` 叶子 Tag 0→1：实体 `TimeScale` 写 0（`CombatTimeClock.FreezeSourceId`），`CharacterRenderFX.SetFreeze(1)` 出冰壳。点燃 / Buff 计时仍走 `WorldDelta`。
- Demo Buff `20601` 冻结 3 秒 / `20602` 长冻结 8 秒（Modify `24`）：`MoveForbid` + `SkillForbid` + `Buff.Freeze`。`20301` 是普通灼烧，不要复用。
- 测试用：`20701` 霜蚀（冻结中仍跳冰伤）、`20801` 测试护盾、`21001` 对冻结目标 +50%（挂攻击者）、`31011` 点燃。
- 电机 `CombatUnitLocomotionTimeSource` 与技能轴粒子乘实体钟；闪白走**层 delta**（不含实体），冻结时仍能播完。
- 极限闪避包 `DodgeTimeFracture` 现含断裂 + 灰屏 + 残影。灰屏计时 unscaled，与 TimeFracture 对齐。

文档第 10 条冻结与本表 P2 是同一件事。

---

## P3 时缓场（范围 / 遭遇，不拧世界层）

对照：鸣潮极限闪避是遭遇级子弹时间（[TapTap 闪避篇](https://www.taptap.cn/moment/586186995710034131)）；相里要解放 / 万方法则是**范围内敌人时缓**（[17173](https://news.17173.com/z/mingchao/content/09122024/114719764.shtml)、[TapTap 1.2](https://www.taptap.cn/moment/584200891297435454)）；乘霄山是关卡时流。虚幻没有「这块空间的 deltaTime」，惯例是 Overlap 后改 Actor Custom Time Dilation。本项目对应物已经有：`EntityTimeScaleComponent` 按 `SourceId` 乘。

**不要**加第五根钟。**不要**把时缓场写成新的 `GameTimeManager` 层。层钟继续管暂停、大招整场时停、镜头；谁变慢由实体钟决定。

Kuro 没有公开引擎帖。落地以可验收行为为准，不抄未证实的 PhysX 区域时间。

### 3.1 三套作用域

| 档 | 对谁慢 | 对照 | 本项目怎么圈人 |
|---|---|---|---|
| `Encounter` | 导演已登记敌人（`CombatEncounterDirector` 上限 16） | 极限闪避 | 扫导演数组，不 Overlap |
| `Sphere` | 球心 + 半径内、且 mask 命中的战斗体 | 相里要范围时缓 | `OverlapSphereNonAlloc`，每场最多 16 |
| `Zone` | 关卡触发器进出 | 乘霄山 / 溯流仪 | **本期不做**；接口留着，不要先做开放世界时流 |

大招整场时停仍走现有 `SkillTimeStop`（世界层 0 + 发起者 hold 玩家钟），**不是**时缓场。奥古斯塔式「别人冻、自己能走」若以后要做：还是 `SkillTimeStop` + 发起者 hold，不要用 Sphere。

### 3.2 运行时对象

新增 `CombatTimeField`（`CombatContext` 子实体，可 Tick，池化，同时最多 **8** 个）：

| 字段 | 含义 |
|---|---|
| `Kind` | `DodgeFracture` / `SkillSlow`（同 Kind 只留一份，刷新时长；不同 Kind 相乘） |
| `Scope` | `Encounter` / `Sphere` |
| `Scale` | 写入实体的倍率，如 0.3。必须 `0 ≤ scale < 1` |
| `Duration` | 计时走 **unscaled**（和现在 TimeFracture 效果槽一样，暂停时冻） |
| `Radius` | 仅 Sphere；米 |
| `Anchor` | 球心：施法者 / 锁定目标 / 世界点；每 Tick 跟着走 |
| `Mask` | 默认 `Enemies`。可加 `Projectiles`。默认 **不含** `UsesPlayerCombatClock` 的单位 |
| `Priority` | 同 Kind 刷新规则，抄 `TimeScaleEffectManager` |

`SourceId`：`CombatTimeClock.TimeFieldSourceId - slot`（slot 0–7），与 HitStop `-1001`、冻结 `-1002` 错开。进场 `AddModifier(sourceId, scale)`，离场 / 场销毁 `RemoveBySource`。冻结 0、HitStop 仍按乘算叠上。

热路径：无 LINQ、无每帧 `GetComponent`、Overlap 用 `NonAlloc` + 固定数组。导演扫已经是固定 16 槽。

### 3.3 谁跟着慢、谁不准慢

| 跟着宿主实体钟（要慢） | 继续走世界层（不准慢） |
|---|---|
| 敌人动画、技能轴、CD、电机、受击槽 | Buff / 点燃 / 偏谐真空（已约定不乘实体钟） |
| 该敌人身上的技能粒子（`CombatParticleClockDriver` 已乘实体） | `CombatEncounterDirector` 发牌、欲望、租约 |
| 进场的抛体：创建时记下场 Id，Tick 乘该场 Scale；或进球再写入 | 对象池回收、UI、相机层 |

物理：角色位移已走 `CombatTimeClock`。不要指望 PhysX 区域时间；无主刚体本期不管。

本地玩家 / 候场 / 死亡：默认不写入。Mask 显式 `IncludePlayer` 才能慢玩家（第一期不要开）。

### 3.4 表现包与轴

现 `DodgeTimeFracture` 调 `AddTimeFracture` → `WorldScale=0.3`，场外和点燃会一起慢。P3 改成开一个 `Kind=DodgeFracture, Scope=Encounter, Scale=0.3` 的场；灰屏 / 残影仍走现在的包，计时继续 unscaled。

新 `CombatFxKind.TimeField`（包字段：Duration / Scale / Radius / Scope）。轴 Msg 可后置：`MsgName=TimeField`，`FloatdMsg`=秒，`StrMsg`=`0.3|8|Sphere`（倍率\|半径\|作用域）。第一期 **C# API + 闪避改接** 即可，不必先改 Flux。

数字不要写死角色名。半径 / 倍率进表现包或技能 Msg，不要 `if (相里要)`。

### 3.5 落地切块

| 项 | 要对齐的行为 | 状态 |
|---|---|---|
| `CombatTimeField` 实体 + SourceId 写入/摘掉 | 球内慢、球外 1；销毁必摘干净 | [ ] |
| 同 Kind 刷新、异 Kind 相乘 | 闪避 0.3 叠技能场 0.5 → 0.15，不会 min 进世界层 | [ ] |
| 极限闪避改接 Encounter 场 | 导演登记的怪慢，玩家 1，点燃/场外不慢 | [ ] |
| `AddTimeFracture` 不再给闪避用 | 旧 API 可留着给调试，玩法路径不要调 | [ ] |
| 调试：`SkillEditorScene` 在玩家身前开 Sphere 场 | 按钮或热键，能看见近怪慢、远怪正常 | [ ] |
| 轴 Msg / 技能包 TimeField | 相里要式范围时缓可配 | [ ] 后置 |
| `Zone` 关卡触发器 | 乘霄山时流 | [ ] 不做 |

验收（编辑器里即可）：

1. 极限闪避：只有导演登记的敌人动作变慢，偏谐真空仍按世界钟走，UI 不卡。
2. 调试球：走进球的怪慢，走出恢复；玩家走跑正常。
3. 球内冻结 Buff：仍是 0（乘算），点燃继续跳。
4. 场到期或 Break：所有 SourceId 摘掉，没有人永远 0.3。

### 3.6 实现落点（不要写进 DamageAction）

- 场逻辑：`Assets/Scripts/ACTGame/Combat/CombatTimeField.cs`（或同级），Tick 在 `CombatContext.Update` 里、出招之后，不要进伤害结算。
- 表现：`TimeScaleFxBridge` 增 TimeField；闪避包改调场而不是 `TimeScaleEffectType.TimeFracture`。
- 调试：只加 `SkillEditorScene`，不要在 `GameTimeManager` 上轮询键。

P3 不改 Buff 时钟，不改镜头层，不把导演发牌绑上实体钟。

---

## 建议落地顺序

| 阶段 | 内容 | 立刻能感到的变化 |
|---|---|---|
| P0 | 选层 delta + 动画 speed + 时停 hold | 断裂时怪真的慢、玩家轴不卡、大招时停自己还能播 |
| P1 | HitStop 局部化 + 闪避自动断裂 | 手感接近鸣潮轻顿 / ZZZ 重顿；极限闪避不用手摆轴 |
| P2 | 冻结实体钟 + 粒子/残影 | 冰冻雕塑、慢镜头里特效跟得上 |
| P3 | 时缓场（遭遇/球）替换闪避的 WorldScale | 近怪慢、远怪和点燃不慢；技能可配范围时缓 |

P0 不改 Buff 时钟，不改镜头层。

---

## 回看

先扫勾选。未勾且仍符合当前玩法的，才值得做。对照两作时以「玩家窗口里能行动、异常不跟身体停」为准，不要抄崩 3 的全局 `timeScale`。范围减速走实体钟时缓场（P3），不要再拧 `WorldScale`。
