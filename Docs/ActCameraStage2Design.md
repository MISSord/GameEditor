# ACT 相机意图 P2：混合构图（设计备忘）

> 状态：**阶段 A / B / F 运行时已落地**；C–E、G 未做。现役无 Ultimate 轴，构图验收走 F7，第一条大招再挂 Msg。
> 关联代码：`CameraManager`（意图栈 + 权重仲裁 + 唯一落地口）、`CameraIntent.cs`、`CameraState.cs`、`LockSystem.cs`、`CameraShakeController.cs`。
> 前置约定：Stage 0（距离意图）与 Stage 1（意图栈仲裁）已落地；震屏为基准之外的叠加层。

---

## 一、设计目的

Stage 1 把相机升级成"意图仲裁"（同源单槽 + 优先级接管 + 平滑归还），解决了"本帧由谁控制、改动多少"的**独占式所有权**。但它没解决鸣潮 / 绝区零战斗镜头里最重要的特征：**镜头从不真正静止，多个需求连续混合**。

P2 要解决三件事：

1. **部分所有权**：现在角度只有"最高优先级独占"——锁定是互斥状态、技能意图会完全压掉玩家视角。真实战斗镜头是"锁定 70% + 玩家输入 30%"这种连续混合。
2. **自动构图（AutoFraming）**：现在战斗中目标打高打低全凭玩家手动转视角，目标出屏就丢了。商业动作游戏在战斗状态下会持续把镜头往"玩家-目标连线"方向微微校正，把双方保持在画幅里。
3. **混合质感**：接管/归还目前只有单一 SmoothDamp；大招、切目标、进出战斗各需要自己的攻/释曲线。

大招「拍角色」是第 4 件事，不塞进 D 的 Cutscene 源，单独走 **F / G**（见 §八）。

---

## 二、现状对照（Stage 1 之后的真实状态）

| 现状 | 位置 | P2 要改成 |
|---|---|---|
| FreeLook / LockOn 互斥状态切换 | `CameraState.cs` | LockOn 降级为**永驻权重源**：锁定时 weight 0→1（包络），FreeLook 保留残差 |
| 角度仲裁 = 最高优先级独占 | `CameraManager.UpdateAngleArbitration` | 权重归一化混合，低优先源不再被完全忽略 |
| 无战斗构图 | — | 新增 AutoFraming 永驻源（只偏 yaw，不碰 pitch / 距离） |
| 接管只有 SmoothDamp 一种曲线 | `CameraManager.SmoothAngleTo` | 意图自带 BlendCurve（attack / release），切目标走最短弧 |
| Cutscene 源只占位 | `CameraIntentSource` | 接入：优先级最高 + 硬切 / 自定义曲线 |
| 大招只有时停、镜头冻死 LookPoint | `UltimateCinematic` / `CombatHarmonyBreak` | 时间轴驱动的镜头**配方**（每帧相对角色解算），见 F/G |

---

## 三、设计方向（硬约定，实现时必须守）

1. **基准 / 叠加分离继续成立**：状态机读回 `CleanLookRotation`（基准），混合、构图、震屏全部发生在基准之后的输出层——P2 只是把"输出层"从"独占覆盖"升级为"混合"。这是震屏漂移那次事故买来的教训，不许回退。
2. **唯一落地口不变**：`CameraManager.LateUpdate` 仍是最终 transform 唯一写入口。
3. **意图无状态**：每帧从活跃意图重算混合，不存"当前控制者"。新增的权重、曲线都只是意图的字段。
4. **四根钟不变**：混合与曲线计时走镜头层（`CameraDelta` / unscaled），暂停冻结；不新增第五根钟。
5. **零 GC**：混合在 LateUpdate 热路径，只用 struct + 固定小集合；曲线用闭式公式或预采样 LUT，不做每帧分配。
6. **所有权瞬间换手仍是禁忌**：所有源的开 / 关都必须走权重包络（硬切过场除外）。

---

