# EffectModifyType 槽位含义说明

本文档列出每种 EffectModifyType 下 BuffModifySetting 的 Param 槽位含义，供配表与流程梳理使用。

---

## 按来源细分的类型（推荐）

### SkillHpDamage (10) — 技能 HP 伤害

| 槽位 | 含义 | 说明 |
|------|------|------|
| ParamInt1 | DamageCalcuFormulaType | 0=Default 1=Simple 2=Flat |
| ParamInt2 | DamageType | 0=Physic 等，见 DamageType 枚举 |
| ParamInt3 | CanCrit | 0=否 1=是 |
| ParamFloat1 | skillRate | 技能倍率，>0 时使用，否则 1 |
| ParamFloat2 | 预留 | 暂未使用 |

### BuffHpDamage (11) — Buff HP 伤害

槽位含义与 **SkillHpDamage** 完全相同。

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
| ParamFloat1 | value | 数值 |
| ParamFloat2 | 预留 | 暂未使用 |

### PlayerControll (2) — 玩家行为禁制

| 槽位 | 含义 | 说明 |
|------|------|------|
| ParamString1 | 标签列表 | 添加/移除的 Tag |
| ParamInt/ParamFloat | 未用 | - |

### ActionModify (6) — 修饰伤害/治疗行为

| 槽位 | 含义 | 说明 |
|------|------|------|
| ParamInt1 | FilterType | 0=All 1=BySkillId 2=ByDamageType 3=BySource |
| ParamInt2 | FilterValue | 过滤值 |
| ParamInt3 | ApplySide | 0=受击者 1=攻击者 |
| ParamFloat1 | 百分比 | 如 20 表示 +20% |

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
