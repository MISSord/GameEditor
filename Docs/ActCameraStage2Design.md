# ACT 相机意图 P2：混合构图（设计备忘）

> 状态：**设计备忘，未排期**。未来有"战斗构图 / 演出镜头 / 镜头手感调优"需求时回来看。
> 关联代码：`CameraManager`（意图栈 + 仲裁 + 唯一落地口）、`CameraIntent.cs`、`CameraState.cs`、`LockSystem.cs`、`CameraShakeController.cs`。
> 前置约定：Stage 0（距离意图）与 Stage 1（意图栈仲裁）已落地；震屏为基准之外的叠加层。

---

## 一、设计目的

Stage 1 把相机升级成"意图仲裁"（同源单槽 + 优先级接管 + 平滑归还），解决了"本帧由谁控制、改动多少"的**独占式所有权**。但它没解决鸣潮 / 绝区零战斗镜头里最重要的特征：**镜头从不真正静止，多个需求连续混合**。

P2 要解决三件事：

1. **部分所有权**：现在角度只有"最高优先级独占"——锁定是互斥状态、技能意图会完全压掉玩家视角。真实战斗镜头是"锁定 70% + 玩家输入 30%"这种连续混合。
2. **自动构图（AutoFraming）**：现在战斗中目标打高打低全凭玩家手动转视角，目标出屏就丢了。商业动作游戏在战斗状态下会持续把镜头往"玩家-目标连线"方向微微校正，把双方保持在画幅里。
3. **混合质感**：接管/归还目前只有单一 SmoothDamp；大招、切目标、进出战斗各需要自己的攻/释曲线。

---

## 二、现状对照（Stage 1 之后的真实状态）

| 现状 | 位置 | P2 要改成 |
|---|---|---|
| FreeLook / LockOn 互斥状态切换 | `CameraState.cs` | LockOn 降级为**永驻权重源**：锁定时 weight 0→1（包络），FreeLook 保留残差 |
| 角度仲裁 = 最高优先级独占 | `CameraManager.UpdateAngleArbitration` | 权重归一化混合，低优先源不再被完全忽略 |
| 无战斗构图 | — | 新增 AutoFraming 永驻源（只偏 yaw，不碰 pitch / 距离） |
| 接管只有 SmoothDamp 一种曲线 | `CameraManager.SmoothAngleTo` | 意图自带 BlendCurve（attack / release），切目标走最短弧 |
| Cutscene 源只占位 | `CameraIntentSource` | 接入：优先级最高 + 硬切 / 自定义曲线 |

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
| A | `CameraIntent` 加 `Weight`(0..1)；仲裁改为权重归一化混合（优先加权）；LockOn 改权重源，锁定 / 解锁走 0↔1 包络 | 锁定 / 解锁平滑过渡；自由视角手感与现在一致；低优先意图有残差贡献 | [ ] |
| B | AutoFraming 源：锁定目标（无锁则最近敌人），yaw 朝"玩家→目标"方向偏转，权重随目标屏幕偏心距增长，只调 yaw | 战斗中目标始终在画幅内，镜头有轻微"牵引感"；跑图（无战斗）不受影响 | [ ] |
| C | 混合曲线：意图加 `BlendCurve`（attack / release 各一条）；切目标 yaw 走最短弧 Slerp | 大招接管与归还曲线可调；Q/E 切目标不绕远路 | [ ] |
| D | Cutscene 接入 + 战斗演出镜头（大招相机：SkillCamera 高优先 + 自定义曲线 + 可选距离目标） | 时间轴可接管镜头、结束平滑归还；被打断也不残留 | [ ] |
| E | 调参面板（每个源的 Weight / 曲线可视调） | 策划可自行调出想要的手感 | [ ] |

> A 是其余全部的地基；B 单独落地就能明显"像"鸣潮 / 绝区零；C / D 是演出向；E 收尾。

---

## 五、接口草图（A 阶段最小形态）

```csharp
// CameraIntent 增补（向后兼容：默认 Weight=1、无曲线 = 现在的行为）
public readonly float Weight;                 // 0..1 话语权
public readonly CameraBlendCurve AttackCurve; // null = SmoothDamp（现状）

// CameraManager 仲裁核心（伪代码）
// 1. 收集本帧活跃意图（含状态机提议：FreeLook weight=1，LockOn weight=lockWeight）
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
5. 不做多人分屏 / 多镜头。

---

## 七、回看

开工前：先扫上方状态列，再对照 `CameraManager` 现状（尤其 `UpdateAngleArbitration` 与 `CleanLookRotation` 的职责边界有没有被后来的改动破坏）。每阶段合入后更新本表勾选。