## 四、大方向实现流程（分阶段，每段独立验收）

| 阶段 | 内容 | 验收标准 | 状态 |
|---|---|---|---|
| A | `CameraIntent` 加 `Weight`(0..1)；仲裁改为权重归一化混合（优先加权）；LockOn 改权重源，锁定 / 解锁走 0↔1 包络 | 锁定 / 解锁平滑过渡；自由视角手感与现在一致；低优先意图有残差贡献 | [x] |
| B | AutoFraming 源：锁定目标（无锁则最近敌人），yaw 朝"玩家→目标"方向偏转，权重随目标屏幕偏心距增长，只调 yaw | 战斗中目标始终在画幅内，镜头有轻微"牵引感"；跑图（无战斗）不受影响 | [x] |
| C | 混合曲线：意图加 `BlendCurve`（attack / release 各一条）；**锁定目标切换** yaw 走最短弧 | 大招接管与归还曲线可调；切锁不绕远路（不是换人） | [ ] |
| D | Cutscene 源接入（最高优先 + 硬切 / 自定义曲线）。**大招构图不走 Cutscene**，见 F/G | 真过场可硬切接管，结束平滑归还；被打断不残留 | [ ] |
| E | 调参面板（每个源的 Weight / 曲线可视调） | 策划可自行调出想要的手感 | [ ] |
| F | 大招构图 D0：镜头配方预设 + 轴 Msg `SkillCamera`；每帧相对角色解 LookPoint；片段结束 / 打断取消 | 时停中滑入英雄镜；轴结束与打断都还镜、不残留 | [x] |
| G | 大招构图 D1：一条轴多拍 + 配方字段（方位 / 距离 / Weight） | 起手英雄镜切出手过肩；锁定残差可按拍调节 | [ ] |

> A 是其余全部的地基；B 单独落地就能明显"像"鸣潮 / 绝区零战斗跟镜；C 是手感曲线；D 是少数真过场；E 收尾。**F/G 是大招拍角色**，不依赖 B/C/D，可穿插开工。C 落地后 F 的 SmoothDamp 换成攻/释曲线。

阶段 A 落地要点（对照代码，下一阶段不要回退）：

- `CurrentCameraState` 保持 `FreeLook`；`CleanLookRotation` 仍是轨道基准，混合 / 震屏只写 LateUpdate 输出。
- LockOn 是混合层永驻源：`LockWeight` SmoothDamp 0↔1；默认 `FreeLookPriority=0`、`LockOnPriority=1`（满权时约 33% / 67%）。
- 解锁时把混合结果 `RebaseOrbit` 回 FreeLook，避免镜头甩回锁定前的旧角度。
- 距离仍最高优先独占；`BlendCurve` 留给阶段 C（技能接管继续 SmoothDamp）。

阶段 B 落地要点：

- AutoFraming 也是混合层永驻源，**不**经 `SubmitIntent`、**不**写基准。
- 只投票 yaw（pitch 复用 FreeLook，距离不动）。权重 = 水平屏幕偏心距从 `DeadZone` 线性升到 `FullZone`。
- 目标：已锁用锁定目标（可出画仍拉）；无锁用相机前方最近敌人。身后 / 锥外敌人不入选，避免跑图甩镜。
- 默认 `AutoFramingPriority=0`（与 FreeLook 同档）：目标在画面中央时权重 0，等于没这个源。
- 高优先 SkillCamera / Cutscene 仍靠 `2^(priority-max)` 压过构图。

阶段 F 落地要点：

- 配方在 `CameraShotRecipe`（`HeroThreeQuarter` / `OverShoulder` / `CloseImpact`），`CameraIntent.HasRecipe` 时每帧解朝向，不冻 LookPoint。
- 轴 Msg `SkillCamera`，StrMsg=预设名，FloatMsg=BlendTime（0=默认）。priority **22**。片段 `OnFinish`（已 Trigger）按预设取消，避免后一段被前一段关掉；`ActSpellSession.Finish` 再兜底 `CancelSkillShot`。
- 只打当前跟随角色。F7 循环三套预设、1.5s 还镜（`Debug` 源 priority 30）。
- 现役 CharacterId=1 无 Ultimate 行，轴挂点见回包【技能编辑器待改】。谐度破坏仍是冻死 LookPoint、priority 28，不被 22 覆盖。

