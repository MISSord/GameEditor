# ACT Buff：文章对照与落地清单

对照来源：语雀《用 Unity 制作一个极具扩展性的顶视角射击游戏战斗系统》，以及鸣潮 / 绝区零的战斗 Buff 用法。

本文只记 **Buff**。技能 OnCast 改时间轴已判定与本项目不匹配，不在此清单。

勾选表示运行时已具备可玩行为，不是「枚举里有这个名字」。

---

## 已经对齐（不必再学一遍）

- [x] 流程问 Buff 列表，不写 `if (着火)` — `CombatBuffPipeline.Dispatch` + `Priority`
- [x] 流程中加 Buff 延后落地 — EffectLock
- [x] 致死窗口 — `PreBeKilled` / `PreCauseKill` / `PreReceiveKill`
- [x] 被动 = 常驻 Buff — `PassiveSkillBuffComponent`
- [x] OnTick + 掉层 — `TickStackPolicy`（风蚀 / 光噪 / 电磁）
- [x] 粘性属性 / 控制可撤销 — `PlayerModify` / Tag + `RevertOnDisable`
- [x] 对「带某 Buff / Tag 的目标」增伤 — `ActionModify` Filter 4 / 5

---

## 明确不学

1. **运行时 clone / hack `BuffModel`**  
   表只读。要改间隔、伤害系数，改这一份 Buff 实例上的 Numeric / Snapshot。

2. **同一 BuffId 默认多实例**  
   鸣潮光噪、绝区零灼烧是目标身上一条积蓄 / 一层状态。多实例只给白名单：独立护盾段、角色专属印记。

3. **Buff 回调里改技能时间轴**  
   形态 / 回路用 Form 和槽位表，不在 OnCast 改 XC 轴。

4. **把削韧、失衡、异常积蓄做成普通 TimeBuff**  
   它们是第二条资源条，不是状态栏图标。不要塞进 `IdStatuses`。

5. **用 Buff 模拟无敌帧 / 霸体**  
   继续走轴上 `XCMsgEvent` → Tag（`Buff.Roll`、`Buff.UnStopped`）。

架构收口：ACT 里不要把一切揉成一种 BuffObj。

| 块 | 是什么 | 现状 |
|---|---|---|
| 状态 Buff | 加攻、点燃、眩晕、被动 | 已有 Marker + 回调 |
| 积蓄 Meter | 异常条、失衡 / 削韧 | 未做，满了才变成状态或引爆 |
| 吸收 / 资源 | 护盾、协奏类 | 护盾已落 Vital 多段；协奏仍未做 |

---

## 值得学（未完成项）

### 1. 上 Buff 也走完整管线（抵抗 / 免疫 / 否决）

- [x] `AddStatusAction` 作为可改的单（BuffId / RequestedBuffId / Effect / Source）；时长覆盖、快照未做
- [x] 落地前 Dispatch `PreGiveStatus` / `PreReceiveStatus`；Combat 源效果锁，新建入队带已裁决 BuffId；免疫不入队
- [x] Immunity（`StatusApplyModify` 行为 0：All / BigBuffType / BuffId）
- [x] 对 `BigBuffType`（及 All / BuffId）的抵抗%（行为 1，累加上限 100；无衰减）
- [ ] 改层数 / 改成别的 Id 后再 `RequestAddStatus`
- [x] 控制类 Gate 真正读 Tag；沉默可闪、眩晕不可闪

**游戏对照：** Boss 冰冻抗性、异常抗性、净化窗口、控制免疫。  
**现状：** 眩晕推 SkillForbid+MoveForbid，沉默只推 SkillForbid；Gate 按闪避 Sort 认禁移。冻结停动画仍归第 10 条。

---

### 2. OnRemoved 要带原因，且能开火

- [x] 统一移除入口 `RemoveStatus(id, BuffRemoveReason)`
- [x] Reason：`Expired` / `Dispelled` / `Consumed` / `Replaced` / `Death` / `Manual`（护盾破了走 `Consumed`；引爆吃层仍归第 3 条）
- [x] 卸之前只打正在卸的那条 `PreRemoveStatus`，再 Revert（不全局 Dispatch）
- [x] `ApplyDeath` 先 `OnRemoved(Death)` 再清
- [x] 净化：`RemoveBuffByBigType` / `RemoveStatuses` 可按 `Buff.Debuff` / `Buff.Buff` Tag 滤极性

