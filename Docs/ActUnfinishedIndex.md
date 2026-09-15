# ACT 文档状态总索引（未完成项）

> 生成：整理归档后统一记录"哪份文档还有哪些没做完"。**本索引是未完成项的唯一权威清单**；专题文档文内勾选若与本索引冲突，以本索引为准（并回来改文档）。
> 已基本实现完 / 已放弃的文档移入 [`已完成/`](已完成/)。其余文档留在 `Docs/` 根目录。
> 状态记号：✅ 已完成 · 🟡 部分完成 · ⬜ 未做 · 🚫 已放弃（表/代码保留但不走玩法）。

---

## 一、状态一览

| 文档 | 位置 | 状态 | 一句话 |
|---|---|---|---|
| `ActTimeEffectsBacklog.md` | 已完成/ | ✅ 完成 | 四根钟、断裂、HitStop、冻结全部落地；唯一遗留项随失衡屏蔽作废 |
| `ActSkillConfigAndLeveling.md` | 已完成/ | ✅ 基本完成 | 阶段 1–5 收口；养成 UI 后置，锁头飞弹明确不做 |
| `ActHarmonyBreakDesign.md` | 已完成/ | 🟡 核心完成 | 步骤 0–4 已接；14001 轴待换处决动画，后置项待实机 |
| `ActEnemyAiDesign.md` | Docs/ | 🟡 | 决策层 A–F+H 落地；屏外公平、Boss 相位、Peak/威胁表/远程、正式预警资产未做 |
| `ActBuffLearningBacklog.md` | Docs/ | 🟡 | 管线/护盾/控制完成；异常计量、紊乱、快照、切人转移未做 |
| `ActSquadDesign.md` | Docs/ | 🟡 | M4.0–4.2 落地；M4.3 延奏、M4.5 喧响未做；M4.4 连携已屏蔽 |
| `ActSkillKitConfig.md` | Docs/ | 🟡 | 阶段 0–4 落地；闪避反击、安比 13100、多档蓄力未做 |
| `ActLocomotionWuWaPlan.md` | Docs/ | ⬜ 未开工 | P0–P6 全部待做（步态机与动画接线） |
| `ActCameraStage2Design.md` | Docs/ | ⬜ 未排期 | 混合构图设计备忘，A–E 全未做 |
| `ActCharacterKitTemplate.md` | Docs/ | ⬜ 模板待用 | 角色选型已定；安比 13100 未做 |
| `ActCombatRoadmap.md` | Docs/ | 🟡 总表滞后 | 以本索引与各专题文档状态栏为准 |
| `ProjectOverview.md` / `ProjectConventions.md` | Docs/ | ✅ 常青 | 全景与硬约定，持续维护 |

---

## 二、已完成归档（`Docs/已完成/`）遗留备注

### 2.1 `ActTimeEffectsBacklog.md` — ✅ 全部落地
文内所有勾选项已接运行时。唯一未勾项「StaggerBreak 不会自动播」因 **ZZZ 失衡硬直已屏蔽**（`CombatMeterComponent.DazeGameplayEnabled=false`）而作废，不再追踪。

> ⚠️ 追加计划项（来源：`ProjectConventions.md` 专题表标注，本文档文内未写）：**「P3 时缓场（范围 / 遭遇，未接）」**——范围性时间减速场 / 遭遇时缓，未落地且无独立方案文档，属新提出的未做项；要做时先补一份方案再动代码。

### 2.2 `ActSkillConfigAndLeveling.md` — ✅ 阶段 1–5 完成
- 段表复合主键、`RatioByLevel`、`HitReaction`、`InterruptLevel`、升级组、近战多窗收口全部落地。
- 遗留（已拍板不追）：
  - 阶段 6 **养成 UI** —— 后置（"养成未来再说"），升级验证仍用 F9；
  - **锁头飞弹** —— 明确不做（弹道类需求另立方案时再议）。

### 2.3 `ActHarmonyBreakDesign.md` — 🟡 核心完成，两条尾巴
- 步骤 0（屏蔽失衡）/ 1（Ready 相位）/ 2（F + 真空锁零）/ 3（连招记忆）/ 4（处决包 + 构图镜头 + HUD）**均已接运行时**。
- 未完成：
  1. ⬜ **14001 轴仍是占位**，需 Flux 换正式处决动画；
  2. ⬜ 后置项（实机对照后再定）：杂兵 Auto 执行（`ExecuteMode`）、谐度破坏独立增幅公式、偏移/干涉。

---

## 三、未完成项明细（按文档逐项）

### 3.1 `ActEnemyAiDesign.md`（敌人 AI）