阶段 C 说明（未开工，两件独立的事）：

1. **BlendCurve（大头）**：技能 / 大招构图的接管、归还不再共用一条 SmoothDamp。攻曲线快砸进来，释曲线慢还回去。默认空 = 现在的 SmoothDamp，F 的 `SkillCamera` 可以直接换上。
2. **切锁最短弧（不是换人）**：指鸣潮 / 崩 3 那种「已锁定时换下一个敌人」，镜头 yaw 走劣弧 ≤180° 转到新目标。本项目锁定是 Tab / 中键。**换人已迁到 1/2/3 绝对槽**（`ActSquadDesign.md` §八）；Q/E 现只被 `LockOnState` 用来 `SwitchTargetPrev/Next`，不再和换人抢键。C 落地可沿用 Q/E，或再拆专用切锁键。
3. 跟踪层 `_lockLookYaw` 已是 `SmoothDampAngle`（本身最短弧）。若实机仍绕远，查候选列表环绕选到背后的人，或加权混合里 FreeLook 旧 yaw 把合成结果拽过 180°。切锁可另给一条更短的 `LockSwitchSmoothTime`，不要和「跟移动目标」共用 0.1s。

---

## 五、接口草图（A 阶段最小形态）

```csharp
// CameraIntent 增补（向后兼容：默认 Weight=1、无曲线 = 现在的行为）
public readonly float Weight;                 // 0..1 话语权
public readonly CameraBlendCurve AttackCurve; // null = SmoothDamp（现状）

// CameraManager 仲裁核心（伪代码）
// 1. 收集本帧活跃意图（含状态机提议：FreeLook weight=1，LockOn weight=lockWeight，AutoFraming weight=ecc）
// 2. 权重归一化 + 优先级加权：w' = w * 2^(priority - maxPriority)
// 3. 朝向 = 球面加权平均（yaw/pitch 各源）；距离 A 阶段可仍走最高优先独占，先只混角度
// 4. 输出层：混合结果 → 遮挡 → 震屏 → 落地（顺序不变）
```

---

## 六、明确不做（防 scope creep）

1. 不迁回 Cinemachine——自研仲裁就是项目决策；
2. 不做真实相机物理（惯性 / 弹跳 / 碰撞刚体模拟）；
3. 不做过场编辑器（Cutscene 数据用现有时间轴表达）；
4. FOV 不进混合（大招 FOV 冲击继续走 `CameraShakeController` 叠加层，避免双重体系）；
5. 不做多人分屏 / 多镜头；
6. 不用 Flux `FCameraManagerEvent` 切换场景 Camera 物体（破坏唯一落地口）；
7. 大招构图不改 `focusPoint`、不写进 `DamageAction` / 段表 / Luban；
8. v1 不做样条或相机动画片段。

---

## 七、回看

开工前：先扫上方状态列，再对照 `CameraManager` 现状（尤其 `UpdateAngleArbitration` 与 `CleanLookRotation` 的职责边界有没有被后来的改动破坏）。每阶段合入后更新本表勾选。

---

## 八、大招构图（阶段 F / G）

对照鸣潮：共鸣解放几乎都会**换构图拍角色**，但 90% 不是独立过场相机，而是同一套第三人称相机换机位——时停里转到 3/4 或侧面英雄镜、拉近，出手再切过肩战镜，结束还回锁定/自由。`UltimateCinematic` 只负责时停；构图按招写在技能轴上。

### 8.1 镜头语言（拆成可配的拍）

