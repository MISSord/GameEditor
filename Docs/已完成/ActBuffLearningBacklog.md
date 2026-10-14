# ACT Buff：对照鸣潮落地清单

对照以 **鸣潮** 为主（异常叠层、护盾、控制）。绝区零只保留已拍板的换人攻防 / 黄红闪，**不再从 Buff 抄积蓄条和紊乱**。顶视角射击文章里的 clone 模型、伤害队列、改写 Id 明确不学。

> **状态：对照清单已收口。** 延奏 / 候场停跳不在本文，见 `ActSquadDesign.md` §5.8（M4.3）。

勾选表示运行时已具备可玩行为，不是「枚举里有这个名字」。

---

## 已经对齐（不必再学一遍）

- [x] 流程问 Buff 列表，不写 `if (着火)` — `CombatBuffPipeline.Dispatch` + `Priority`
- [x] 流程中加 Buff 延后落地 — EffectLock
- [x] 致死窗口 — `PreBeKilled` / `PreCauseKill` / `PreReceiveKill`
- [x] 被动 = 常驻 Buff — `PassiveSkillBuffComponent`
- [x] 鸣潮异常叠层 — `32001` 光噪 / `32002` 风蚀 / `32003` 电磁；跳伤 × 层；`TryConsume` 打爆（爆发伤害由技能发）
- [x] 刷新时的 Caster 归属 — 默认后上覆盖；Tag `Buff.Caster.KeepFirst` 锁原施法者；跳伤 / 击杀只用 `GetLiveCaster()`，Caster 没了就不出手
- [x] TriggerFormula 能滤卸除原因 — `#IsRemoveReasonDeath` / `Expired` / `Consumed` / `Dispelled`；试玩 `32100` 死亡爆炸
- [x] 粘性属性 / 控制可撤销 — `PlayerModify` / Tag + `RevertOnDisable`；叠层会重算修饰
- [x] 对「带某 Buff / Tag 的目标」增伤 — `ActionModify` Filter 4 / 5（菲比打光噪）
- [x] `ActionModify` Filter 3：`DamageSource.Skill` vs `Buff`（跳伤不吃「命中上异常」已有 `#IsActiveSkillHit`）
- [x] 上 Buff 前置点 + 免疫 / 抵抗% — `AddStatusAction` + `StatusApplyResolver`
- [x] 控制 Gate 认 Tag — 沉默可闪、眩晕不可闪
- [x] 卸带原因并能开火 — `RemoveStatus(id, Reason)` + 本条 `PreRemoveStatus`
- [x] 死亡先 `OnRemoved(Death)` 再清；净化可按 `Buff.Debuff` / `Buff.Buff` 滤极性
- [x] 护盾 = Vital 吸收层，Buff 只挂 `AddShield`（破盾 `Consumed`）
- [x] 生命周期绑定 — `Buff.Bind.Skill` / `Form` / `HitsTaken` / `HitsDealt`（时间只是一种）
- [x] 控制落到动作 — 眩晕断招、冻结停实体钟、沉默只禁技能、霸体加抗打断、硬控互斥（`HardControlMutex`）

---

## 明确不学（丢掉）

来自 **绝区零 / 旧路线图 §二**，不再进 Buff 计划：

1. **异常积蓄条满了才转状态**  
   鸣潮光噪 / 风蚀是命中直接上状态叠层，不是第二条 Meter。偏谐已经占用 `CombatMeterComponent`，不要再给异常开槽。

2. **紊乱（Disorder）**  
   双异常互爆、清旧异常换新异常。本项目不学。

3. **失衡 / 削韧当 Buff 或第二套异常条**  
   已改偏谐；失衡硬直运行时已删除。不要塞进 `IdStatuses`。

4. **焚烧加速灼烧 / `ModifyExistingBuff`**  
   改别人 Buff 的间隔。要改强度就改这一份实例的 Numeric，不要为抄 ZZZ 加新 Modify 类型。

来自 **语雀顶视角文章**，架构对不上：

