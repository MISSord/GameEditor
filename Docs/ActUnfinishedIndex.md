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
| `ActBuffLearningBacklog.md` | Docs/ | ✅ 对照完成 | 管线/护盾/控制/异常/Caster/Reason 已收口；延奏归小队 M4.3；ZZZ 积蓄/紊乱已丢掉 |
| `ActSquadDesign.md` | Docs/ | 🟡 | M4.0–4.2 落地；M4.3 延奏、M4.5 喧响未做；M4.4 连携已删除 |
| `ActSkillKitConfig.md` | Docs/ | 🟡 | 阶段 0–4 落地；闪避反击、安比 13100、多档蓄力未做 |
| `ActLocomotionWuWaPlan.md` | Docs/ | ⬜ 未开工 | P0–P6 全部待做（步态机与动画接线） |
| `ActCameraStage2Design.md` | Docs/ | 🟡 | 阶段 A/B/F 运行时已接；C 曲线 / D Cutscene / E 调参 / G 多拍字段未做 |
| `ActCharacterKitTemplate.md` | Docs/ | ⬜ 模板待用 | 角色选型已定；安比 13100 未做 |
| `ActCombatRoadmap.md` | Docs/ | 🟡 总表滞后 | 以本索引与各专题文档状态栏为准 |
| `ActVibrationDesign.md` | Docs/ | ✅ 第 0–4 步已接 | 鸣潮共振度：表 + 身份 + 段表扣条/Stagger + 招架/F 全局比例 + 头顶 HUD/`StaggerBreak` |
| `ProjectOverview.md` / `ProjectConventions.md` | Docs/ | ✅ 常青 | 全景与硬约定，持续维护 |

---

## 二、已完成归档（`Docs/已完成/`）遗留备注

### 2.1 `ActTimeEffectsBacklog.md` — ✅ 全部落地
文内时间层勾选项已接运行时。`StaggerBreak` 由削韧破韧自动播，见 `ActVibrationDesign.md` 第 4 步。

> ⚠️ 追加计划项（来源：`ProjectConventions.md` 专题表标注，本文档文内未写）：**「P3 时缓场（范围 / 遭遇，未接）」**——范围性时间减速场 / 遭遇时缓，未落地且无独立方案文档，属新提出的未做项；要做时先补一份方案再动代码。

### 2.2 `ActSkillConfigAndLeveling.md` — ✅ 阶段 1–5 完成
- 段表复合主键、`RatioByLevel`、`HitReaction`、`InterruptLevel`、升级组、近战多窗收口全部落地。
- 遗留（已拍板不追）：
  - 阶段 6 **养成 UI** —— 后置（"养成未来再说"），升级验证仍用 F9；
  - **锁头飞弹** —— 明确不做（弹道类需求另立方案时再议）。

### 2.3 `ActHarmonyBreakDesign.md` — 🟡 核心完成，两条尾巴
- 步骤 0（删除失衡运行时）/ 1（Ready 相位）/ 2（F + 真空锁零）/ 3（连招记忆）/ 4（处决包 + 构图镜头 + HUD）**均已接运行时**。
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

### 3.2 `ActBuffLearningBacklog.md`（Buff，对照鸣潮）

对照清单 **已收口**。延奏从本文挪到 `ActSquadDesign.md` §5.8。ZZZ 积蓄条、紊乱、完整 Snapshot、改写 Id、伤害队列在「明确不学」。

| 项 | 文档出处 | 状态 | 说明 |
|---|---|---|---|
| 配表异常 + 跳伤乘层 + `TryConsume` | 阶段 C | ✅ | `32001–32003`；不要积蓄条 |
| TriggerFormula 滤 `BuffRemoveReason` | Reason | ✅ | `#IsRemoveReasonDeath` 等；`32100` |
| ReApply 更新 Caster | 归属 | ✅ | 默认后上覆盖；`Buff.Caster.KeepFirst` |
| `#IsNormalOrUltimateSpell` | 已知配表坑 | ⬜ | Buff 31000 公式名不存在；改表或补方法，不是新玩法 |

### 3.3 `ActSquadDesign.md`（小队换人）

| 项 | 出处 | 状态 | 说明 |
|---|---|---|---|
| M4.0 三槽 + 常规换人 + 合轴 | §十一 | ✅ | 已落地 |
| M4.1 快速支援 | §十一 | ✅ 运行时已接 | 现役 Quick 列为 0 → 窗内 1/2/3 失败是预期；填列后打入场技 |
| M4.2 黄/红支援 | §十一 | ✅ 运行时已接 | 支援点已删。现役 Evasive 列为 0 → 红闪 1/2/3 失败是预期 |
| M4.3 延奏 | §五·8 / §十一 | ⬜ | 换人成功同一帧：先 `TransferByTag(Buff.Outro)` 再 `NotifySwitchOut`；候场 `BuffTime.Pause`；Death 不延奏 |
| M4.4 连携轮转 | §十一 | 🚫 已删除 | 失衡连携运行时已拿掉；`Kit.ChainSkillId`/13001 表保留不挂技能 |
| M4.5 喧响（团队大招条） | §十一 | ⬜ | 小队计量 + Ultimate 槽消耗整条；已拍板"满条一次性" |

