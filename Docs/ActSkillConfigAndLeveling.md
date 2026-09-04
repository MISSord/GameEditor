# ACT 主动技能：伤害收口与技能升级

对照鸣潮 / 绝区零的主动技能配法，以及当前项目的 `SkillDemo`、`SkillDamage`、时间轴 Trigger、`DamageAction`。

本文只记 **主动技能的数字从哪来、升级改什么**。Buff 清单见 `ActBuffLearningBacklog.md`。位移权威（意图进电机、碰撞后坐标回写）不在本文。

勾选表示运行时已按本文落地，不是「表里有这个字段」。

---

## 结论

- **角色等级**（已有 `RoleAttri`）管攻击 / 生命 / 暴击白字。
- **技能等级**只改命中段上的 `%攻击`，不换动画、不换盒、不换连招 SkillId。
- 主动技伤害 **只查段表** `(SkillId, SegmentIndex)` + 技能等级。禁止回退全局 `BuffModify` Effect 8。
- 时间轴只填：何时开盒、盒形状、**段号 ≥ 1**、HitGroup。不填倍率。
- 连招多 SkillId（如 11001/02/03）共用一个 **升级组**；养成页升的是组，不是三个技能各升一次。

最终伤害仍是：

`攻击(角色等级) × 段倍率(技能等级) × 防御 / 抗性 / 暴击 / 增伤`

---

## 已对齐的流程（不必推倒）

命中链路方向正确，升级不是新技能系统：

```
输入入队 → ActSpellSession 占轴
  → 时间轴 XC 开盒
  → HitPipeline 入队 / Flush
  → DamageAction 乘区扣血
```

保留：`MotionDirector`、HitPipeline、DamageAction 乘区、Buff / Tag / Gate、CD 计时器、`SkillSlotConfig` / `SkillFormConfig` / 连招链、`Ability` 按 SkillId 一份（`AbilityDefinition` 全局缓存，不按等级 new）。

---

## 当前问题（收口要修）

阶段 1–5 已把 11001–03 的数字源、段号、组等级和近战多窗打完收口。还剩：

1. **`Skill_4(Red_x3)` 等其它 Sequence** 仍可能段号 0；导出时会被校验拦住，不要盲改。
2. **养成 UI**（阶段 6）：现在用 F9 把普攻组打到满级验倍率。
3. **以后的锁头飞弹**：近战盒已不再按锁定过滤；单体弹要单独开关，不要把过滤加回来。

---

## 配置分层

```
SkillDemo（身份，几乎不随等级）
    SkillGroupId ──► 运行时只存这一组的等级
    │
    ├─ 时间轴 Scriptable（何时开盒、段号、HitGroup）
    │
    └─ SkillDamage（SkillId + SegmentIndex）
           RatioByLevel[lv]  ← 唯一伤害数字
```

时间轴不随等级变。高命 / 形态多一段是解锁，不是技能等级，不要塞进倍率数组。

---

## 保留 / 改动 / 先不做

### 保留

| 块 | 原因 |
|---|---|
| 时间轴资产（Anim / Move / Trigger / 连招窗） | 升级不换动作 |
| `MotionDirector`、`HitPipeline`、`DamageAction` 乘区 | 结算顺序已对 |
| Buff / Tag / Gate / CD 计时器 | 与技能等级无关 |
| 槽位 / 形态 / 连招链 | 入口仍是 SkillId |
| `Ability` + `AbilityDefinition` 按 SkillId 缓存 | 不要按等级分实例 |
| `RoleAttri` 角色成长 | 只产攻击 / 生命等白字 |
| `SkillDemo` 的 CD、消耗、Required/BlockedTags、TriggerFormula | 一般不随技能等级变 |
| `BuffModify` 作处理器（上 Buff、资源、护盾） | 主动技伤害不再从这里读倍率 |

### 要改

| 块 | 改什么 |
|---|---|
| `SkillDamage` | 主键 `(SkillId, SegmentIndex)`；`Ratio` 改为 `RatioByLevel` |
| `SkillDemo` | 加 `SkillGroupId`、`MaxLevel`、可选 `SkillCategory`；`EffectIds` 只表示命中额外效果 |
| `AbilityDefinition` | 去掉「没 EffectIds 就塞 Effect 8」 |
| `DamageAction.FillDamageFromFormula` | 主动技只查段表 + 技能等级 |
| 时间轴 Trigger | 段号必须 ≥ 1；空 `EffectIds` = 只出该段伤害 |
| `SkillDamageReader` | 复合键，禁止只按 SkillId |
| 运行时 | `SkillLevelComponent`（按组存等级） |
| 11001–11003 资产 | 段号 / HitGroup 配齐 |
| `PostAcceptedHit` | 近战锁定后不要 Break 后续窗 |

### 明确先不做

1. **用 `base + add` 拟合技能倍率**  
   和 `RoleAttri` 不同。鸣潮 / 绝区零天赋是逐级离散表，不是线性。

2. **技能等级换时间轴、解锁新盒**  
   以后若要「高等级多一段」：时间轴可以一直有这个盒，段表加 `MinSkillLevel`，不够就跳过结算。不要按等级换 Scriptable。

3. **CD / 消耗随技能等级变**

4. **被动（19001 / 19002）随技能等级改 Buff 数值**  
   仍走 `PassiveSkillBuffComponent` + Buff 表。以后是 Buff Numeric × 技能组等级，不进 `SkillDamage`。

5. **段表立刻加失衡 / 异常**  
   等 Meter（见 Buff 清单第 4 条）。

6. **养成 UI**  
   第一期等级 API 与 Debug 设等级即可。

---

## 表结构

### `SkillDemo`（一行一个 SkillId）

现有 Type、CD、消耗、标签、公式 **保留**。

