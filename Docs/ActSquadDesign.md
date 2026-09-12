# ACT 小队方案（对照绝区零）

> 状态：**M4.0 已落地**（三槽 + Manual 合轴 + 阵亡补位）。M4.1+ 未开工。对应路线图 M4 / §四。
> 结论：拍 **B 三人小队**。A 单角色切模型只做本方案 §4.2 的子集，不做支援。
> 关联：`ActCombatRoadmap.md` §四 / §六·3、`ActBuffLearningBacklog.md` §8–9、`ActEnemyAiDesign.md`（导演焦点）、招架 `CombatParry`、连携 `CombatChainSkill`。
> 硬约定沿用 `ProjectConventions.md`：四根钟、`Enqueue→Gate→Session→XC`、`CombatStateDirector` 唯一写态、表现不进 `DamageAction`、数字走 Luban、轴走 Flux、热路径零 GC。

---

## 〇、一句话

场上只操作一人；普攻/技能中切人时旧人把当前轴打完再退场。手动换人不打一刀；有刀的是支援 / 招架 / 连携。

---

## 一、拍板

| 项 | 结论 | 理由 |
|---|---|---|
| 定位 | **B 三人小队**（ZZZ 本体） | 招架已按「极限支援模型」落地；连携注释写明「三人轮转不在本切片」。再做单人切模型会把已接的黄闪/连携做成半成品。 |
| 同屏人数 | **1 主控 + 最多 1 个退场中** | 候场隐藏。普攻/技能中 Manual 合轴时旧人留场演完当前轴（ZZZ）。禁止两人同时 Exiting，避免三具身体。 |
| 换人输入 | 独立按键，**不进技能槽** | 换人是小队指令，不是当前角色的一次 Enqueue。 |
| 手动换人 | **无入场攻击**，Warp 后 Idle | 对齐 ZZZ：普通切人是画面后冲刺进场，不打一刀。有攻击的只有快速支援 / 招架支援 / 回避支援派生 / 连携。 |
| 换人锁 | **无技能式 CD**；不能切回还在退场的人 | ZZZ 无 CD，未退场角色按钮变灰。现有 5s 弃用。表现锁只挡住入场冲刺那一下。 |
| 支援点数 | **团队 3 点**，黄/红支援各耗 1，快速支援不耗 | **没点 = 换人失败**（不降级 Manual、不白嫖无敌）。黄闪仍可当前角色按 L 招架，红闪自己闪。不抄 ZZZ「黄变红再换人闪避」。 |
| 硬控中换人 | **允许 Manual**（瞬间隐藏）；普攻/技能中 **合轴** | 见 §5.6 / §5.7。连携 / 大招 / 支援突击轴禁止切。 |
| 第一版角色 | **同一预制体 ×3** | 先跑通三槽与换人，M4.4 前再分槽。 |
| 喧响 | **团队计量**，挂小队实体，不挂出场角色 | 换人不能把大招条带走或清掉。本方案只留接口，本体进 M4.5 / 路线图 §六·3。 |
| 延奏 | **换人成功后的 Buff 层**，不是第四种支援 | ZZZ 没有鸣潮「延奏/协奏」这个词。本项目要叠：ZZZ 换人攻防 + 鸣潮 `Buff.Outro` 转移。 |
| 开工顺序 | 建议 **M2 招架体感稳了再进 M4.0** | M4.2 极限支援直接复用 `CombatParry`。M4.0（三槽+常规换人）可与 M3 异常并行。 |

路线图 §九 第 1 条由此收口：喧响是团队资源。

---

## 二、绝区零对照（学什么、不学什么）

ZZZ 小队拆成六块，本项目已有挂钩如下。

