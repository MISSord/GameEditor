# ACT 走跑方案（对照鸣潮子集）

> 状态：**方案文档，未开工**。按 §四 阶段逐项落地，一次只开一阶段。
> 走跑 / 移动动画对照**鸣潮**（缩小版 ALS：循环 + 起步/急停/Pivot + 锁敌侧移）。战斗循环（黄红闪、小队支援、Perfect Dodge）仍对照**绝区零**；敌人高潮改偏谐，见 `ActHarmonyBreakDesign.md`。失衡连携已屏蔽。
> 关联代码：`LocomotionMotor`、`LocomotionTuning` / `PlayerMoveSettingSo`、`CombatLocomotionInstaller`、`MotionDirector`、`RootMotionDriver`、`CombatAnimDirector`、`CombatStateDirector`。
> 硬约定沿用 `ProjectConventions.md`：四根钟（走跑/动画走玩家层 × 实体钟）、`CombatStateDirector` 唯一写 `CurState`、技能唯一出招口、水平位移三通道互斥。

---

## 〇、结论与边界

**电机已经按鸣潮写了**（三档速度、Shift 锁存疾跑、松开粘性、闪避接疾跑、锁敌朝向、快跑 Pivot、跳跃土狼/缓冲/落地顿）。还差的是把「循环片」补成带过渡的步态机，并把工程里已有的片接上。

| 学 | 不学 |
|---|---|
| 鸣潮：走 / 慢跑 / 快跑、锁存疾跑、急停、正向起步、未锁定 Pivot、锁敌侧移、站立转身 | 终末地：多向起步全目录、多高度落地、自动上台阶、飘带趋势库 |
| 绝区零：技能取消、切人合轴、黄红闪、失衡（其它文档） | 绝区零城市：每人几十个走跑、城/战两套片、楼梯战斗贴地 IK |
| | 鸣潮战斗：收缩金圈弹刀（路线图已否决） |

个人项目在 **P5 做完** 就会明显像鸣潮跑图。P6 是站立转身。再往后不要做。

位移权威不变：平时胶囊体 + SmoothDamp；**只有起步后半、急停、Pivot 才借用根运动**（与现有 Pivot 同一套 `RootMotionDriver.SetLocomotionOwnsMotion`）。技能仍走 Token，和走跑互斥。闪避仍是技能轴（Roll）+ `ArmSprintFromDodge`，不要改回走跑状态机。

目标步态：

```
Idle ──起步──► 循环(走/慢跑/快跑) ──急停──► Idle
                  │         ▲
                  │         └── 反向 Pivot（快跑、未锁定，已有）
                  └── 跳起 / 落地
锁定：身体朝敌人，脚走相对方向（侧移）；不进 Pivot
```

---

## 一、现状

### 1.1 已有（不要重做）

| 层 | 现状 |
|---|---|
| 意图 | 镜头相对摇杆；Ctrl 走/慢跑；Shift 锁存快跑；松开 0.15s 粘性；锁敌时身体朝目标、位移仍绕圈 |
| 逻辑位移 | `CharacterController` + SmoothDamp；`MotionDirector` 三选一（Locomotion / RootMotion / SkillCurve）；跳跃土狼、缓冲、落地顿 |
| 动画 | Idle / `Walk_Eqip_Front` / `Running` 短 CrossFade；快跑急转 `SprintPivot_L/R`（R 为 Mirror），窗口借根运动 |
| 脚步 | 已采样 `FootCyclePhase` / `StanceFoot`，供急转选片 |
| 战斗覆盖 | 技能 Token 独占 Animator；闪避结束仍按方向则接疾跑 |

代码注释已点名「鸣潮式停步滞回」「闪避锁存快跑」。`PlayerMoveSettingSo` 按走 / 慢跑 / 快跑、Pivot 角约 135°、commit 点调参。

### 1.2 必须先认清的坑

这些会让后面加片也看不出效果：

1. **慢跑和快跑都在播 `Running`。** 速度变了，片子没变，疾跑一定滑。工程里已有 Mixamo `Assets/Res/Anim/Common/FastRun.fbx`，Controller 未用。
2. **走路有完整八向 `Walk_Eqip_*`，电机却永远 `CrossFade(Walk_Eqip_Front)`。** `MoveX/MoveY` 每帧在写，没有 2D 混合树在读。
3. **跳跃去播名为 `Jump` 的状态，Controller 里没有这个名字。** 真正有的是 `Jump_Stand_Start/Loop/End`、`Jump_Running_Start/Loop/End`。
4. **技能结束 `CombatAnimDirector.ReturnToLocomotion` 固定切 Idle**，即使摇杆还按着。
5. **Pivot 逻辑与 `_L/_R` 状态已有**，优先打磨，不要重做。

