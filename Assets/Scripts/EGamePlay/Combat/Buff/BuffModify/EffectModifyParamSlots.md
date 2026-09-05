# EffectModifyType 槽位含义说明

本文档列出每种 EffectModifyType 下 BuffModifySetting 的 Param 槽位含义，供配表与流程梳理使用。

---

## 按来源细分的类型（推荐）

### SkillHpDamage (9) — 非段表临时 HP 伤害

主动技命中倍率走 `SkillDamage` 段表，**不读**本行 ParamFloat1。本类型只给 Buff 跳伤以外、不走段表的临时伤害。

| 槽位 | 含义 | 说明 |
|------|------|------|
| ParamInt1 | DamageCalcuFormulaType | 0=Default 1=Simple 2=Flat |
| ParamInt2 | DamageType | 0=Physic 等，见 DamageType 枚举 |
| ParamInt3 | CanCrit | 0=否 1=是 |
| ParamFloat1 | skillRate | 仅非段表路径使用，>0 时用，否则 1 |
| ParamFloat2 | 预留 | 暂未使用 |

### BuffHpDamage (11) — Buff HP 伤害

槽位含义与 **SkillHpDamage** 相同，另：

| 槽位 | 含义 | 说明 |
|------|------|------|
| ParamFloat2 | TickStackPolicy | 仅 TimeBuff 周期跳（OnTick）成功后生效。0=不掉层 1=减1层 2=层数减半向下取整 3=清层。OnStart 首次触发不掉层。已有表全是 0，行为不变。 |

---

### SkillResource (12) — 技能资源变动

目标由外部（技能/触发器）选定后传入，本效果不参与目标选择。

| 槽位 | 含义 | 说明 |
|------|------|------|
| ParamInt1 | ResourceFormulaType | 0=Flat 1=CasterAttrMul 2=TargetAttrMul 等 |
| ParamInt2 | AttributeType | 资源类型：HealthPoint / Mana / 特殊条 |
| ParamInt3 | 预留 | 不再用于目标选择 |
| ParamFloat1 | A | 公式主系数 |
| ParamFloat2 | B | 公式附加值（可选） |

### BuffResource (13) — Buff 资源变动

目标由外部（Buff 触发逻辑）选定后传入，本效果不参与目标选择。槽位含义与 **SkillResource** 相同。

---

## 其他类型

### PlayerModify (1) — 修饰玩家属性

| 槽位 | 含义 | 说明 |
|------|------|------|
| ParamInt1 | AttributeType | 目标属性类型 |
| ParamInt2 | ModifyType | 修改方式 |
| ParamInt3 | ApplySide | 0=作用于目标 1=作用于施法者 |
| ParamFloat1 | value | **单层**数值。可叠层 Buff 运行时乘以当前层数；层数变化会就地改写修饰器并刷新属性。 |
| ParamFloat2 | 预留 | 暂未使用 |

### PlayerControll (2) — 玩家行为禁制

| 槽位 | 含义 | 说明 |
|------|------|------|
| ParamString1 | 标签列表 | 添加/移除的 Tag。冻结配 `Buff.MoveForbid` + `Buff.SkillForbid` + `Buff.Freeze`（`Buff.Freeze` 会把实体 TimeScale 打到 0 并出冰壳）。 |

### PreRemoveStatus（行动点 65536）— 卸 Buff 前开火

不是新 Modify 类型。死亡爆炸 / 卸时跳伤：该 Buff 配 `TriggerBuff` + `ActionPointType` 含 `PreRemoveStatus`，效果仍用已有 `BuffHpDamage` / `BuffAddStatus`。

`StatusComponent.RemoveStatus` 只对**正在卸的那条**调用 `OnEvent`，不全表 Dispatch。上下文是 `RemoveStatusAction`（`Reason` / `BuffId`）。到期、净化、死亡都会走到这里；只要死亡才爆，需后续用 TriggerFormula 滤 `Reason`。

实体 `OnDestroy` 回收不清回调，避免误爆。

### BuffExpirePolicy — 生命周期绑定（表 Tag，不是新 Modify）

时间只是一种结束条件。可与 Duration 同时成立，谁先到谁走 `RemoveStatus(Expired)`。刷新同 BuffId 不重绑 Runner/Form。

| BuffTag | 策略 | 参数 | 挂钩 |
|---|---|---|---|
| （TimeBuff） | Duration | `BaseDuration` | 现有计时器 |
| `Buff.Bind.Skill` | SkillRunner | 施加时 Caster 的 `ActiveExecution.Id` | 轴 `Finish` / Session 销毁（全场单位） |
| `Buff.Bind.Form` | Form | 施加时持有者 `ActiveFormId` | `SetForm` / `ClearForm` 离开该形态 |
| `Buff.Bind.HitsTaken` | HitsTaken | `BaseTimes`（≤0 当 1） | 实际扣血后，闪避/免疫不计 |
| `Buff.Bind.HitsDealt` | HitsDealt | 同上 | 持有者打出实际扣血 |
| — | OnSwitchOut | — | 未挂钩（第 9 条） |