**游戏对照：** 光噪被打爆、紊乱吃掉当前异常、死亡爆炸、净化去灼烧。  
**现状：** 到期不再提前 Deactivate；死亡走 `RemoveAll(Death)`。爆炸伤害入队、吃层 `Consumed` 仍归第 3 / 5 条。 TriggerFormula 滤 Reason 未做。

---

### 3. 引爆 / 吃层：一等公民

- [ ] `StatusComponent.TryConsume(buffId, stacks, out snapshot)`：扣层或整段移除
- [ ] 返回 Caster + 快照攻击 + 剩余 / 原层数
- [ ] 调用方发**独立** `DamageAction`（来源见第 5 条），再 `OnRemoved(Consumed)`
- [ ] 新 Modify：`ModifyExistingBuff`（目标 BigType 或 BuffId，改间隔 / 剩余时间 / 额外倍率）
- [ ] 改的是实例 Numeric，不 clone 表（焚烧加速灼烧）

**游戏对照：** 满条触发、打爆、紊乱换异常。  
**现状：** `TickStackPolicy` 只管周期跳，没有外部吃层 API。

---

### 4. 异常 / 削韧 / 失衡：Meter，不要冒充 Buff

- [ ] 目标上独立 `CombatMeterComponent`：`Anomaly[元素]`、`Daze`（或削韧）
- [ ] 技能命中加积蓄（可被抗性缩小）
- [ ] 满条：转成现有状态 Buff（如点燃 31011），或直接 Consume 爆发
- [ ] 现有 `CanStack` + `MaxLevel` 只表示「异常已触发后的层数」，不当积蓄条
- [ ] UI：异常用专用条，不塞进 `StatusSlot` 图标栏

**游戏对照：** 鸣潮属性异常；绝区零异常积蓄 + 紊乱 + 失衡眩晕。  
**说明：** 文章没有这条，是按 ACT 补的。唯一计划中的新组件。

---

### 5. 额外伤害入队 + 来源标签

- [ ] `DamageSource` 扩成：`Skill` / `DoT` / `AnomalyBurst` / `Coordinated` / `Shield`（名称可再定）
- [ ] EffectLock 解开后再冲**伤害队列**（和延后加 Buff 同一拍）
- [ ] TriggerFormula 按来源过滤，少写 `#IsXxx`
- [ ] ActionModify 可滤「只加成技能直伤、不加成跳伤」

**游戏对照：** 点燃跳字、协同、紊乱都是另一下：独立飘字，往往不吃「命中点燃」。  
**现状：** `ExecuteHpDamage` 同步再 `ApplyDamage()`；来源只有 `Skill | Buff`。

---

### 6. 护盾 = 吸收层，Buff 只负责挂上

- [x] Vital 增加 `Shield`（多段列表，FIFO 先吃旧段；同 BuffId 一段）
- [x] `beHurt` 扣血前先吞盾，溢出再进 HP；致死窗口按「HP + 盾」预判，预判不消耗盾
- [x] Buff Modify：`AddShield`（`EffectModifyType = 16`），`ParamFloat1` = 单层盾量 × 层数；时长用 Buff 自己的 Duration；卸 Buff 掉剩余盾；破盾 `RemoveStatus(Consumed)`
- [x] 不用 `PlayerModify` 改最大生命冒充护盾

**游戏对照：** 鸣潮护盾、绝区零屏障。  
**配表：** 专盾 Buff 挂一行 `AddShield`，不要和眩晕混在同一条。`RefreshDuration` / 叠层会把该段重置为满额；`AddDuration` 只续时不补盾。飘字仍用 `DamageValue`，可用 `ShieldAbsorbed` / `HpDamageApplied` 做盾吸收表现（未接 UI）。吞盾也算受击，仍走 `NotifyHitTaken`。

---

### 7. 快照 + 刷新规则（含 Caster 归属）

- [ ] `Buff` 上只读 `Snapshot`（攻击、精通、元素、层数上限）
- [ ] DoT / 引爆用快照，不每次现算 Caster 当前属性
- [ ] 刷新策略配表：`RefreshDuration` / `KeepStrongerSnapshot` / `ReplaceCaster`
- [ ] 击杀、协同算 Snapshot 的主人
- [ ] 同 BuffId 合并时按策略更新或不更新 Caster