### 1.3 `CurMoveState` 不必膨胀

电机内部建议用细步态枚举（只给走跑用，不必塞进 `CombatStateDirector`）：

`Idle / Walk / Jog / Sprint / Start / Stop / Pivot / JumpStart / JumpLoop / JumpLand`

对外 `MoveTypeEnum` 仍用 Idle / Walk / Run / Jump / Pivot / Falling。Start / Stop 对外还是 `Moving`。

---

## 二、落地约定

1. 电机继续用代码 `CrossFadeInFixedTime` 切状态（与现在一致）。**不要**靠 Animator 箭头条件赶手感。
2. 人在 Unity 里加好**同名状态**；`Animator.HasState` 找不到则静默回退到现有循环片。不要手改巨大 Controller YAML。
3. Stop / Start / Pivot 只 `SetLocomotionOwnsMotion`，**不要** `SetPolicy(RootMotion)`，否则和技能抢通道。
4. 一次只开一阶段。P2 之前不要做 P5（锁定滑步会掩盖急停是否成功）。
5. 热路径：无 LINQ、无 `GetComponent`、Animator 属性用缓存 hash。

---

## 三、阶段总览

| 阶段 | 内容 | 新片 | 状态 |
|---|---|---|---|
| P0 | 接线：走八向树、疾跑换片、交轴不回 Idle | 无（用库存） | [ ] |
| P1 | 跳跃接到现有分段片 | 无 | [ ] |
| P2 | 快跑急停 | `SprintStop_L`（R 可 Mirror） | [ ] |
| P3 | 快跑起步 | `SprintStart` | [ ] |
| P4 | Pivot 打磨 | 无 | [ ] |
| P5 | 锁定侧移；锁定疾跑降档 | 建议慢跑四向；可先降档 | [ ] |
| P6 | 站立转身（可选） | `TurnInPlace_L/R` | [ ] |

P5 之后停。不要接着做：八向起步、走/慢跑急停全套、Stride / Orientation Warp、脚 IK、多高度落地、自动上台阶。

---

## 四、分阶段方案

### P0 — 接线（不买新片）

**代码**

- `LocomotionMotor.TryCrossFadeGait`：走 → 状态 `Walk`（没有则回退 `Walk_Eqip_Front`）；慢跑 → `Running`；快跑 → `Sprint`（没有则回退 `Running`）。
- `CombatAnimDirector.ReturnToLocomotion`：有移动意图则切对应循环，不要固定 Idle。交轴后 Token 已清，立刻允许电机写参数。
- `MoveX/MoveY` 做短平滑（约 0.08s），避免锁敌时方向抖。
- 切循环时用已有 `FootCyclePhase` 对齐 `normalizedTime`。

**Animator（人改，【待改】）**

- 新建状态 `Walk`：2D BlendTree，参数 `MoveX` / `MoveY`，塞现有八向 `Walk_Eqip_*`。相位尽量对齐（左脚着地约 0.0，右脚约 0.5）。
- 新建状态 `Sprint`，Motion 用已有 `FastRun`。循环、原地（XZ Bake Into Pose），速度感靠电机。
- `Running` 继续当慢跑循环。

**验收**

- Ctrl 走会侧移/后退，不再只有正向走。
- Shift 疾跑和慢跑姿势能分开。
- 出招结束仍按着 W，直接进跑，不闪 Idle。

**主要文件：** `LocomotionMotor`、`CombatAnimDirector.ReturnToLocomotion`。不要动技能轴、配表。

---

### P1 — 跳跃接到现有分段片

电机跳跃（土狼、缓冲、落地顿）已有，只缺动画。仍**不占技能 Token**。不做空战连段。

**代码**

- 起跳：有水平速度播 `Jump_Running_Start`，否则 `Jump_Stand_Start`；没有这些名字再试 `Jump`。
- 空中：`Jump_*_Loop`。
- 落地：`Jump_Running_End` / `Jump_Stand_End_01`（没有 End 就 CrossFade 回循环/Idle）。
- 落地后保持现有 `LandSlow`。

**Animator：** 状态名与上面一致。片已在 `man_test` 里，确认 Loop 开了 loop。

**验收：** 站跳 / 跑跳姿势不同；落地有一顿再加速。

**主要文件：** `CombatAnimDirector.TryPlayLocomotionJump` + motor 空中/落地回调。

---

### P2 — 快跑急停（新片，性价比最高）

松手后现在只靠 SmoothDamp 减速 + 循环跑，会「脚还在跑、人已经停」。

**逻辑（照抄 Pivot 窗口）**

