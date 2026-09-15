# ACT 角色技能配置收口（对照绝区零）

> 状态：**阶段 0–4 已接运行时**（表 + 4 键 Click/Hold 互斥 + Kit/Z 窗 + 支援点 + 被动 `PassiveBuffIds` + **任意槽位行 Empowered 预检改写**）。闪避反击的极限闪避 Tag 授予未做。**多档蓄力 / 松手结算只是方案（§十），未落地。绝区零失衡硬直 / Z 连携已屏蔽**，高潮改偏谐见 `ActHarmonyBreakDesign.md`。
> 关联：`ActSkillConfigAndLeveling.md`（段表与升级）、`ActCharacterKitTemplate.md`（新角色填表）、`ActSquadDesign.md`（换人 Reason；**输入以本文为准**）、`ProjectConventions.md`。
> 养成 UI、命座、驱动盘不在本文。敌人招池仍走 12000 号段 + AI，不进玩家 `CharacterSlot`。

---

## 〇、一句话

当前角色只有 **4 个战斗键**（每键 Click / Hold 互斥），加上候场 **2 个 Z 键**。  
招式包按「角色 × 键 × 按法」进 Luban；Z 触发的招架 / 支援写在 `CharacterKit` 列，**不进** `CharacterSlot`。连携已屏蔽；场上 F 谐度破坏见 `ActHarmonyBreakDesign.md`。  
**空就是没有**：缺行、SkillId=0、Kit 列=0 都不回退系统默认。

---

## 一、已拍板

| 项 | 结论 |
|---|---|
| 战斗键 | **Attack=ButtonX，Dodge=ButtonY，Ultimate=ButtonA，Skill=ButtonB** |
| 特殊键 Z | **Switch1 / Switch2（Q/E）即 ButtonZ / ButtonZ2**：两人候场肖像。无窗=Manual 换人；黄闪=防御支援；红闪=回避支援；支援窗=快速支援。~~失衡窗=连携~~ **已屏蔽** |
| 招架 | **不是面键，不是场上按 L。** 黄闪按某个 Z = 换入该角色并打 `DefensiveAssistSkillId` |
| 连携 | **已屏蔽。** 原「失衡窗 Z = `ChainSkillId`」不再触发 |
| 谐度破坏 | 场上 **F**，读 Kit `HarmonyBreakSkillId`。空列这次失败，不换人、不回退 13001 |
| 槽表主键 | `(CharacterId, Button, Press, FormId, Priority)`。Button **仅** 4 战斗键 |
| EX / 资源改写 | **任意** `CharacterSlot` 行的 `EmpoweredSkillId`（阶段 4）。能量门写在 **Empowered 那条** `SkillDemo`，不是 Kit 判断方法 |
| 闪避反击 | Attack×Click + 极限闪避 Tag，不是新键（阶段 5） |
| 连招链 | **不进 Excel**。运行时从轴 `SkillInputEvents` BFS 收集 |
| 键位硬件 | 全角色同一套 InputProfile。不按角色拆键位表 |
| 被动 | `SkillDemo.PassiveBuffIds` + Kit 点名。映射 SO 已删 |
| 空配置 | 不读 CharacterId=0 系统行；不回退 `const 11005 / 13001`；不把空的 B/A 绑去普攻；红闪空列不复用闪避轴 |
| 单人调试 | **不降级。** 没有合法候场时 Z 失败，不改成场上自己招架 / 连携 |

取消的旧方案：B=招架、A=连携、场上 L 自己招架、空键回退系统默认、Ultimate×Click + ChainWindow 改写连携。

与 `ActSquadDesign` 冲突时，**输入键位以本文为准**；小队 Reason 优先级不变：

`黄闪 DefensiveAssist > 红闪 EvasiveAssist > 支援窗 QuickAssist > Manual`

（原「失衡 Chain」一档已屏蔽，运行时不会再走到。）

没支援点时黄 / 红 Z **整次失败**（不降级 Manual）。点数挂小队实体，开战灌满，惰性回复。

