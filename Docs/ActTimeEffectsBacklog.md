# ACT 时间效果：对照与落地清单

对照鸣潮（极限闪避、共鸣解放）和绝区零（Perfect Dodge、命中顿帧、异常冻结），以及当前 `GameTimeManager` / `TimeScaleEffectManager` / `CombatContext` / `AnimComponent` / `BuffTimeComponent`。

本文只记 **时间层怎么接到角色上**。Buff 条目见 `ActBuffLearningBacklog.md`。主动技数字见 `ActSkillConfigAndLeveling.md`。

勾选表示运行时已按本文接线，不是「枚举里有这个名字」。

---

## 结论

项目已经有四根钟，不要再加第五根：

| 钟 | 用途 |
|---|---|
| 世界 `WorldDelta` | 敌人逻辑、物理、Buff / 点燃、对象池回收 |
| 玩家 `PlayerDelta` | 本地玩家走跑、技能轴、CD、动画 |
| 相机 `CameraDelta` | 跟镜头；断裂/顿帧里保持 1 |
| 实体 `GetTimeScale()` | 只乘在宿主身上（以后的冻结/单体减速） |

原则：**同一角色的动画、位移、技能轴、CD 必须同一根层钟**，再乘实体倍率。Buff 持续不乘实体倍率（点燃不被冻结暂停）。

对照两作：

- 时空断裂 / 极限闪避 = 世界慢、**玩家仍能出招**。
- 大招时停 = 世界停、**发起者的轴继续**。
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
| 轻重段 | `HitCausedHeavy` / Crit / Stagger 接到段，不要每下同一套 Light | [x] |
| 极限闪避 | 闪过攻击再播 `DodgeTimeFracture`，不要只靠轴上手工 Msg | [x] |

实现要点：

- HitStop 全局层保持 1，只给攻击者 + 受击者写实体 `TimeScale`（`CombatTimeClock.HitStopSourceId`）。周围单位不跟着爬。
- 同类型只留一份：新 Priority 更低则拒绝；否则替换并刷新时长。连打不会 `min` 叠到接近 0。
- 段号：未填或 1 → `HitCausedLight`（Priority 10）；≥ 2 → `HitCausedHeavy`（20）；暴击 → `HitCausedCrit`（30），受击闪白走 Heavy。
- 极限闪避：翻滚带 `Buff.Roll` 时 `CombatHitResolver` 把伤害标成 Dodge，本地玩家 `PostReceiveDamage` 自动播 `DodgeTimeFracture`（0.5s 世界 0.3）。轴上不必再摆 Msg。
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

## 建议落地顺序

| 阶段 | 内容 | 立刻能感到的变化 |
|---|---|---|
| P0 | 选层 delta + 动画 speed + 时停 hold | 断裂时怪真的慢、玩家轴不卡、大招时停自己还能播 |
| P1 | HitStop 局部化 + 闪避自动断裂 | 手感接近鸣潮轻顿 / ZZZ 重顿；极限闪避不用手摆轴 |
| P2 | 冻结实体钟 + 粒子/残影 | 冰冻雕塑、慢镜头里特效跟得上 |

P0 不改 Buff 时钟，不改镜头层。

---

## 回看

先扫勾选。未勾且仍符合当前玩法的，才值得做。对照两作时以「玩家窗口里能行动、异常不跟身体停」为准，不要抄崩 3 的全局 `timeScale`。