| 项 | 文档出处 | 状态 | 说明 / 依赖 |
|---|---|---|---|
| G 屏外公平 | §十二 12.1 | ⬜ 暂缓 | 屏外仍只降权可出手；方案=硬禁或 `OffscreenDelay` + 屏外持牌方向指示；画幅判定等相机 Stage 2 |
| Telegraph 正式资产 | §十二 12.1 P0 | ⬜ | 现为占位四角星；特效/提示音订阅 `CombatTelegraph.Shown` 即可 |
| 12001/12003 轴上预警 | §十二 12.1 P0 | ⬜ | 平劈仍 Brain 兜底 0.45s；挂轴或改 `TelegraphKind.None` |
| K 黄闪招架 | §十二 12.1 P1 | 🟡 | **成交链路已通**（`CombatParry` + 极限支援换人版）；未收口：演出、时长、招架贡献喂偏谐 |
| J Boss 相位 | §十二 12.1 P2 | ⬜ | `PhaseMask` 已预留、`PhaseIndex` 写死 0；HP 阈值 + 过渡轴 + 换招池 |
| Peak 压力态 | §十二 12.1 P3 | ⬜ | 枚举在，无进入条件 |
| 威胁表 / Return / Ranged | §十二 12.1 P3 | ⬜ | 多焦点、leash、远程牌（`TokenKind.Ranged` 占位） |
| 敌人正式动画 | §十二 12.2 | ⬜ | 仍借用玩家剪辑，动作与位移未对位 |
| 警戒正式表现（感叹号/音效） | §十二 12.1 P3 | ⬜ | 占位暖橙十字已有 |

文档状态栏滞后项：文内 I（失衡条）已被偏谐方案替代（见 `已完成/ActHarmonyBreakDesign.md`），K 见上表实际进度。

### 3.2 `ActBuffLearningBacklog.md`（Buff / 计量）

| 项 | 文档出处 | 状态 | 说明 |
|---|---|---|---|
| C 伤害队列 + `DamageSource` 细分 | 阶段表 C | ⬜ | 来源只有 `Skill\|Buff`；点燃/紊乱独立飘字与加成隔离未做 |
| D `TryConsume` + `ModifyExistingBuff` | 阶段表 D | ⬜ | 引爆/吃层 API 无 |
| E 异常计量 `CombatMeterComponent` 多元素槽 | 阶段表 E | ⬜ | 现组件只接了偏谐/失衡槽；异常积蓄仍计划复用同组件 |
| G Snapshot 快照 + 刷新/归属 | 阶段表 G | ⬜ | `ReApply` 不更新 Caster；DoT/异常归属未锁 |
| §1 改层数 / 改写 Id 后再 RequestAddStatus | §1 | ⬜ | |
| §2 TriggerFormula 滤 Reason | §2 | ⬜ | |
| §8/§9 切人 `OnSwitchOut` 挂钩 + `TransferByTag(Buff.Outro)` | §8–9 | ⬜ | = SquadDesign M4.3 延奏 |
| 护盾 UI 表现（`ShieldAbsorbed`/`HpDamageApplied` 飘字） | §6 | ⬜ | 吸收逻辑已落 Vital，表现未接 |
| §10 硬控互斥 | §10 | ✅ **实际已完成** | `HardControlMutex` 已接（高 Priority 覆盖，冻结高于眩晕）；**文档勾选滞后，需回改** |

### 3.3 `ActSquadDesign.md`（小队换人）

| 项 | 出处 | 状态 | 说明 |
|---|---|---|---|
| M4.0 三槽 + 常规换人 + 合轴 | §十一 | ✅ | 已落地 |
| M4.1 快速支援 | §十一 | ✅ 运行时已接 | 现役 Quick 列为 0 → 窗内 Z 失败是预期；填列后打入场技 |
| M4.2 黄/红支援 + 支援点 | §十一 | ✅ 运行时已接 | 现役 Evasive 列为 0 → 红闪 Z 失败是预期 |
| M4.3 延奏 | §十一 | ⬜ | `OnSwitchOut` 挂钩 + `Buff.Outro` 转移 + 候场跳伤暂停 |
| M4.4 连携轮转 | §十一 | 🚫 已屏蔽 | 随失衡连携一起关闭；`Kit.ChainSkillId`/13001 保留不用 |
| M4.5 喧响（团队大招条） | §十一 | ⬜ | 小队计量 + Ultimate 槽消耗整条；已拍板"满条一次性" |

### 3.4 `ActSkillKitConfig.md`（招式包 / 键位）

| 项 | 出处 | 状态 | 说明 |
|---|---|---|---|
| 阶段 0–4（表 + 4 键 + Kit/Z 窗 + 支援点 + 被动 + Empowered 预检） | §八 | ✅ | 已接运行时 |
| 阶段 5 闪避反击 | §八 | ⬜ | 行匹配已接；**极限闪避 Tag 授予 + Attack 高 Priority 表行未做** |
| 阶段 6 延奏 / 喧响与 Kit 被动交叉 | §八 | ⬜ | 依赖 SquadDesign M4.3/M4.5 |
| 阶段 7 安比 13100 | §八 | ⬜ | 只改表 + 轴（见 `ActCharacterKitTemplate.md`） |
| 阶段 8 形态 / 敌人 MoveSet | §八 | ⬜ | 后置；`FormId` 行已参与匹配 |
| §十 多档蓄力 / 松手结算 | §十 | ⬜ 仅方案 | **未落地**；需实机对照清单 8 项后再加 `MinHoldSeconds`/`AutoFireAtMax` 列 |
| §九 待拍板 ×3 | §九 | ⬜ | 终结技与连携同升级组、强化普攻落点、蓄力实机结论 |