---

## 二、输入

### 2.1 键位

| 逻辑 | `InputListernType` | 硬件（现役） | 作用 |
|---|---|---|---|
| Attack | ButtonX | J / 手柄西 | 普攻；极限闪后仍按 X = 闪避反击；Hold = 蓄力 |
| Dodge | ButtonY | K / 南 | 闪避 |
| Ultimate | ButtonA | I / 北 | **只出终结技**。现役 CharacterId=1 无行则空 |
| Skill | ButtonB | L / 东 | **战技 / EX**。现役无行则空 |
| Assist1 | Switch1（即 Z） | Q | 候场相对 +1 |
| Assist2 | Switch2（即 Z2） | E | 候场相对 +2 |
| Execute | Execute | F | 谐度破坏。情境键，不进 `CharacterSlot`。Kit 列=0 不触发 |
| Jump | Jump | Space | 不进招式包 |

Z **不进** `InputBuffer` / `SkillResolver`。`ConfigurableInputManager` 调 `CombatSquad.TrySwitchRelative(1|2)`。

同一次战斗键按下：当前能匹配到 Hold 行则长按到阈值（玩家钟，默认 0.35s）**入队并吞点按**；提前抬起才点按。无匹配 Hold 行则点按立刻入队，长按**不会**改放 Click 技能。  
这是阶段 3 的「到点就放」。多档蓄力要改成松手才结算，见 **§十**（未落地）。

### 2.2 三层解析

```
ButtonX / Y / A / B + Click|Hold
  → CombatButton          输入层（SkillSlotConfig 只绑键，不持有 SkillId）
      → CharacterSlot 行   同键同按法：Form → Tag+Priority → 命中一行
      → 同行 Empowered     预检（Tag / TriggerFormula / 资源）通过则改写
      → 完整 Gate          再 Enqueue
      → 轴 InputEvents     技能中连招窗

Switch1 / Switch2（Z）
  → CombatSquad.TrySwitchTo(候场槽)
      → Manual：不 Enqueue
      → 其余：换入者 Kit 列 SkillId → Enqueue
```

不要跨按法回退：Hold 能量不够只回到 **该 Hold 行的 SkillId**，不会改放 Click。

提交时解析，不要在 `LoadFromTable` 把 Idle SkillId 写死。`SetSkillId` 仍可钉死点按，钉死后不再走表 / Empowered。

### 2.3 连招窗 InputData（轴上） vs 表

连招链 **不进 Excel**。窗边只描述「这扇窗听哪个键、接到哪一发」。

| 字段 | 放哪 | 说明 |
|---|---|---|
| 窗的帧范围 | **轴** `FSkillInputEvent` | 何时开窗 |
| `ListernType` | **轴** InputData | 听哪个战斗键 |
| `PressType` | **轴** InputData | 这扇窗要点按还是长按（不是 Idle 的 CharacterSlot.Press） |
| `SkillId` | **轴** InputData | 下一发。禁止写进 CharacterSlot 当连招表 |
| `InputTimeout` | **轴** InputData | 窗边预输入寿命；0 = 玩家 `ComboBufferTimeout` |
| `SkillSort` / `Offset` | **`SkillDemo.SkillCategory`** | 运行时 `SkillSortUtil.FromSkillId`。Idle 打断档仍用 `SkillSlotConfig`（键槽身份） |
| `RequiredTags` / `BlockedTags` | **`SkillDemo`** | Gate 已读目标技能。边上不要再抄一份 |
| `InputCallBackType` | **不填** | 连招边固定按缓冲里的 Performed（点按/长按已拆成 PressType） |
| CD / 消耗 / 公式 | **`SkillDemo`** | Empowered / 资源门，不在窗边上 |

旧预制体上仍可能看到隐藏的 Sort / Tag 序列化，忽略即可；不必为清字段去改轴。改 `SkillCategory` 后连招打断档立刻跟着变，不用重导出。

---

## 三、技能页 → 键（填表对照）