| ZZZ | 玩家体感 | 本项目落点 | 本切片 |
|---|---|---|---|
| 上场 / 候场 | 一人在场，两人肖像 | `CombatSquad` 三槽 + 隐藏/暂停 | M4.0 |
| 手动换人 | 冲刺进场，无攻击 | Warp + Idle + `SwitchIn` 表现包，**不 Enqueue** | M4.0 |
| 快速支援 | 被打 / 队友技能 Cue 后换入打入场技 | 支援窗 + `QuickAssist` 轴 | M4.1 |
| 防御支援（黄闪） | 候场角色入场撞刃 | `CombatParry.TryCommit` 的换人版本 | M4.2 |
| 回避支援（红闪） | 候场角色入场带 i-frame 闪过 | 新模式，复用闪避 Tag 语义，不走招架成交 | M4.2 |
| 连携技轮转 | 失衡窗内换人各打一段连携 | `CombatChainSkill` 从写死 13001 改为读槽位 | M4.4 |
| 支援点数 | 黄/红支援的次数闸 | 小队计量，**不是 Buff** | M4.2 |
| 喧响 / 终结技 | 团队大招条 | 小队计量 + 已有 Ultimate 槽 / `UltimateCinematic` | M4.5 |
| 属性 / 紊乱 | 换人换属性打条 | 依赖路线图 §二，小队只保证「当前出场角色的段表属性」生效 | 不单做 |
| 阵营被动 Additional Ability | 配队光环 | 后置；先不做 | 不做 |

**明确不抄：**

- 不把候场角色留在场上当 AI 队友（不是双人同屏操作）。
- 不做鸣潮协奏「能量条满才能延奏」；延奏只跟换人成功走 Tag 转移。
- 不把换人做成 `PlayerManager.SwitchCameraToPlayer` 的升级——那个 API 只切镜头，实体仍站在原地。
- 不在 `DamageAction` 里播入场特效。
- **不抄「黄变红」**：ZZZ 支援点耗尽后黄闪改红闪，换人变成极限闪避。本项目没点就是失败，L 招架 / 自己闪仍可用。
- **合轴要抄、但加闸**：普攻/技能中旧人演完再退；同时最多 1 个 `Exiting`。不做三人同时出招。

---

## 三、现状（代码里真正有的）

| 已有 | 现状 | 小队怎么用 |
|---|---|---|
| `PlayerManager` | 生成 1 个 `isTruePlayer`；`AddFakePlayer` 再造一个站在旁边的人；`SwitchCameraToPlayer` + 5s CD 只切跟随 | **扩成小队入口**，不要新 Singleton。切镜头 API 降级为调试。 |
| `isTruePlayer` / `GetID(true)→0` | 主控是隐式单例；假玩家不能是 true，否则 NetId 撞 0 | 拆成「阵营」和「当前主控」。NetId 生成后不变。 |
| `ConfigurableInputManager._curPlayer` | 技能键只写当前 `IAttackPlayer` | 换人键在这里拦下，不进 `InputBuffer`。换人成功后 `ChangeCurPlayer()`。 |
| `SkillSlotRuntime` / `CharacterSlotConfig` | 已按角色覆盖槽位；注释写了「换角色时 Load」 | 支援/连携/招架走槽位覆盖。手动换人无技能槽。第一版三实例同一配置。 |
| `CombatParry` | 黄窗在来刀上；成交吸附 + 11005；`IsThreatened` 认 defender 或任意 `isTruePlayer` | 极限支援：成交者改成**换入角色**，来刀仍算原受击者。 |
| `CombatChainSkill` | 单人 13001；注释「三人轮转 / 喧响不在本切片」 | 窗口判定保留；SkillId 改读出场角色 Chain 槽。 |
| `CombatEncounterDirector.FocusTarget` | 每帧 `LocalPlayer.Combat` | 换人后改 `LocalPlayer` 即换焦点，不必新接口。 |
| `CombatFxPackageId.SwitchIn` / `SwitchOut` | 包已占位，入场只有短 HitStop | 换人播包；连携仍走 `ChainAttack`。 |
| `BuffExpirePolicy.OnSwitchOut` | 枚举占位，未挂钩 | 退场卸带该政策的 Buff。 |
| `TransferByTag("Buff.Outro")` | Buff 文档 §9 未做 | M4.3：退场 → 下场 `AddStatusAction`。 |
| `CombatMeterComponent` | 挂敌人，玩家未 Configure | 支援点 / 喧响挂 **小队实体**，不要挂角色（换人会错边）。 |
| `MainUIPanel` 切人按钮 | 调 `SwitchCameraToPlayer` | 改调 `CombatSquad.TrySwitch`。 |