### 3.4 `ActSkillKitConfig.md`（招式包 / 键位）

| 项 | 出处 | 状态 | 说明 |
|---|---|---|---|
| 阶段 0–4（表 + 4 键 + Kit 换人窗 + 被动 + Empowered 预检） | §八 | ✅ | 已接运行时。支援点已删 |
| 阶段 5 闪避反击 | §八 | ⬜ | 行匹配已接；**极限闪避 Tag 授予 + Attack 高 Priority 表行未做** |
| 阶段 6 延奏 / 喧响与 Kit 被动交叉 | §八 | ⬜ | 依赖 SquadDesign M4.3/M4.5 |
| 阶段 7 安比 13100 | §八 | ⬜ | 只改表 + 轴（见 `ActCharacterKitTemplate.md`） |
| 阶段 8 形态 / 敌人 MoveSet | §八 | ⬜ | 后置；`FormId` 行已参与匹配 |
| §十 多档蓄力 / 松手结算 | §十 | ⬜ 仅方案 | **未落地**；需实机对照清单 8 项后再加 `MinHoldSeconds`/`AutoFireAtMax` 列 |
| §九 待拍板 ×3 | §九 | ⬜ | 终结技与连携同升级组、强化普攻落点、蓄力实机结论 |

### 3.5 `ActLocomotionWuWaPlan.md`（走跑 / 步态机）

整体 ⬜ 未开工。逐阶段：P0 八向树+疾跑片+交轴接跑 → P1 跳跃分段片 → P2 疾跑急停（`SprintStop_L/R`，最高性价比）→ P3 疾跑起步 → P4 Pivot 收口 → P5 锁定侧移 → P6 站立转身（可选）。最低资源 = 3 条新动画片。注意文内 §1.2 列出的 5 个既有坑（慢跑/快跑同播 `Running`、八向树无人读、Jump 状态名错、交轴固定回 Idle 等）是开工前必读。

### 3.6 `ActCameraStage2Design.md`（相机混合构图）

🟡 阶段 A / B / F 运行时已接。C–E、G 未做。现役无 Ultimate 轴，构图先用 F7。

| 项 | 状态 | 说明 |
|---|---|---|
| A 权重混合 + LockOn 包络 | ✅ | `CameraIntent.Weight`；`w' = w * 2^(priority-max)` 球面混合；FreeLook 常驻基准，锁定 0↔1 包络 |
| B AutoFraming | ✅ | 只偏 yaw；权重随水平屏幕偏心距；无锁只收相机前方最近敌人 |
| C BlendCurve + 切锁最短弧 | ⬜ | 切目标=锁定循环，不是换人。换人已迁 1/2/3；Q/E 现只切锁 |
| D Cutscene 源 | ⬜ | 真过场；大招构图不走这里 |
| E 调参面板 | ⬜ | Inspector 已有 A/B 的优先级 / 包络 / 死区 |
| F 大招构图 D0（预设 + 轴 Msg） | ✅ | `PlayEventMsg.SkillCamera` + 三预设；打断/会话结束取消。轴待第一条大招挂 |
| G 大招构图 D1（多拍 + 字段） | ⬜ | 英雄镜切过肩的字段化；F 已支持两段 Msg 换预设 |

### 3.7 `ActCharacterKitTemplate.md`（角色选型与模板）

- 选型已定：安比（验证）→ 漂泊者（正式）→ 露西亚（第二角色）→ 刻晴（位移压测）。
- ⬜ 安比 13100 未做（= SkillKitConfig 阶段 7）；待拍板 4 项（§七）未收口。

### 3.8 `ActCombatRoadmap.md`（总路线图）

文档自声明"总表有滞后"，**以本索引为准**。其内仍有效的未做项：五 Boss 相位与屏外公平、六 表现补全（Telegraph 正式资产 / 敌人动画 / 喧响 EP / 受击分级表现）、七·3 Peak/威胁表/远程。§二 ZZZ 积蓄+紊乱与七·2 完整 Snapshot 已按 Buff 文档丢掉；鸣潮异常改走 Buff 叠层（见 3.2）。

### 3.9 `ActVibrationDesign.md`（削韧 / 共振度）

✅ 第 0–4 步已接。与偏谐并存，不要复用 `DazeSetting` 三档或 `DazeRatio`。