| 条目 | 入口 | `SkillCategory` |
|---|---|---|
| 普攻连段 | Attack×Click | BasicAttack |
| 蓄力 | Attack×Hold | HeavyAttack |
| 冲刺攻击 | Attack×Click + 闪避后 Tag | DashAttack |
| 分支 | 轴窗边 | Branch |
| 闪避 | Dodge×Click | Dodge |
| 闪避反击 | Attack×Click + 极限闪避 Tag | DodgeCounter |
| 特殊技 | Skill×Click（B） | Special |
| EX | 同行 `EmpoweredSkillId`（不限 Skill×Click） | ExSpecial |
| 终结技 | Ultimate×Click（A） | Ultimate |
| 连携技 | ~~Z + 失衡窗 → `Kit.ChainSkillId`~~ **已屏蔽** | Chain |
| 谐度破坏 | 场上 F → `Kit.HarmonyBreakSkillId`（0 不触发） | HarmonyBreak |
| 核心被动 / 额外能力 | 无键，Kit 点名 | CorePassive / AdditionalAbility |
| 快速支援 | Z + 支援窗 | QuickAssist |
| 防御支援（招架） | Z + 黄闪 | DefensiveAssist |
| 回避支援 | Z + 红闪 | EvasiveAssist |
| 支援突击 | 招架 / 回避成功后派生 | AssistFollowUp |
| 敌人主动技 | 不进玩家槽 | EnemyActive |

安比 13100 号段示例见 `ActCharacterKitTemplate.md`。不要把招架 / 连携写成第 5 个 `CombatButton`。

---

## 四、表

源：`Tools/Config/Datas/`。禁止手改 json / 生成 C#。导出：`Tools/` 下 `cmd /c gen_code_json.bat`。

### 4.1 `CharacterKit`（`CharacterSetting.xlsx`）

主键 `CharacterId`。读的永远是 **换入角色** 的行。

| 字段 | 说明 |
|---|---|
| Attribute / Faction / CombatRole | 额外能力条件与 UI；段伤害仍读段表 |
| CorePassiveSkillId / AdditionalAbilitySkillId / AdditionalCondition | 被动。第一期 Condition=`None` |
| ExtraPassiveSkillIds | 列表，替代预制体 ExtraPassive |
| DefaultFormId | 0=无形态 |
| ChainSkillId | 表仍保留。Z + 失衡窗 **已屏蔽**，运行时不读 |
| HarmonyBreakSkillId | 场上 F 谐度破坏。**0=这次 F 失败**，不换人、不回退 13001。角色之间可配不同 SkillId |
| QuickAssistSkillId | Z + 支援窗。0=该窗失败 |
| DefensiveAssistSkillId | Z + 黄闪。0=没有招架支援。不要再单列 `ParrySkillId` |
| EvasiveAssistSkillId | Z + 红闪。0=该窗失败 |
| AssistFollowUpSkillId | 招架 / 回避成交后派生。0=无 |

### 4.2 `CharacterSlot`（同文件）

主键 `(CharacterId, Button, Press, FormId, Priority)`。Luban `mode=list`。

| 字段 | 说明 |
|---|---|
| Button | **仅** `Attack` / `Skill` / `Dodge` / `Ultimate`（枚举 `CombatButton`） |
| Press | `Click` / `Hold`（枚举 `CombatPress`，不要用 `PressType`，会和全局枚举撞名） |
| FormId | 0=默认形态 |
| Priority | 同键同按法多行，大的先匹配 |
| RequiredTags | 空=默认 Idle。极限闪避、核心强化等走高 Priority 行 |
| SkillId | 0 或缺行 = **该键该按法没有招** |
| EmpoweredSkillId | 同行资源改写（EX、满 Forte 蓄力等）。0=没有。预检写在 **该 SkillId 的 `SkillDemo`**（`Cost*` / `TriggerFormula` / 技能 Tag）。姿态 / 闪避后变招另开 Tag 行，不要写进别的键的 Empowered |