5. **运行时 clone / hack 表行** — 表只读。
6. **同一 BuffId 默认多实例** — 一目标一 Id；护盾同 Id 一段，不要靠多实例。
7. **Buff 回调里改 XC 轴** — 形态走 Form / 槽位表。
8. **用 Buff 模拟无敌帧 / 霸体** — 轴上 Tag（`State.Roll`、`State.UnStopped`）。
9. **EffectLock 解开后再冲伤害队列** — 锁只延后加/卸 Buff。跳伤继续同步 `ApplyDamage`。
10. **`StatusApplyModify` 改写 Id / 改层数后再落地** — 免疫和抵抗% 够用；不搞「变成另一条 Buff」。
11. **完整 Snapshot 对象**（攻击/精通/元素 + `KeepStrongerSnapshot` 三策略）— 击杀算谁先锁 **Caster 归属** 即可，不要为跳伤再做一套快照资产。
12. **补齐废弃 `EffectModifyType`** — `BuffModify(3)` / `DamageEffect(4)` / `CurveEffect(5)` / 旧 `AddStatus(7)` / `RemoveStatus(8)` 不要为对齐枚举再实现。治疗走 `BuffResource`，卸走 `RemoveStatus` API。

小队侧已拍板、不要从 Buff 再提：

13. **协奏能量条满才能延奏** — `ActSquadDesign.md` 已否；延奏只跟换人成功走 Tag 转移（M4.3 / §5.8）。
14. **延奏 / `OnSwitchOut` / 候场停跳** — 已从本文挪走，只在小队文档立项。不要在 Buff 里另做 Aura 运行时。

表现、不进本清单：

15. **护盾飘字**（`ShieldAbsorbed` / `HpDamageApplied`）— Vital 已写入，UI 另做。
16. **表列 `IsAddPoint` / `SmallBuffType` / `StatusSlot`** — 导出了，运行时没人读；不要按这三列设计新玩法。`OnStart` 无条件跳一次，不看 `IsAddPoint`。

架构收口：不要把一切揉成一种 BuffObj。

| 块 | 鸣潮里是什么 | 我们怎么放 |
|---|---|---|
| 状态 Buff | 加攻、光噪/风蚀、眩晕、被动 | `IdStatuses` + Tag + Modify |
| Meter | 偏谐条（不是异常条） | 已有 `CombatMeterComponent`，只服务偏谐 |
| 吸收 | 护盾 | Vital 多段；Buff 只负责挂上 |
| 小队循环 | 延奏给下场 | **`ActSquadDesign.md` §5.8**，不在本文做 |

---

## 阶段落点（均已收口）

| 阶段 | 内容 | 立刻能支持 | 状态 |
|---|---|---|---|
| A | 上 Buff 前置 + 免疫/抵抗 + Gate | Boss 控抗、眩晕不能出招 | [x] |
| B | `BuffRemoveReason` + 卸前开火 | 死亡爆炸、净化、破盾 | [x] |
| F | Vital 护盾 | 护盾技 | [x] |
| 控制 | 眩晕 / 冻结 / 沉默 / 硬控互斥 | 控制有手感 | [x] |
| 绑定 | Runner / Form / Hits | 技能结束、形态结束卸 | [x] |
| C | 配表异常 + 跳伤乘层 + `TryConsume` | 光噪/风蚀/打爆 | [x] |
| 归属 | ReApply 更新 Caster | 跳伤算对施法者 | [x] |
| Reason | 公式滤卸除原因 | 只死亡才爆 | [x] |
| H | 延奏 Outro + 候场停跳 | 换人循环 | 迁出 → 小队 M4.3 |

不换架构、不加 Meter、不学紊乱。新异常先加 Tag + Modify + 表行。

试玩入口：`BuffBarTester`（F11）。光噪 `32001` / 命中上光噪 `32010` / 对光噪增伤 `32020` / 死亡爆炸 `32100`。

---

## 已知配表坑（不是新需求）

Buff **31000** 的 `TriggerFormula` 写了 `#IsNormalOrUltimateSpell`，`BuffMethod` 里没有这个方法，触发会失败。要嘛补方法，要嘛改成已有的 `#IsActiveSkillHit`。

---

## 维护备忘（不是新玩法）

回看时先扫「已经对齐」和「明确不学」。下面几条是框架边角，**不要当成对照鸣潮的新立项**：

- `PlayerModify.ParamInt3` 写了 ApplySide=1（施法者），运行时仍改 **持有者** 属性。电磁减攻挂在敌人身上是对的；真要给施加者加攻，现在配不出来。
- `BuffTriggerComponent.Registrations` 开火已改走 `StatusComponent.Dispatch` 读表 `ActionPointType`，这份 Registration 列表基本闲置。
- `AttachStatus` 仍不写 `Caster`；战斗路径请走 `AddStatusAction` / `RequestAddStatus`。
- 候场 `BuffTimeComponent.Pause/Resume` 等小队 M4.3，不要在 Buff 里先做一套独立暂停。

回看时：未勾选且仍在「明确不学」里的，不要重新立项。