轴上 `RemoveTriggerEffectList` 仍是 `Manual`。完美闪避窗继续短 Duration + `Buff.Roll`，不要绑 Runner。

### AddShield (16) — 护盾吸收层

挂在 **专盾 Buff** 上，数值在 `VitalComponent` 多段列表，**不是** Attribute / 假 HP。`OnEnable` 挂上，`RevertOnDisable` 掉剩余盾；周期 OnTrigger 不刷盾。一条 Buff 只配一行。不要和眩晕等控制混在同一条（破盾会 `RemoveStatus(Consumed)` 整条卸掉）。

同 BuffId 一段：`RefreshDuration` / 叠层把该段重置为 `ParamFloat1 × 当前层数`（已吃掉的也会回满），队列位置不变。`AddDuration` 只续时，不补盾。多条不同 BuffId 按 FIFO 先吃旧段。致死预判用 `HP + Shield`，不在预判里扣盾。`CheckDead` 仍只看 HP。

时长用 Buff 自己的 `BaseDuration` / TimeBuff，不要另开 duration 槽。

| 槽位 | 含义 | 说明 |
|------|------|------|
| ParamFloat1 | 单层盾量 | 运行时 × `GetStackCount()`，取整 |
| 其余 Param | 未用 | 时长走 Buff Duration |

`EffectModifyType.AddShield = 16`（Luban 枚举若重新生成需保留）。

### StatusApplyModify (15) — 上 Buff 前置扫描

挂在**承受者** Buff 上，**不在 OnTrigger 执行**，由 `StatusApplyResolver` 在 `PreGive/PreReceive` 之后扫描。`EffectModifyType.StatusApplyModify = 15`（Luban 枚举若重新生成需保留）。

本切片已落地 **行为 0 免疫** 与 **行为 1 抵抗%**。行为 2（改写 Id）读表会被忽略。

刷新已有同 BuffId 时不扫描（不挡叠层/刷新）。被动 / `RequestAddStatus` 直挂不走此扫描。

免疫优先于抵抗：命中行为 0 则不再掷骰。抵抗%按匹配项 **ParamFloat1 × 当前层数** 累加，上限 100；`RandomHelper.RandomRate()`（1–100）≤ 累加值则 `Resisted`。100% 不掷骰，直接抵抗。

| 槽位 | 含义 | 说明 |
|---|---|---|
| ParamInt1 | FilterType | 0=All 1=ByBigBuffType 2=ByBuffId |
| ParamInt2 | FilterValue | BigBuffType / BuffId |
| ParamInt3 | Behavior | 0=Immunity 1=抵抗% 2=改写 Id（未落地） |
| ParamFloat1 | 抵抗百分比 | 行为 1：20 表示 20%；可叠层累加，上限 100 |
| ParamString1 | 预留 | - |

### ActionModify (6) — 条件增伤（命中乘区）

挂在攻击者或受击者 Buff 上，**不在 OnTrigger 执行**，由 `DamageCalcuFormula` 增伤区扫描已激活 Buff。`EffectModifyType.ActionModify = 6`（Luban 枚举若重新生成需保留该空缺值）。

| 槽位 | 含义 | 说明 |
|------|------|------|
| ParamInt1 | FilterType | 0=All 1=BySkillId 2=ByDamageType 3=BySource 4=目标有 BuffId 5=目标有 Tag |
| ParamInt2 | FilterValue | SkillId / DamageType / DamageSource / 目标 BuffId |
| ParamInt3 | ApplySide | 语义标注：0=常挂受击者 1=常挂攻击者。运行时按实际挂载方扫描，不强制校验。 |
| ParamFloat1 | 百分点 | 20 表示 +20%（乘区加 0.20） |
| ParamString1 | 目标 Tag | FilterType=5 时，受击者拥有其中**任一** Tag 即命中 |

### CurveEffect (5) — 资源型效果（治疗/回复能量等）

| 槽位 | 含义 | 说明 |
|------|------|------|
| ParamInt1 | ResourceFormulaType | 公式类型 |
| ParamInt2 | AttributeType | 资源类型 |
| ParamInt3 | TargetSide | 0=目标 1=施法者 |
| ParamFloat1 | A | 主系数 |
| ParamFloat2 | B | 附加值 |

---

## 兼容旧配置

### DamageEffect (4) — [已拆分，保留兼容]

旧 Param 约定（一槽多义，不推荐新配置使用）：

| 槽位 | 含义 | 说明 |
|------|------|------|
| ParamInt1 | DamageEffectFormulaMode | 0=HpDamage 1=Resource |
| ParamInt2 | formulaCode | HpDamage 时为 DamageCalcuFormulaType，Resource 时为 ResourceFormulaType |
| ParamInt3 | AttributeType | 资源类型 |
| ParamFloat1 | A / skillRate | HpDamage 时为倍率，Resource 时为 A |
| ParamFloat2 | B | Resource 时使用 |

建议将旧配置迁移到 **SkillHpDamage / BuffHpDamage / SkillResource / BuffResource**。