**禁止** 把 Chain / Parry / Assist / Z 写成 `CombatButton`。  
**禁止** 合并 CharacterId=0 做运行时回退。  
**禁止** 在 Kit / Slot 再加「判断方法」列覆盖 Gate。

解析：硬件键 → `CombatButton` → `CombatPress` → FormId（精确优先，否则 FormId=0）→ `RequiredTags` + Priority → 同行 Empowered 预检 → 当选技完整 Gate。

### 4.3 `SkillDemo` 加列

主动技能、被动技能两个 sheet **列必须一致**（同一 bean）。

| 列 | 说明 |
|---|---|
| SkillCategory | 见 §三。用于 `IsChainSkill` / `IsPlayerParrySkill`，不要再写死 13001 / 11005 |
| OwnerCharacterId | 归属角色；敌人 12xxx 填对应 CharacterId |
| PassiveBuffIds | 被动挂上的 BuffId 列表。`InlineBuffEffectIds` 废弃，列先留着不读 |

### 4.4 现役 CharacterId=1（搬家，不是代码默认）

`CharacterSlot` **只写现在真正有的招**：

| Button | Press | Pri | Tags | SkillId |
|---|---|---|---|---|
| Attack | Click | 0 | | 11001 |
| Dodge | Click | 0 | | 11000 |

不建 Skill / Ultimate / Hold 行。按 B / A 不出招。

`CharacterKit` 把原先绑在 A/B 上的系统技 **显式搬过来**（不写则三人队按 Z 也打不出来）：

| 列 | 值 | 为什么 |
|---|---|---|
| DefensiveAssistSkillId | 11005 | 原 B 招架轴，复用 |
| ChainSkillId | 13001 | 原 A 连携轴，复用 |
| 其余 Z / 被动列 | 0 | 没有就不填 |

11000 / 11005 / 13001 仍是可复用轴，**写进该角色格子才生效**。

### 4.5 不建

Combo 表、每角色键位表、`LongButton*` 配表、场上 Parry 键表、CharacterId=0 系统 Kit。形态表后置（`FormId` 占位）。  
`MinHoldSeconds` / `AutoFireAtMax`：**先不建列**，等 §十对照绝区零 / 鸣潮实机后再加。

### 4.6 `SquadSetting`（`SquadSetting.xlsx`）

主键 `Id=1` 一行全局。支援窗 / 支援点 / 换入 i-frame，不进角色 Kit。

| 列 | 现役 | 说明 |
|---|---|---|
| SwitchLock | 0.3 | 仅 Manual 表现锁（秒），本阶段未接线 |
| AssistWindow | 1.2 | 快速支援窗（秒，世界钟） |
| AssistPointsMax | 3 | 开战灌满 |
| AssistPointRegenPerSec | 0.12 | 约 8s 回 1 点 |
| SwitchIFrame | 0.4 | 红闪换入 `Combat.SwitchIFrame` |
| AssistPointDefensiveCost | 1 | 黄闪耗点 |
| AssistPointEvasiveCost | 1 | 红闪耗点；快速支援不耗 |

---

## 五、号段

| 段 | 用途 |
|---|---|
| 11000–11999 | 现役演示。写进格子才生效，不是全局默认 |
| 12000–12999 | 敌人 |
| 13000–13099 | 系统连携（13001 已占） |
| 14000–14099 | 谐度破坏（现役 CharacterId=1 用 14001） |
| 13100+ | 新角色，每角色 100 号（安比 131xx，漂泊者 132xx） |

块内习惯：x01 普攻入口、x10/x11 特殊/EX、x20 终结、x30 连携、x33 防御支援、x35 闪避反击、x90 核心被动。

---

## 六、配置规范