**游戏对照：** 灼烧快照上异常时的攻击；谁上的谁拿击杀。  
**现状：** `ReApply` 不更新 Caster；`ApplyKvParams` 只能改 Attribute 整型。

---

### 8. 生命周期绑定（时间只是一种）

- [x] `BuffExpirePolicy`：`Duration` / `HitsTaken` / `HitsDealt` / `SkillRunner` / `Form`；`OnSwitchOut` 枚举占位
- [x] 轴结束、切形态时走统一移除入口（`Expired`）；轴上显式移除仍是 `Manual`
- [x] 完美闪避反击窗继续用短 Duration + Tag，不必新系统

**游戏对照：** 直到这次技能结束、直到切人、直到形态结束、N 次受击。  
**现状：** 认 `BuffTag`：`Buff.Bind.Skill` / `Form` / `HitsTaken` / `HitsDealt`（次数用 `BaseTimes`）。切人 `OnSwitchOut` 未挂钩。

---

### 9. 切人光环 / 延奏

- [ ] 换人时 `TransferByTag("Buff.Outro")`，或 CombatContext 短时 Aura（目标 = 下场角色）
- [ ] 退场角色隐藏后，其 Status 列表不再 Tick 伤害
- [ ] 依赖第 2、7 条的 Reason 和 Snapshot

**游戏对照：** 鸣潮退场给下场加 Buff、协奏、入场技吃这个 Buff。  
**说明：** 后做，但是鸣潮核心循环。

---

### 10. 控制要落地到动作，不只是图标

- [x] `StateDirector` 控制槽（Dead > Control > Hit > Skill）；Gate 认 Forbid Tag（第 1 条）
- [x] 眩晕 = `MoveForbid` 0→1 断招 + 控制槽，时长跟 Tag 计数
- [x] 沉默 = 禁技能槽、闪避仍可（Gate，不进控制槽）
- [x] 冻结 = 停动画与位移（实体 TimeScale）
- [x] 霸体仍是 Tag，只跳过短硬直，不挡硬控 Buff
- [ ] 硬控互斥：新硬控替换旧硬控，或高 Priority 覆盖（在第 1 条 PreReceiveStatus 里做）

**游戏对照：** 眩晕断招、冻结停动画、沉默只能闪避、霸体吃硬直不吃断招。  
**现状：** `MoveForbid` 沿变进 `PlayerStateEnum.Control`；硬控中跳过 0.35s 受击。冻结 Buff `20601` 推 `Buff.Freeze`，实体钟=0 + 冰壳；点燃仍走世界钟。硬控互斥未做。

---

## 建议落地顺序

| 阶段 | 内容 | 立刻能支持的玩法 | 状态 |
|---|---|---|---|
| A | 上 Buff 前置点 + 免疫/抵抗 + Gate 认控制 Tag | Boss 控抗、眩晕真的不能出招 | [x] 免疫+抵抗%+Gate；改写 Id / 衰减未做 |
| B | `BuffRemoveReason` + 卸前开火；死亡走移除 | 死亡爆炸、净化、光噪被打掉 | [x] 卸前 PreRemoveStatus；护盾破了 Consumed |
| C | 伤害队列 + `DamageSource` 细分 | 点燃不触发点燃、紊乱独立飘字 | [ ] |
| D | `TryConsume` + `ModifyExistingBuff` | 引爆、紊乱、焚烧加速灼烧 | [ ] |
| E | `CombatMeter`（异常积蓄 / 削韧或失衡） | 鸣潮异常条、绝区零失衡 | [ ] |
| F | Vital 护盾 | 护盾技、声骸盾 | [x] |
| G | Snapshot + 刷新/归属 | 跳伤稳定、击杀算对 | [ ] |
| H | 绑定 Runner/Form + 切人转移 | 入场/退场、形态 Buff | [~] Runner/Form/Hits 已接；切人转移未做 |

A→D 都在现有 `Buff` + `Dispatch` + `AddStatusAction` 上长，不换架构。  
E 才是新组件，也是和文章差距最大、和鸣潮 / 绝区零最像的一块。

回看时：先扫本表勾选，再对阶段表。未勾选且仍符合当前玩法的，才值得做。