| 拍 | 看什么 | 机位 | 距离 |
|---|---|---|---|
| 起手英雄镜 | 角色脸 / 侧面 | 相对角色朝向偏 120°～150°，略仰 | 2.8～3.5m |
| 出手战镜 | 角色背 + 敌人 | 相对锁敌连线偏 20°～40° | 4～5m |
| 命中近景（可选） | 冲击 | 再近一点 | 2.4～3m + FOV 走震屏 |

角色差异主要是**轴上何时切拍**。今汐那种真过场是少数，走阶段 D 的 Cutscene 源，不走 F。

当前输出仍是 `位置 = focusPoint - 朝向 * Distance`。英雄镜只要把朝向转到「从斜前方看向角色」，不必换枢轴、不必第二台 Camera。冻死一个世界 LookPoint（谐度破坏现在那样）会拍到后脑勺，且角色一动镜头就漂。

### 8.2 配方，不要死坐标

意图存配方，LateUpdate 用镜头钟每帧解成 LookPoint + Distance，再进阶段 A 混合：

```csharp
enum CameraShotSpace : byte
{
    CasterFacing, // 英雄镜：相对角色朝向
    LockLine,     // 战镜：相对玩家→锁定目标
}

struct CameraShotRecipe
{
    public CameraShotSpace Space;
    public float Azimuth;    // 0=正后方，180=正脸
    public float Pitch;
    public float Distance;
    public float LookHeight;
    public float Weight;     // 起手 1；出手段可 0.7 留锁定残差
}
```

解算（示意）：`LookPoint = focus + up * LookHeight + (spaceYaw * Euler(Pitch, Azimuth, 0) * forward) * probe`。不写回 `CleanLookRotation`。

优先级：`FreeLook 0 < LockOn 1 < 大招构图 22 < 谐度破坏 28 < 真过场 40`。Gate 已禁止大招与 F 叠；构图优先差保证互压。

### 8.3 数据在时间轴，不在 Excel

构图跟动画帧对齐，和盒 / `TimeStop` Msg 同类。时停继续 `PlayFxPackage(UltimateCinematic)`，**不要把机位塞进表现包**（否则所有大招同一镜头）。

作者侧 v1 先 3 个预设，轴上只选名字 + 片段长度：

| 预设 | 含义 |
|---|---|
| `HeroThreeQuarter` | 起手 3/4 英雄镜 |
| `OverShoulder` | 出手过肩战镜 |
| `CloseImpact` | 命中近景 |

Msg：`MsgName=SkillCamera`，`StrMsg=HeroThreeQuarter`。片段起 `SubmitIntent`，片段止 / 轴 `OnStop` / `ActSpellSession` 结束按**同一 priority** `CancelIntent`。现有 Msg 几乎只在 `OnTrigger` 打一次，镜头必须补 Finish/Stop 取消；`Duration` 只当保险丝（轴走玩家钟，意图计时走镜头钟，不能只靠时长）。

### 8.4 阶段切分

**F（D0，可立刻做，不依赖 B/C/D）**

1. `PlayEventMsg.SkillCamera` + 三个预设，每帧按角色朝向 / 锁线解 LookPoint。
2. `Duration<=0` 表示「片段结束才还」；打断和会话结束必取消。
3. 挑一条现役大招轴挂：起手 `HeroThreeQuarter`，出手切 `OverShoulder`。
4. F7 同套预设验收：时停中滑入、结束还镜、打断不残留。

**G（D1）**

预设不够再把方位角 / 距离 / Weight 写进 StrMsg 或专用 `FSkillCameraEvent`。一条大招轴上多段切换。

曲线（快攻慢释）并入阶段 C，不另开阶段。真过场并入阶段 D。

### 8.5 与其它源的关系

- AutoFraming（B）：战斗跟镜微校正；大招权重高时自然压过。
- 锁定：出手段配方 Weight&lt;1 时仍可留残差。
- FOV 冲击：命中帧走 `CameraShakeController`，不进混合。
- 时停：世界停、发起者轴继续、**相机钟=1**，构图可以在冻结的世界里滑入。