核心缺口：**没有「场上一人」的运行时**。现在多人是并立战斗实体，输入/镜头碰巧只跟其中一个。

---

## 四、运行时结构

### 4.1 放哪

不新建 `TeamManager` 单例。

```
CombatContext
  └─ CombatSquad          ← EGamePlay Entity，三槽 + 团队计量 + 支援窗 Tick
PlayerManager             ← 生成 ActPlayer、绑定槽位、改 LocalPlayer / 相机 / 输入
CombatEntity              ← 加小队标记；不引用 ActPlayer
```

- `CombatSquad` 只持有 `CombatEntity`，遵守「EGamePlay.Combat 不引用 `ActPlayer`」。
- 显隐、Warp、相机、输入在 `PlayerManager`（或薄的 `SquadPresenter`）。
- 生成仍走 `PlayerManager.SpawnActPlayer` + 对象池。

### 4.2 身份（第一刀必须改，否则换人全错）

| 字段 | 含义 | 何时变 |
|---|---|---|
| `NetId` | 实体身份 | 生成时分配，**之后不变**。`GetID` 改为一律递增，取消 `true→0` 特例。 |
| `IsPlayerSquad` | 玩家阵营（小队成员） | 生成时写死。`UsesPlayerCombatClock` 认这个（或 clock hold），不认 `isTruePlayer`。 |
| `isTruePlayer` | **当前主控** | 换人时立刻转移。合轴期间旧人已不是 true。 |
| `SquadSlot` | 0/1/2 | 生成时写入。 |
| `SquadPresence` | `OnField` / `Exiting` / `Bench` | 见下。 |

候场三人不要用 `AgentTag.PlayerB` 当「队友」——`PlayerB` 继续留给调试假玩家。小队成员一律玩家阵营（现有 `PlayerA`，或后续把 AgentTag 收成 Player/Enemy）。`XCEvent` 里「PlayerA∥PlayerB 算焦点侧」要改成问 `IsPlayerSquad`。

`LocalPlayer` / `LocalNetId` / `CurrentFollowNetId` 跟随 **主控**（`OnField`），不是 `Exiting`。

`SquadPresence`：

| 态 | 可见 | 输入 | 盒/受击 | 技能轴 |
|---|---|---|---|---|
| `OnField` | 是 | 是 | 是 | 正常 |
| `Exiting` | 是 | 否 | 是（无 i-frame 的招仍挨打） | **继续跑完当前轴** |
| `Bench` | 隐藏 | 否 | 否 | 停 |

`IsBench` ≡ `SquadPresence == Bench`。合轴窗口场上最多两具身体：1 主控 + 1 退场中。

### 4.3 候场暂停（Buff 文档 §9）

候场 **GameObject 隐藏 + 关碰撞**，实体仍留在 `CombatContext` 列表，`Bench` 时跳过大部分 Tick。

`Exiting` **不停** 电机/动画/盒——那是合轴。只停 `TickPendingSkillInput`（没人再给旧人按键）。

| | OnField | Exiting | Bench |
|---|---|---|---|
| 技能 CD | 走 | 走 | 走 |
| 当前技能轴 | 走 | **走完** | 无 |
| 输入 / 新 Enqueue | 走 | 停 | 停 |
| 受击 / 盒 | 走 | 走 | 停 |
| 跳伤 ET | 走 | 走 | 暂停 |

Buff 持续：候场跳伤与计时暂停。CD 候场仍转。

`CombatContext.Update`：`Bench` 只让 CD 更新。不要只 `SetActive(false)` GO 当唯一手段。小队成员之间关角色碰撞（合轴时两胶囊重叠会顶飞）。

### 4.4 团队计量（不是 Buff）

挂在 `CombatSquad` 上，复用计量语义（上限 + 当前 + 事件），**不进 `IdStatuses`**。

| 条 | 谁加 | 谁花 |
|---|---|---|
| 支援点（0–3） | 世界钟回复 | 黄支援 / 红支援 各 1 |
| 喧响（后做） | 连携、招架、极限闪、高光行为 | 终结技一次清空（拍板：满条一次性） |

角色 EP（特殊技能量）仍在角色 `Vital` / Gate 资源上，候场冻结回复。