### 3.5 `ActLocomotionWuWaPlan.md`（走跑 / 步态机）

整体 ⬜ 未开工。逐阶段：P0 八向树+疾跑片+交轴接跑 → P1 跳跃分段片 → P2 疾跑急停（`SprintStop_L/R`，最高性价比）→ P3 疾跑起步 → P4 Pivot 收口 → P5 锁定侧移 → P6 站立转身（可选）。最低资源 = 3 条新动画片。注意文内 §1.2 列出的 5 个既有坑（慢跑/快跑同播 `Running`、八向树无人读、Jump 状态名错、交轴固定回 Idle 等）是开工前必读。

### 3.6 `ActCameraStage2Design.md`（相机混合构图）

⬜ 未排期。A 权重混合 → B AutoFraming → C 混合曲线 → D 演出镜头 → E 调参面板，全未做。做之前重读文内六条硬约定。

### 3.7 `ActCharacterKitTemplate.md`（角色选型与模板）

- 选型已定：安比（验证）→ 漂泊者（正式）→ 露西亚（第二角色）→ 刻晴（位移压测）。
- ⬜ 安比 13100 未做（= SkillKitConfig 阶段 7）；待拍板 4 项（§七）未收口。

### 3.8 `ActCombatRoadmap.md`（总路线图）

文档自声明"总表有滞后"，**以本索引为准**。其内仍有效的未做项：二 异常/紊乱、五 Boss 相位与屏外公平、六 表现补全（Telegraph 正式资产 / 敌人动画 / 喧响 EP / 受击分级表现）、七·2 快照、七·3 Peak/威胁表/远程。

### 3.9 非 Docs 文档

| 文档 | 状态 |
|---|---|
| `Assets/Scripts/EGamePlay/Combat/Buff/BuffModify/EffectModifyParamSlots.md` | ✅ 参考文档，与代码同源，随 `EffectModifyType` 同步维护 |
| `.cursor/rules/*.mdc` | ✅ 规则文档，常青 |
| `README.md` / `AGENTS.md` | ✅ 常青（AGENTS 入口已同步本索引） |

---

## 四、已放弃清单（汇总，避免后人再提）

1. **ZZZ 失衡硬直 + 连携轮转**——`DazeGameplayEnabled=false` 屏蔽；表/组件保留不删（`ActHarmonyBreakDesign.md` §十一）。
2. **养成 UI**——后置（"养成未来再说"）；升级验证用 F9。
3. **锁头飞弹**——明确不做。
4. **鸣潮收缩金圈弹刀**——拍板不做（学绝区零黄红闪）。
5. **场上 L 招架入口**——招架改为候场 Z（`ActSkillKitConfig.md`）。
6. **Behavior Designer / GOAP / ML** 框架——自研决策层（`ActEnemyAiDesign.md` §十三）。
7. **NavMesh / 开世界选点 / RVO**——P3 后置，封闭场地不阻塞。
8. **双人同屏 / 候场 AI 助攻 / 阵营配队被动**——不做。
9. **5s 换人 CD**——弃用，改 ZZZ 式无 CD + `Exiting` 闸（`ActSquadDesign.md`）。
10. **多档蓄力**——未落地，仅方案（`ActSkillKitConfig.md` §十），实机对照后再议。

---

## 五、文档与代码不一致（已核实，待回改文档）

| 文档 | 文内状态 | 实际代码 |
|---|---|---|
| `ActBuffLearningBacklog.md` §10 | 硬控互斥"未做" | ✅ `EGamePlay/Combat/Action/HardControlMutex.cs` 已接（高 Priority 覆盖） |
| `ActEnemyAiDesign.md` §十二 K | 黄闪招架"未做" | 🟡 `CombatParry` + 极限支援成交已通，剩演出/时长 |
| `ActEnemyAiDesign.md` §十二 I | 计量条"未做" | 🚫 已被偏谐方案替代（`DazeGameplayEnabled=false`） |

---

## 六、建议下一刀（按当前缺口）

1. **安比 13100**（SkillKit 阶段 7 + CharacterKitTemplate）——新角色流水线验证，改动最小、最能暴露表/轴管线的坑；
2. **延奏 M4.3**（SquadDesign + Buff §8–9）——换人循环的最后一块，量小；
3. **走跑 P0**（LocomotionWuWaPlan）——不买新片即可让"走路有八向、疾跑换片、出招接跑"成立；
4. **异常/紊乱**（Buff C/D/E/G + Roadmap §二）——系统量最大，建议排在上述小件之后。

---

## 七、回看

整理文档后：先扫本索引"状态一览"，再进对应专题文档。任何一项落地 / 放弃后，回改两份：对应专题文档勾选 + 本索引状态列。