1. 一 SkillId 一行 Demo；主动技另有一份 Flux 轴。数字只在 `SkillDamage`。
2. `CharacterSlot` 只有 4 个 Button。反击 / 冲刺 / 回路强化普攻用 **Attack 的 Tag 行**；满条改写普攻/蓄力用 **Attack 该按法行的 Empowered**。都不要写进战技行的 Empowered。
3. Z 响应技只写 Kit 列。
4. 不要再写 `ParrySkillId` / 场上招架键。
5. 不要把连携配在 Ultimate 行。
6. Combo 不进 Excel。
7. **空就是空。** 不用 -1，不继承系统行，不回退 C# 常量，不把空的 B/A 绑去 11001。
8. 核心被动走 Buff+Tag，禁止 `if (角色名)`。
9. 玩家 Kit 不引用 12xxx。
10. 无 Hold 行不误放：长按不回退 Click。有可匹配的 Hold 行才延迟点按等抬起 / 阈值。
11. **单人调试不降级。** 无候场时 Z 失败是预期。
12. `SkillSlotConfig.asset` 只保留 4 战斗键的输入绑定，`DefaultSkillId=0`。技能 Id 只来自 Luban。
13. Empowered 是 **同行改写**，任意键任意按法。预检只问 Empowered 技能的 Tag / 公式 / 资源，不问 CD / 硬直 / Sort。完整 Gate 失败不降级到另一按法。EX 与普战技若要共 CD，配同一套 CD，不要靠预检混。
14. 连招窗 InputData 只填键 / 按法 / 下一发 SkillId / 窗边超时。打断档和释放 Tag 读目标 `SkillDemo`，不要在边上重复填。

---

## 七、运行时约定

```
4 战斗键 → InputBuffer(SkillSlotId) → CharacterSlot 行 → Empowered 预检 → Gate → Enqueue
Z/Z2     → TrySwitchTo → Manual 不占轴；否则换入者 Kit 列 Enqueue
F        → CombatHarmonyBreak.TryExecute → 场上角色 Kit.HarmonyBreakSkillId（0 不触发）
```

- `ActPlayer.CharacterId` 必须是表里的 Id（现役预制体 **1**）。0 读不到 Kit / Slot，表现为没招。
- `SkillSettingMgr.GetSkillDemoSetting` 的「缺 Id 回退表第一行」**禁止**用在 Kit / Slot / 时间轴加载。用 `GetSkillDemoSettingOrNull`。
- `CombatParry` / `CombatChainSkill` 不再用 `const SkillId` 当默认绑定。识别用 `SkillCategory`；出招 Id 读换入者 Kit 列。
- 黄闪 / 红闪还问支援点；没点或 Kit 列=0 整次 Z 失败。红闪走 `CombatEvasiveAssist`，不成交招架。快速支援窗由受击 / 轴 `AssistCue` 打开，免 CD、不耗点。
- 4 战斗键点按 / 长按互斥。Hold 只读 `CharacterSlot` 的 Hold 行（含当前 Form / Tag）；无匹配行则 `ResolveIdle(..., LongPress)` 返回 0 并丢掉该预输入，也不回退 Click。
- Idle SkillId **提交时**解析（`SkillResolver` + `GetCharacterSlotRow`）。`LoadFromTable` 只收集 Attach 列表（含各行 Empowered）。
- Empowered 预检：`AbilityActivationGate.PassesEmpoweredPreview`（技能 Tag + `TriggerFormula` + `CanAfford`）。通过打 Empowered，否则打该行 `SkillId`。当选技再走完整 `Evaluate`。
- 连招窗：`TryResolveEdges` 用目标 `SkillCategory` 当打断档，释放 Tag 只问 Gate（`SkillDemo`），不再读边上的 Sort / Tag。
- 轴文件缺失时，招架 / 连携 **不要** 静默换成 11001 / 11003。敌人黄闪 12006 缺轴仍可暂借 12001（敌人内容管线，与玩家空配置无关）。
- `SkillSlotId` 仍是输入缓冲下标：Attack→0，Skill→1，Ultimate→4，Dodge→5。Chain=6 / Parry=7 **不再当战斗键**。
- 热路径不扫全表、不用 LINQ。Kit / Slot 按角色缓存数组后线性匹配（每角色行数很少）。`EGamePlay.Combat` 不引用 `ActPlayer`。