---

## 五、换人模式

换人是小队指令，统一入口：

```text
CombatSquad.TrySwitch(slot, SwitchReason) → SwitchGate → 退场 → 入场 → 按原因决定是否 Enqueue
```

| `SwitchReason` | 触发 | 换入轴 | 表现锁 | 支援点 |
|---|---|---|---|---|
| `Manual` | Q/E 或肖像，无黄/红/支援/连携窗 | **无**。Warp 后 Idle | 约 0.3s | 不耗 |
| `QuickAssist` | 支援窗内换人 | `QuickAssist` | 免（吃入场轴） | 不耗 |
| `DefensiveAssist` | 黄闪来刀 + 换人 + 有点 | 该角色招架轴（默认 11005）+ `CombatParry.TryCommit` | 免 | 1 |
| `EvasiveAssist` | 红闪来刀 + 换人 + 有点 | 短 i-frame 轴（可先复用闪避） | 免 | 1 |
| `Chain` | 失衡窗内换人或连携键切下一人 | 该角色 `SkillSlotId.Chain` | 免 | 不耗 |
| `Death` | 出场阵亡且有活着的候场 | 无攻击轴，落地 Idle | 免 | 不耗 |

**Gate（顺序，失败即停，不降级成别的 Reason）：**

1. 目标槽存活、非当前槽、非 `Exiting`（ZZZ：未退场角色按钮灰，不能切回去）。
2. 已有另一人 `Exiting` → Manual 失败（最多 1 个退场中）。
3. 当前轴若是连携 / 大招 / 支援突击（招架后的派生）→ 禁止换人。
4. 推断为 `DefensiveAssist` / `EvasiveAssist` 时：必须有来刀窗 **且** 至少 1 支援点；否则 **整次换人失败**（不改走 Manual）。
5. 目标未在死亡溶解中。
6. `Hit` / `Control`：**允许** Manual（瞬间隐藏）；若已开快速支援窗则走 `QuickAssist`。

换人 **不是** 当前角色 `AbilityActivationGate` 的一次技能。只有换入者稍后那次支援/招架/连携才走 Gate。

### 5.1 一次换人的时序

主控转移 **同一帧** 完成。旧人是否立刻隐藏看原因和当前轴（§5.7）。

```text
TrySwitch 通过
  → OnSwitchOut / Outro 在「交出主控」时开火（不等旧人演完）
  → 旧：isTruePlayer=false，停输入
  → 新：isTruePlayer=true，LocalPlayer / 相机 / 焦点
  → Manual + 旧人正在普攻/技能/闪避轴上：
        旧 → Exiting，不断轴、不隐藏
        新 → 在旧人身侧/镜头后方偏移出现（不要叠同一个胶囊）+ SwitchIn 冲刺包，Idle，不 Enqueue
  → Manual + 旧人 Idle / Hit / Control：
        旧 → 立刻 Bench（隐藏）
        新 → Warp 到旧位置 + SwitchIn 包
  → 支援 / 招架 / 连携 / 阵亡：
        旧 → 断轴并立刻 Bench
        新 → 按原因 Enqueue 或落地
```

禁止：先切相机再过 1 秒才改 `isTruePlayer`。Manual 冲刺进场只是表现包。合轴时导演焦点已经是新人，敌人打主控；旧人剩余盒仍打得到怪，自己没 i-frame 也会挨打。

### 5.2 快速支援窗

开窗（世界钟，建议 1.0–1.3s），对齐 ZZZ **受击支援 = 击飞/击退**，不是每一刀挨打：

1. 出场者被打出击飞 / 击退（短硬直 Hit 不开窗，此时仍可 `Manual`）。
2. 时间轴 Msg：`MsgName=AssistCue`（技能驱动的换人提示，对齐放完强化特殊技后的三角）。

窗内换任一存活候场 → `QuickAssist`。窗到期仍可 `Manual`。

UI：肖像高亮。不要做自动换人。

### 5.3 极限支援（黄）与回避支援（红）

黄闪继续只问 `TelegraphKind.Parry` + `Ai.Parryable`，与 M2 相同。