| 项 | 状态 | 说明 |
|---|---|---|
| 怪物表 `Monster`（主键 MonsterId） | ✅ | 学 `CharacterKit`；号段 101/201/301；不进玩家 Kit |
| `VibrationSetting`（主键 MonsterId） | ✅ | 一对一；缺行=无条，不回退 |
| `VibrationRule` Id=1 | ✅ | 招架 / F 削韧全局百分比，不跟角色、技能、怪物分行 |
| 段表 `VibrationRatio` | ✅ | 只服务普通命中；14001 填 0 |
| 运行时 `MonsterId` + Poise / 偏谐问怪物行 | ✅ | F2=101、F3=201、F4 杂兵 101 / 精英 201；删掉 `_isElite` 配偏谐 |
| 组件 + Stagger 瘫痪窗 | ✅ | 无易伤；世界钟黄条；Tag `State.VibrationBreak`；起身 `Combat.DazeRecover` |
| 招架 / F 全局砍条 | ✅ | `Max × 0.25` / `Max × 0.30`；缺 `VibrationRule` 不砍；F 多段只砍一次 |
| HUD / `StaggerBreak` 包 | ✅ | Intact 白条，Immobilized 黄条；破韧由 `ApplyVibrationBreak` 播包，不进 `DamageAction` |

### 3.10 非 Docs 文档

| 文档 | 状态 |
|---|---|
| `Assets/Scripts/EGamePlay/Combat/Buff/BuffModify/EffectModifyParamSlots.md` | ✅ 参考文档，与代码同源，随 `EffectModifyType` 同步维护 |
| `.cursor/rules/*.mdc` | ✅ 规则文档，常青 |
| `README.md` / `AGENTS.md` | ✅ 常青（AGENTS 入口已同步本索引） |

---

## 四、已放弃清单（汇总，避免后人再提）

1. **ZZZ 失衡硬直 + 连携轮转**——运行时已删除（`CombatChainSkill` / Punish / 满条 Stagger）；`DazeSetting` / `DazeRatio` / `Kit.ChainSkillId` 表列保留当偏谐数字或不用（`ActHarmonyBreakDesign.md` §十一）。
2. **养成 UI**——后置（"养成未来再说"）；升级验证用 F9。
3. **锁头飞弹**——明确不做。
4. **鸣潮收缩金圈弹刀**——拍板不做（学绝区零黄红闪）。
5. **场上 L 招架入口**——招架改为 1/2/3 换人键（`ActSquadDesign.md` §八）。
6. **Behavior Designer / GOAP / ML** 框架——自研决策层（`ActEnemyAiDesign.md` §十三）。
7. **NavMesh / 开世界选点 / RVO**——P3 后置，封闭场地不阻塞。
8. **双人同屏 / 候场 AI 助攻 / 阵营配队被动**——不做。
9. **5s 换人 CD**——弃用；小队切人无 CD（`ActSquadDesign.md`）。`PlayerManager` 里 5s 只挡调试切镜头。
10. **多档蓄力**——未落地，仅方案（`ActSkillKitConfig.md` §十），实机对照后再议。
11. **ZZZ 异常积蓄条 + 紊乱 + 完整 Snapshot / 改写 Id / 伤害队列**——Buff 对照改为鸣潮后丢掉（`ActBuffLearningBacklog.md`「明确不学」）。
12. **绝区零支援点（次数 + 回复）**——运行时已删；切人无限次。表列保留不读。
13. **瘫痪窗易伤 / 白条减伤**——削韧方案已拍板不做（`ActVibrationDesign.md` §一）。

---

## 五、文档与代码不一致（已核实，待回改文档）

| 文档 | 文内状态 | 实际代码 |
|---|---|---|
| `ActEnemyAiDesign.md` §十二 K | 黄闪招架"未做" | 🟡 `CombatParry` + 极限支援成交已通，剩演出/时长 |
| `ActEnemyAiDesign.md` §十二 I | 计量条"未做" | 🚫 已改偏谐；失衡运行时已删除 |

---

## 六、建议下一刀（按当前缺口）

1. **安比 13100**（SkillKit 阶段 7 + CharacterKitTemplate）——新角色流水线验证，改动最小、最能暴露表/轴管线的坑；
2. **延奏 M4.3**（`ActSquadDesign.md` §5.8）——换人循环的最后一块；Buff 对照清单已收口，不要再回 Buff 文档立项。
3. **走跑 P0**（LocomotionWuWaPlan）——不买新片即可让"走路有八向、疾跑换片、出招接跑"成立。

鸣潮异常配表 + `TryConsume` 已在 Buff 阶段 C 落地，不再排进下一刀。

---

## 七、回看

整理文档后：先扫本索引"状态一览"，再进对应专题文档。任何一项落地 / 放弃后，回改两份：对应专题文档勾选 + 本索引状态列。