- 条件：快跑锁存中、未锁定、着地、松开摇杆、水平速度 ≥ 急停阈值（可复用或拆出 `StopMinSpeed`，默认接近 `PivotMinSpeed`）。
- 进入 Stop：按 `StanceFoot` 选 `SprintStop_L` / `_R`；借用根运动（位移+Yaw）；代码减速关掉，速度跟 `DeltaPosition`。
- 片播完或根位移耗尽 → Idle，并清 `_sprintArmed`。
- **可打断：** 窗口内又推杆 → 立刻 Abort，进循环或起步（WASD 连点不能卡死）。
- 技能 / 跳 / 闪避 → Abort，与 Pivot 一样。
- 慢跑、走路：P2 **不要**急停片，继续 SmoothDamp。

没有 Distance Matching 插件时，v1 用根运动驱动急停即可；不要第一版按剩余距离 scrub。

**调参（进 `PlayerMoveSettingSo`）**

- `StopMinSpeed`
- `StopAnimCrossFade`（约 0.06，与 Pivot 同级）

时长跟片走，不要写死 0.36s。

**Animator：** `SprintStop_L`、`SprintStop_R`。Root **不要** Bake Into Pose。右脚可用 Mirror。

**验收：** 疾跑松手有刹车姿势，停点干净；松手立刻再推，能马上跑起来。

**主要文件：** `LocomotionMotor`（照 `TickSprintPivot` 加 Stop）、`PlayerMoveSettingSo`。不要改 `MotionPolicy` 互斥规则。

---

### P3 — 快跑起步

现在一推杆就进循环，没有「迈第一步」。

**逻辑**

- 静止或速度很低 + 进入快跑（Shift 或闪避锁存）→ `SprintStart`。
- 前约 30% 可松手回 Idle；过了 commit 必须接到 `Sprint` 循环，并用脚步相位对齐。
- 位移：前半仍用胶囊加速（手感要跟手），后半可轻微借根运动。**不要全程根运动**，否则摇杆会钝。
- 走路 / 慢跑起步 P3 不做。
- 闪避结束且 `ArmSprintFromDodge`：若已有速度，**跳过起步**直接循环。

`MinimumStepTime`（现约 0.08s）只保证最短迈步，不能当起步片长。起步片大约 0.2～0.4s。

**Animator：** `SprintStart` 一条正向即可。原地或带一点 Root 都行，和电机约定一种。

**验收：** 从停到疾跑有蹬地；闪避接跑仍然干脆。

---

### P4 — Pivot 打磨（已有，只收口）

已有条件：快跑、未锁定、夹角 ≥ 约 135°、速度够。

- 片上确认 Root XZ + Yaw 都在（电机注释已要求）。
- Abort / 结束把 `_currentSpeed` 从根运动接回 SmoothDamp，避免结束瞬间速度掉光再加速。
- 锁定、走路、空中不进 Pivot（已是这样，回归测一下）。

**片：不新做。** 缺左右脚时继续 Mirror。

---

### P5 — 锁定侧移（慢跑八向；疾跑降档）

逻辑已经「身体朝目标、位移相机相对」。缺的是快跑只有正向 `Running/Sprint`，锁敌绕圈会横着滑。没有 Orientation Warp 时不要硬播正向疾跑。

**推荐策略**

- 锁定 + 走：用 P0 的 `Walk` 八向。
- 锁定 + 慢跑：同一个八向树，或另做 `Jog` 四向（前/后/左/右）。
- **锁定 + 疾跑：不要播正向 Sprint。** 保持锁存速度可以，动画改播八向/四向慢跑（playrate 上限约 1.15）。否则一定滑。
- 若只有走路八向、没有侧跑：先锁敌强制走/慢跑八向，疾跑键在锁定时不升档。
- 锁定继续禁止 Pivot（已有）。

已有 `Assets/Res/Anim/Using/run_strafe_front.anim` 可当慢跑前向，不要和 `FastRun` 混成同一个循环。

**验收：** 锁定绕圈是侧步/后退；解锁恢复面朝速度 + 疾跑正向。

**主要文件：** `LocomotionMotor.ApplyLocomotionAnim` 的 gait 选择。不要改 `LockSystem` 协议。

---

### P6 — 站立转身（可选）

`PlayerMoveSettingSo.m_StationaryTurnSpeed` 已写未用。

- 水平朝向误差 > 约 70°、无移动意图、着地、无技能 → `TurnInPlace_L/R`。
- 用 StationaryTurnSpeed 转胶囊；片 inplace。
- 推杆立刻打断进起步/循环。
- 180° 可以后做。

**验收：** 站着甩镜头，角色有转身，不会脚碾地瞬转。

---

## 五、动画清单

循环片：inplace，XZ Bake Into Pose，左右脚接触点对齐。起步 / 急停 / Pivot：**保留 Root XZ**；急转再保留 Yaw。左右对称优先 Mirror，先做左脚。武器手与现有 `Walk_Eqip` 一样持械，不要混 Unequip / Crouch。