| 输入 | 黄闪 | 红闪（`Unblockable`） |
|---|---|---|
| L / 招架槽 | 当前角色 `CombatParry.TryCommit`（已有） | 不成交 |
| 换人 + 有支援点 | 换入者 Warp 到刀路，`TryCommit(incoming)`，播该角色招架轴 | 换入者带 i-frame 进场，**不成交招架**、不吸附撞刃（或只闪到身侧） |
| 换人 + 无点 | **整次失败**，不换人、不无敌 | **整次失败**，必须自己闪 |

`CombatParry` 要改的点：

- `TryCommit(executor)`：吸附和 11005 的是 **executor**；来刀 `Defender` 仍可以是刚下场的人。
- `IsThreatened`：问「defender 是当前焦点或同小队」，不要 `isTruePlayer` 万能匹配。
- `IsConsumedHit`：后续盒打到 **下场者或换入者** 都消招架，避免换人帧对面打空旧身体再打新人。
- `NotifyPlayerParry` 仍走导演 Relax，时长跟换入者招架轴。

红闪支援 **不** 走 `ApplySuccess`（不加招架失衡、不断黄闪那种轴）。只给换入 i-frame + 短轴。红闪漏了仍普通受击。

黄/红窗内没点时 **不要** 把 Reason 降成 Manual：否则等于用换人吃刀。UI 可闪一下「支援点不足」。

### 5.4 连携轮转

M1 已有：窗内按连携槽打 13001，`ChainCount` 次。

小队后：

- `CombatChainSkill.SkillId` 不再写死，读 **出场角色** `SlotRuntime.GetSkillId(Chain)`。
- 失衡窗内换人 → `SwitchReason.Chain`（若该角色还没在本窗口打过连携）。
- 次数仍是敌人 `CombatMeter.TryConsumeChain`，与角色数解耦；三人队把杂兵/精英 `ChainCount` 配成 2–3。
- 同一窗口同一角色不能连打两次（小队侧记 `chainedMask`）。
- 连携键（现 ButtonA）在窗内且目标合法：优先当前角色连携；若当前已打过、还有次数，可提示换人而不是再打 13001。

喧响满条终结技仍是 Ultimate 槽，不和连携键混用。

### 5.5 阵亡

出场者 `ApplyDeath`：若有存活候场 → `SwitchReason.Death`（不延奏、不入场技）。全灭走现有失败/结算。候场死亡（理论上不挨打）只把槽位标 Down，不能换入。

### 5.6 硬控 / 受击里能不能切（对照 ZZZ）

ZZZ 实际规则（二测拆解 + 战斗 wiki）：

| 场上状态 | ZZZ | 本项目 |
|---|---|---|
| Idle | 可切；新人冲刺进场、无攻击 | 可切；旧人立刻隐藏，新人 Warp |
| 普攻 / 技能 / 闪避 | 可切；**旧人把当前动作演完再走**；无 i-frame 仍挨打；不能切回未退场的人 | **合轴**（§5.7）：`Exiting` 跑完当前轴再 Bench |
| 被打短硬直 | 仍可切 | `Hit` 允许 Manual，**立刻隐藏**（不解成合轴，避免硬直身体留场） |
| 被击飞 / 击退 | 换人变成受击支援 | 开快速支援窗 → `QuickAssist`（M4.1） |
| 玩家被眩晕/冻结 | 玩家长硬控很少 | `Control` 允许 Manual，立刻隐藏 |
| 支援突击 / 终结技 / 连携演出 | 全程不能切 | 这些轴上 Gate 拒绝 |
| 换人 CD | **无** | 无技能 CD；不能切 `Exiting` 角色 |

Manual 切走硬控时：**不要**给换入者长 i-frame。黄闪没点仍按 §5.3 整次失败。

### 5.7 合轴（普攻 / 技能中 Manual）

学 ZZZ：换人不是取消当前招，是「人先走、招打完」。强化特殊技后摇里切人，就是用换人吃后摇。

**谁合轴：** 仅 `SwitchReason.Manual`，且旧人 `ActiveExecution` 未结束（普攻、特殊技、闪避等）。招架演出 / 连携 / 大招 / 支援突击不走这条（Gate 已禁或走立刻 Bench）。

**旧人 `Exiting`：**