新增：

| 字段 | 含义 |
|---|---|
| `SkillGroupId` | 0 = 自己就是一组。11001/02/03 填同一个（建议 `11001`） |
| `MaxLevel` | 默认 10 |
| `SkillCategory` | 普攻 / 战技 / 大招 / 闪避。给以后套装「普攻伤害+」，第一期可不参与结算 |

`EffectIds`：只挂命中额外效果（上点燃、回能）。主动伤害不要再挂 `SkillHpDamage`。闪避继续空列表。

`InlineBuffEffectIds`：被动用法保持现状，本文不改语义。

### `SkillDamage`（一行一刀）

主键：`SkillId` + `SegmentIndex`（从 1 起）。Luban 索引必须是这两列，不能只索引 `SkillId`。

| 字段 | 随等级？ | 说明 |
|---|---|---|
| DamageType / FormulaType / CanCrit | 否 | 这一刀的属性与公式 |
| `RatioByLevel` | **是** | `1.0\|1.08\|1.16\|...`，下标 = 等级-1；短了用最后一档 |
| OnHitEffectIds（可选） | 否 | 仅这一段要上的额外效果；比盒上填更稳 |

不要再拆 `(SkillId, Segment, Level)` 行表，除非单级还要改属性类型。

查表：`ratio = RatioByLevel[clamp(level, 1, MaxLevel) - 1]`。没有这一段 → 打日志并 **跳过这刀**，不要静默 1 倍。

当前 demo 建议：

| SkillId | 段 | RatioByLevel（可先只填 1 级） | 说明 |
|---|---|---|---|
| 11001 | 1 | `1` 起每级 +0.1，满级 `1.9` | 普攻 1 |
| 11002 | 1 | `1.5` 起每级 +0.15，满级 `2.85` | 普攻 2 第一窗 |
| 11002 | 2 | 同第一窗 | 普攻 2 第二窗（两刀） |
| 11003 | 1 | `3` 起每级 +0.3，满级 `5.7` | 普攻 3 |

### `BuffModify` Effect 8（SkillHpDamage）

保留类型，给非段表的临时伤害用。主动技命中路径 **不读** 它的 `ParamFloat1`。槽位文档不要再把它写成技能倍率源。

---

## 时间轴 Trigger

| 字段 | 规则 |
|---|---|
| `DamageSegmentIndex` | 必须 ≥ 1；编辑器默认改为 1，不再默认 0 |
| `HitGroupId` | 同一击多盒填相同正整数；0 = 按本事件实例去重（两刀） |
| `EffectIds` | **空 = 只结算该段伤害**；非空 = 再执行技能定义里点名的额外效果 |

导出校验：段号 > 0，且段表有 `(本技能 SkillId, 段号)`。

11002 两窗：按两段、HitGroup 各 0（两刀）。若改成「一刀两盒」，则同段号 + 同一 HitGroup，段表只留一行。

---

## 运行时

### `SkillLevelComponent`（挂 CombatEntity）

- 字典：`GroupId → level`，默认 1。
- `GetLevel(skillId)`：读 `SkillDemo.SkillGroupId`（0 则用 SkillId）。
- `SetLevel(groupId, level)`：夹到 `1..MaxLevel`。
- `AbilityDefinition` 无等级字段。
- Play 里 **F9**：本地玩家普攻组在 1 / MaxLevel 间切换，飘字 `LvN`，Console 打各段当前倍率。

### 命中（替换「空 EffectIds = 技能全部效果」）

1. 盒上 `DamageSegmentIndex` + 施法者 `GetLevel(skillId)` 查段表 → 直接 `DamageAction`。
2. 盒 `EffectIds` 为空：结束。
3. 非空：只执行技能定义里点名的额外效果。

指定目标 / 锁定：只影响转向（`OnPreSpell`），**不要**过滤命中盒、也不要在 `PostAcceptedHit` 里 Break 子轴。多盒是否同一击只看 `HitGroupId`。以后若有锁头飞弹，单独给盒加开关，不要复用近战过滤。

---

## 边界（升级改什么、不改什么）

**随技能等级变：** 段倍率。以后可加治疗系数、护盾量、被动 Buff 系数。

**不随技能等级变：** 盒、动画、Root Motion、连招窗、HitGroup、技能 CD、消耗、角色攻击白字。

**高命 / 形态多一段：** 不是技能等级。第一期不做 `MinSkillLevel` 列。

**被动：** 不进段表。见「先不做」第 4 条。

---

## 落地顺序

| 阶段 | 内容 | 状态 |
|---|---|---|
| 1 | Luban：段表复合主键 + `RatioByLevel`；`SkillDemo` 加 Group / MaxLevel；11001–03 配齐段行 | [x] |
| 2 | 结算：只走段表 + 等级；删 Effect 8 回退；空盒只出伤 | [x] |
| 3 | 时间轴：11003 段号 1；11002 两窗两段；编辑器默认段号 1 | [x] |
| 4 | `SkillLevelComponent`，默认全 1；Debug 把某组设到 10 可验证倍率 | [x] |
| 5 | 近战：去掉锁定后 Break 后续窗 | [x] |
| 6 | 养成 UI、被动升级、Meter 列 | 以后 |

1–5 做完，配置模型与鸣潮 / 绝区零一致：天赋页改数组，动作编辑器只填段号。第 4 步没有升级界面也不挡收口。

---

## 待拍板（落地前）

1. **11002 两盒**：本文按 **两段两刀** 写；若要改成同一段多盒，只改资产与段表行数，结构不变。
2. **`MaxLevel`**：可每技能自己填，缺省 10。

回看时：先扫「当前问题」是否还在，再对落地表。未勾选且仍符合当前玩法的，才值得做。