---

## 八、落地顺序

| 阶段 | 内容 | 验收 |
|---|---|---|
| **0** | 表头 + CharacterId=1 迁 X/Y；Kit 显式搬 11005/13001；运行时按 CharacterId 读表；B/A 空；Q/E 按窗推断黄闪/连携 | 当时验收含失衡窗 Q/E 打 13001。**失衡连携现已屏蔽**，该项不再作为回归 |
| **1** | 枚举补 ButtonZ/Z2 别名；红闪 / 快速支援 / 支援点 | 空列失败；没点失败；不降级 Manual。现役 CharacterId=1 的 Quick/Evasive 仍为 0，红闪/支援窗内 Z 失败是预期 |
| **2** | 被动改读 `PassiveBuffIds` | 已删 `PassiveSkillBuffMaps` SO；Kit 点名 19001 才挂 31000 |
| **3** | 接长按 | 无 Hold 行不误放。现役 CharacterId=1 无 Hold 行：点按立刻出 X/Y，长按 J 不会改放 11001 以外的招，也不会把长当点按多放一次 |
| **4** | 任意槽位行 Empowered 预检改写 | 提交时解析；能量够打 Empowered，不够打该行 SkillId；跨按法不回退。现役 CharacterId=1 无 Skill / Empowered 行：按 B 仍空，X/Y 不变 |
| 5 | Attack Tag：极限闪避反击 | 行匹配已接。还差极限闪避 Tag 授予 + Attack 高 Priority 表行。不新增键 |
| 6 | 延奏 / 喧响与 Kit 被动的交叉 | 见小队文档 |
| 7 | 安比 13100 | 只改表+轴 |
| 8 | 形态 / 敌人 MoveSet | 后置。`FormId` 行已参与匹配；形态 SO 点按仍可覆盖槽位表 |
| 后置 | 多档蓄力 / 松手结算 | 见 §十。对照实机后再改表和输入，不提前加列 |

阶段 0 **不再承诺零按键变化**：B 不再招架、A 不再连携。数字升级 F9 仍应有效。

---

## 九、待拍板

1. 终结技与连携是否同 `SkillGroupId`（ZZZ 同页）。第一期可分开。
2. 强化普攻：有回路 Tag 用 Attack×Click 高 Priority 行；只是条够用 Attack 行 `EmpoweredSkillId`。都不要写进战技行的 Empowered。
3. 多档蓄力：§十方案能否直接用，取决于绝区零 / 鸣潮实机（低档到了还按着会不会自动放、满档是否强制放、大招是站桩蓄还是占轴循环）。测完再改列名和默认值。

已拍：空配置不默认绑定；单人调试 Z 不降级成场上招架；不加 Kit「判断方法」列；Empowered 预检不问 CD / Sort；跨按法不回退。

---

## 十、方案：多档蓄力与松手结算（未落地）

对照：绝区零星见雅（极性 + 长按）、鸣潮多档蓄力大招（如绯雪）。**先实机对照，再加表列 / 改输入。本节不是现行行为。**

### 10.1 现状缺口

阶段 3：有 Hold 行 → 全局 `HoldThreshold`（默认 0.35s，玩家钟）一到，**键还按着也立刻入队 LongPress**，再按该行 Tag / Empowered 选招。

因此：

| 需求 | 现在 |
|---|---|
| 极性 / 形态不同，点按与长按各一招 | **可以**：`FormId` 或 `RequiredTags` 分行 + Click / Hold |
| 同一键按得更久换成更强的一招 | **不行**：只有一档 Hold，到点就放 |
| 低档时间到了、条件也满足，但还按着 | **现在就会放掉**，正是要防的 |
| 满档仍按着：自动放 vs 必须松手 | 未建模 |

Empowered 是 **提交那一帧** 的资源改写，不是蓄力计时。不要用「条够了就 Empowered」当第二档：到点提交时条够就会改写，仍然不会按住等待。