- 不断当前 XC 轴，盒继续结算，RootMotion 继续。
- 不再收输入，不能自己闪、不能再 Enqueue。
- 无 i-frame 的招，被打会断轴（现有 Poise/Interrupt）；断完或轴正常结束 → **立刻 Bench**，不要在场上变第二具 Idle。
- 合轴期间死亡：该槽 Down，走死亡，不把尸体留成主控。

**新人：** 立刻主控。出现点 = 旧人身侧或镜头后方偏移（约 1.5–2m），不要 Warp 进同一胶囊。只播 SwitchIn 冲刺，落地 Idle。

**闸：**

- 同时最多 1 个 `Exiting`。再 Manual 失败（肖像灰）。
- 不能把目标选成正在 `Exiting` 的槽（切回去灰掉）。
- 小队成员互相忽略 CharacterController 碰撞。

**结束回调：** `ActSpellSession` 销毁 / `IsMainFinish` 后摇播完 → `CombatSquad.NotifyExitComplete`。受击断招导致 Session 结束同样走这条。

这是 M4.0 的一部分，不是后置进阶。

---

## 六、接缝（必须守住）

| 系统 | 约定 |
|---|---|
| 出招 | 支援技 / 招架 / 连携 / 闪避走 `Enqueue→Gate→Session→XC`。**Manual 不占轴、不 Enqueue。** |
| 状态 | 退场不断 `CurState` 去新写一套。合轴旧人继续 Skill 直到轴结束再 Bench。Manual 新人进 Idle。 |
| 导演 | 只认 `LocalPlayer`（新主控）。合轴旧人不是焦点，但其盒仍能打到怪。Punish 不因换人取消。 |
| 招架 | 不问技能名，问来刀窗 + Tag。极限支援只换 **谁** 去成交。 |
| 时钟 | 小队成员始终玩家钟（含候场 CD）。Buff 跳伤仍世界钟，但候场暂停定时器。不第五根钟。 |
| Buff | 退场：`OnSwitchOut` 卸；`Buff.Outro` 转移用 `AddStatusAction`（走 PreGive/PreReceive），禁止直接改列表。候场 DoT 暂停。 |
| 表现 | `SwitchIn`/`SwitchOut`/`ParrySuccess`/`ChainAttack` 走 PackagePlayer。禁止结算里 `Instantiate`。 |
| 相机 | `ChangeCurFollowTarget` + 短 `CameraIntent`。不抢 Cutscene。混合构图仍是相机文档 P2，本方案不依赖。 |
| 输入 | 换人键不进 `SkillSlotConfig`。现有 X/Y/A/B 继续给出场者。 |
| 热路径 | 三槽固定数组。无 LINQ。换人不是每帧，允许一次性 Warp / 显隐。 |

---

## 七、数据与轴

### 7.1 槽位

`SkillSlotId` 增补（现有 0–7 不动）。**不设手动入场攻击槽。**

| Slot | 用途 | 缺省 |
|---|---|---|
| `QuickAssist = 8` | 快速支援 / 受击支援反击 | 第一版可三人共用一条短反击轴 |
| `EvasiveAssist = 9` | 红闪支援 | 回退闪避轴 |
| `Chain = 6` | 已有，改按角色读 | 13001 |
| `Parry = 7` | 已有，极限支援复用 | 11005 |

`CharacterSlotConfig`：第一版 **三个实例同一份配置、同一个玩家预制体**，只验证换人流程。分角色覆盖放到 M4.4 前。

`SkillSort`：快速支援走 `Speical`/`Weapon`，不要高于闪避。招架支援仍 `Parry=2800`。

### 7.2 Luban

新表 `SquadSetting`（一行全局，禁止手改 json）：

| 列 | 建议默认 | 说明 |
|---|---|---|
| SwitchLock | 0.3 | 仅 Manual 表现锁，不是技能 CD |
| AssistWindow | 1.2 | 快速支援窗（秒，世界钟） |
| AssistPointsMax | 3 | |
| AssistPointRegenPerSec | 0.12 | 约 8s 回 1 点 |
| SwitchIFrame | 0.4 | |
| AssistPointDefensiveCost | 1 | |
| AssistPointEvasiveCost | 1 | |