### 5.1 已有，P0–P1 接线

| 状态名 | 用途 | 来源 |
|---|---|---|
| `Idle` | 待机 | 已有 |
| `Walk_Eqip_*` 八向 | 走进 2D 树 | 已有，现被电机单播正向 |
| `Running` | 慢跑循环 | 已有 |
| `FastRun` | 快跑循环 | `Assets/Res/Anim/Common/FastRun.fbx`，未进状态 |
| `SprintPivot_L` / `_R` | 快跑急转 | 已有，R 为 Mirror |
| `Jump_Stand_Start/Loop` + `Jump_Stand_End_01` | 原地跳 | 已有 |
| `Jump_Running_Start/Loop/End` | 移动中跳 | 已有 |

`Walk`、`Sprint` 是**新状态名**，Motion 用旧片。

### 5.2 要新做

| 优先级 | 建议状态名 | 条数 | 内容 | 根运动 | 阶段 |
|---|---|---|---|---|---|
| 必做 | `SprintStop_L` | 1 | 疾跑急停，左脚支撑落地收势 | XZ 开 | P2 |
| 必做 | `SprintStop_R` | 0 或 1 | 可 Mirror L | 同上 | P2 |
| 必做 | `SprintStart` | 1 | 从停到疾跑，只做**正向** | 可选少量 XZ | P3 |
| 建议 | `TurnInPlace_L` / `_R` | 1+Mirror | 原地约 90° | 关，只转胶囊 | P6 |
| 建议 | `Jog_F/B/L/R` | 4 | 锁定用慢跑四向；对角可混 | 关 | P5 |
| 可后置 | `JogStart` | 1 | 正向慢跑起步 | 可选 | 停更稳之后 |
| 可后置 | `JogStop_L` | 1+Mirror | 慢跑急停 | XZ 开 | 通常不需要 |
| 不做 | 走八向起步/急停 | 16+ | — | — | 终末地级 |
| 不做 | 疾跑八向 | 8 | 锁定疾跑改降档 | — | 无 Warp 别做 |
| 不做 | Lean 叠加、楼梯 IK | — | — | — | — |

**最低可买/可做包：3 条有效片** = `SprintStop_L` + Mirror 成 R + `SprintStart`。P0 / P1 / P4 全用库存。

商店关键词（工程已是 Mixamo 风格）：`Sprint Stop`、`Run Start`、`Strafe Run` 四向。导入后：循环片 loop + Bake XZ；Stop / Start 关掉 Bake XZ；统一到当前骨骼 Avatar。

---

## 六、代码改动范围

| 阶段 | 主要文件 | 不要动 |
|---|---|---|
| P0 | `LocomotionMotor.TryCrossFadeGait`、`CombatAnimDirector.ReturnToLocomotion` | 技能轴、Luban |
| P1 | `TryPlayLocomotionJump` + motor 空中/落地回调 | 技能 Token |
| P2–P3 | motor 照 `TickSprintPivot` 加 Stop/Start；`PlayerMoveSettingSo` 加字段 | `MotionPolicy` 互斥 |
| P4 | Pivot 结束速度衔接 | 重写急转条件 |
| P5 | `ApplyLocomotionAnim` 锁定时 gait | `LockSystem` 协议 |
| P6 | Idle 朝向误差 → Turn；接上 `m_StationaryTurnSpeed` | 相机逻辑 |

闪避保持：`ActSpellSession` 对 Roll `ArmSprintFromDodge`。技能位移继续轴上 RootMotion / 曲线。

Animator 待办用人在编辑器里做。需要时用下面格式（不要手改 Controller YAML）：

```
【Animator 待改】
- Controller: Assets/Res/Animator/man_test.controller（以角色实际用的为准）
- 状态（名称必须能被 HasState 找到）:
  1.
- 不要改: 技能轴 SkillData Scriptable、走跑电机互斥 Policy
```

---

## 七、验收顺序

1. 走八向 + 疾跑换片 + 出招接跑不闪 Idle  
2. 站跳 / 跑跳分段  
3. 疾跑松手有刹车，连点 WASD 不卡  
4. 从停到疾跑有蹬地；闪避接跑仍直接循环  
5. 锁定绕圈是侧步；解锁急转仍是 Pivot  
6. 站着甩镜头有转身  

---

## 八、明确不要做

1. 用全局 `Time.timeScale` 或第五根钟驱动走跑。
2. 技能占轴时走跑抢水平通道（`MotionDirector` 互斥）。
3. 把闪避做成 locomotion 状态。
4. 为丝滑去抄终末地过渡片目录或绝区零城市双套走跑。
5. 无 Warp 时锁定仍播正向 `Sprint`。
6. 手改 `SkillDataScriptable` 或巨大 Animator YAML 当日常入口。