### 10.2 两套玩法（实机先分清）

**A. Idle 多档（站桩/可移动蓄力，松手才出招）**  
按下不 Enqueue。按住只涨档、可播姿势 / UI。抬起（或满档且表允许自动放）才在同键 Hold 行里选一发。适合普攻蓄力、战技长按、部分不占轴的大招。

建议选行（落地时）：

```
按下 → 蓄力，不入队
抬起或满档 AutoFire → 同键 Press=Hold 且 Form/Tag 已匹配的行
  → MinHoldSeconds ≤ 已按时长，且 Empowered 预检 / 行 Skill 可用
  → 取最高档（MinHold 更大或 Priority 更高）
  → 当选技完整 Gate 后 Enqueue
```

低档时间到、条件也满足，**只要还按着就不提交**。  
Click = 抬起发生在该键最低档 `MinHoldSeconds` 之前。  
跨按法仍不回退：Hold 结算失败不改放 Click。

建议列（**未建**，名字可随实机改）：

| 列 | 意向 |
|---|---|
| `MinHoldSeconds` | Hold 行最低按时长（玩家钟）。0 = 等同现在「过了点按识别就是这一档」 |
| `AutoFireAtMax` | 仅最高档。默认关：满档仍按着也等松手。开：到最高档自动 Enqueue |

阈值跟 **行** 走，不跟角色名走，也不再靠全角色一个 `HoldThreshold` 一刀切。`CombatPress` **不拆** Hold1/Hold2。极性仍是 Form/Tag 两套行；同一极性里短蓄 / 长蓄是两行不同 `MinHoldSeconds`。

**B. 占轴蓄力（按下立刻进蓄力循环轴）**  
Click/按下先 Enqueue **蓄力轴**（这是一招，不是结算档位）。抬起走轴上 Input 窗，窗里再接不同结束技。原则仍是：**窗开了、键还按着，不要自动 Enqueue 低档结束技**。这是轴窗策略，和 A 的 Idle 选行分开接。

星见雅极性多半是 A 的 Form/Tag + 一档 Hold；若某极性还有短蓄/长蓄，才是 A 的多行 `MinHoldSeconds`。绯雪式大招先看实机是 A 还是 B。

### 10.3 禁止用的做法

- 用 Empowered 表示「蓄更久的那一档」
- 蓄力过程中往身上挂 Tag，让 Idle 在按住时匹配到高 Priority 行并立刻 Enqueue
- `CombatPress` 增加 Hold2/Hold3，让输入层在按住时猜档位
- Kit / Slot 上按角色写判断方法

### 10.4 实机对照清单

去绝区零 / 鸣潮时只记事实，回来再改 §10.2：

1. **低档到了还按着**：会不会自动放出低档？还是必须松手？
2. **满档还按着**：自动放、循环等松手、还是强制打断成最高档？
3. **不同键**：普攻蓄力、战技长按、大招，阈值和「是否自动放」是否各写各的？
4. **极性 / 形态**（雅）：换极性是点按、长按、还是独立状态？换了之后长按招是否换成另一套轴？
5. **大招蓄力**：按下是否立刻占轴播循环（B），还是人还在 Idle（A）？
6. **资源不够**：松手时放低档、整次失败、还是根本进不了蓄力？
7. **被打断**（受击 / 闪避 / 换人）：蓄力清零还是保留？松手还放不放？
8. **点按识别**：多短算点按？和第一档 `MinHold` 是否就是同一条界？

现役 CharacterId=1 无 Hold 行，点按立刻出招；接 §十之前行为不变。

---

## 十一、回看

新角色：`CharacterSlot` 只写有招的键；`CharacterKit` 只写有的被动和 Z 响应。空列不要靠默认。不要新建 `CharacterSlotConfig.asset` 当技能源，不要为招架 / 连携加第 5 战斗键。单人场景按 Z 失败是预期。多档蓄力未对照实机前，不要加 `MinHoldSeconds` 列，也不要改阶段 3 的到点入队。