角色数字（入场技 Id）**不要**进段表；那是槽位配置。段表继续只管倍率 / 失衡 / 以后的属性。

延奏：Buff 表 `BuffTag` 加 `Buff.Outro`。卸场转移，不新建 Buff 运行时类型。

`OnSwitchOut`：Buff 表加 `Buff.Bind.SwitchOut`（或认已有政策位），`ResolveExpirePolicy` 挂钩。

### 7.3 时间轴

支援技 / 连携 / 招架 = 普通技能轴。手动换人 **不改轴**。

```
【技能编辑器待改】
- SkillId: 快速支援反击（第一版三人共用一条）
- 预制体: Assets/Editor/SkillSequences/{id}.prefab
- 导出后产物: Assets/Game/Config/SkillDataScriptable/{id}.asset
- 改动:
  1. 不要做「手动换人斩击」轴。普通切人只播 SwitchIn 表现包。
  2. 快速支援：一段短反击（可带重击/硬直），盒+段号走段表。
  3. 需要技能驱动开支援窗的轴：MsgName=AssistCue，FloatdMsg=窗长（0=用表默认）。
  4. 11005 继续缩短（路线图 §三），极限支援复用它。
- 不要改: SkillData Scriptable
```

红闪支援第一版可无新轴：Warp + `Buff.Roll` / `Combat.SwitchIFrame` 0.4s。

---

## 八、输入与 UI

**输入（须在 InputActionAsset 里加，代码枚举才能跟）：**

| 动作 | 建议键 | 处理 |
|---|---|---|
| Switch1 / Switch2 | Q / E 或 1 / 2 | `CombatSquad.TrySwitch`，Reason 由当前窗推断 |
| 招架 L | 已有 ButtonB | 仍只给出场者 |
| 连携 | 已有 Chain 槽 | 窗内打当前角色连携 |

Reason 推断优先级：`DefensiveAssist`（黄窗）> `EvasiveAssist`（红窗）> `Chain`（失衡窗且该角色未打过）> `QuickAssist`（支援窗）> `Manual`。

**UI：** 三肖像 + HP + 支援窗描边 + 支援点数。`Exiting` 肖像变灰，不能点回去。没点时黄/红窗内按换人给失败反馈。

调试：`SkillEditorScene` 加键生成 3 人小队（不要在 `PlayerManager` 里轮询按键）。

---

## 九、表现

| 时机 | 包 / 镜头 |
|---|---|
| 手动换人 | `SwitchOut` + `SwitchIn`（冲刺进场 / 短 HitStop，无刀光） |
| 快速支援 | SwitchIn + 略强震（可暂共用） |
| 极限支援 | 已有 `ParrySuccess`；撞刃吸附已有 |
| 连携 | 已有 `ChainAttack` + `CameraIntent` |
| 红支援 | SwitchIn + 闪避残影（可先残影） |

相机：跟随目标同一帧换到新人；支援/连携再 `SubmitIntent(SkillCamera, …)`，priority 低于过场。不要 `Time.timeScale`。

---

## 十、明确不做（防膨胀）

1. 候场角色 AI 助攻、协同攻击盒。
2. 三人同屏、联机各操作一人。
3. 自动快速支援。
4. 部位破坏、阵营配队被动、紊乱（跟 §二）。
5. 第五根钟、换人用全局 `timeScale`。
6. 为延奏新建 Action 类型（用现有 `AddStatusAction`）。
7. 手改 `SkillDataScriptable` / Luban json。
8. 把 `SwitchCameraToPlayer` 当正式换人留下来双轨。
9. 超过 1 个 `Exiting`、三人同时受玩家输入、候场 AI 助攻。
10. 支援点耗尽后黄闪变红、换人改极限闪避。
11. 手动换人打入场攻击（原神/鸣潮 Intro）。

---

## 十一、里程碑与验收

| 步 | 内容 | 验收 |
|---|---|---|
| **M4.0** 三槽 + 常规换人 + 合轴 | 身份拆分、同预制体 ×3、Bench 暂停、Manual 无轴、`Exiting` 跑完当前轴 | Idle 切人立刻换；普攻中途切人旧人把刀打完再消失、新人立刻可操作；不能切回未退场者；Hit/Control 立刻隐藏 |
| **M4.1** 快速支援 | 受击/AssistCue 开窗，窗内换人打入场技，免 CD | 被打出提示，换入有反击轴；窗外是 Manual |
| **M4.2** 黄/红支援 + 支援点 | 换人版 `TryCommit`；红闪 i-frame；3 点消耗与回复 | 黄闪换人撞刃同 M2；红闪换人不招架；**没点换人失败**（不降级）；L 仍可自己招架 |
| **M4.3** 延奏 | `OnSwitchOut` 挂钩 + `TransferByTag(Buff.Outro)` | 退场卸绑定 Buff；下场者吃到 Outro；候场列表不 Tick 跳伤 |
| **M4.4** 连携轮转 | Chain 读槽位；窗内换人打各自连携；每角色每窗一次 | `ChainCount=3` 时可三人各一段；次数用尽不能再连携 |
| **M4.5** 喧响 | 小队计量 + Ultimate 槽消耗整条 + `UltimateCinematic` | 满条任一出场者可大招；换人不丢条 |

**M4.0 建议自测路径：** 三人同模型开局 → Idle 换人立刻只剩一人 → 普攻中途换人，旧人把这一刀打完再隐藏、新人立刻能走 → 再按切回旧人失败（灰）→ 无霸体普攻合轴时旧人会被打断然后隐藏 → Hit/Control 换人旧人立刻消失。

---

## 十二、建议代码落点（M4.0 已按此拆；M4.1+ 仍待做）

| 新增 | 职责 |
|---|---|
| `CombatSquad` | 三槽、Presence、Gate、Reason、支援窗、支援点、`NotifyExitComplete` |
| `CombatSquadSwitch`（静态也可） | 退场/入场顺序，保持无分配 |
| `StatusComponent.NotifySwitchOut` / `TransferByTag` | Buff 文档 §8–9 |
| Tag `Combat.SwitchIFrame`、`Buff.Outro`、`Buff.Bind.SwitchOut` | `TagCollection` + 表 |

| 改 | 要点 |
|---|---|
| `PlayerManager` | 开战生成 3 槽；换人改 LocalPlayer；`SwitchCameraToPlayer` 标调试 |
| `CombatEntity` | `IsPlayerSquad` / `SquadPresence` / `SquadSlot`；钟认阵营；Bench 跳过电机与输入；Exiting 停输入不停轴 |
| `CombatParry` | executor ≠ original defender；小队威胁判定 |
| `CombatChainSkill` | SkillId 来自槽位 |
| `CombatHitResolver` | SwitchIFrame → Immunity |
| `ConfigurableInputManager` | Switch1/2；`ChangeCurPlayer` 在换人后 |
| `SkillSlotId` / `CharacterSlotConfig` | 新槽 |
| `MainUIPanel` | 肖像换人 |
| `ActSpellSession` | 结束时若宿主 Exiting → `NotifyExitComplete` |
| `CombatContext` | Bench 的 Update 裁剪 |
| `BuffTimeComponent` | Pause/Resume（候场跳伤） |

`NormalActPlayer.LoadCharacterSlots` 已存在。第一版三个实例塞 **同一份** `CharacterSlotConfig` 即可。

---

## 十三、已拍板

| # | 题 | 结论 |
|---|---|---|
| 1 | 手动换人有没有入场攻击 | **无**。只 Warp + Idle + 冲刺表现包。有刀的只有支援 / 招架 / 连携。 |
| 2 | 没支援点时黄/红闪换人 | **失败**。不降级 Manual，不给无敌。 |
| 3 | 硬控中能否 Manual | **允许**（Hit/Control 立刻隐藏）。击飞走快速支援。连携/大招/支援突击轴上禁止。 |
| 4 | 第一版角色包 | **同预制体 ×3**。 |
| 5 | 喧响消耗 | 满条一次性（沿用路线图）。 |
| 6 | 普攻/技能中换人 | **合轴**：旧人 `Exiting` 演完当前轴再 Bench；最多 1 个退场中；不能切回去。 |

M4.0 可以按此开工，不必等喧响和异常。
