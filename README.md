# ACTGameEditor

<div align="center">

**本地 Unity 6 第三人称动作战斗工程** —— 把《绝区零》《鸣潮》的「可读招、有段落、能换人」战斗循环接到可扩展的运行时上。

`Unity 6 + URP` · `自研 Entity/Component（非 DOTS）` · `Luban 数值表` · `Flux 技能时间轴` · `文档驱动`

[📖 项目全景](Docs/ProjectOverview.md) · [📐 硬约定](Docs/ProjectConventions.md) · [🧭 未完成索引](Docs/ActUnfinishedIndex.md)

</div>

---

## ✨ 现在能玩到什么

- **完整打击感链**：HitStop 顿帧（轻重段分级）、Trauma 震屏、FOV 冲击、受击闪白、闪避残影、极限闪避时空断裂（灰屏 + 世界减速）
- **防御博弈**：敌人黄闪可招架、红闪必须闪（学绝区零，不做鸣潮金圈弹刀）
- **敌人进攻权导演**：Token 竞拍 + 车轮战 + 站位槽——同屏 4~8 只怪也不会一起乱砍
- **三人小队换人**：合轴退场（旧人把刀打完再走）、快速支援、极限支援（招架）、回避支援、支援点数
- **偏谐条 + F 谐度破坏**：鸣潮 3.0 式高潮（原绝区零失衡连携已屏蔽）
- **技能时间轴编辑器**：Flux 工作台改轴 → Play Mode 立刻验证，编辑器与运行时同一套轴语义
- **四根时间钟**：世界 / 玩家 / 相机 / 实体分层，断裂、时停、冻结互不污染（全程不用 `Time.timeScale`）

## 🚀 快速开始

1. **环境**：Unity 6 + URP，直接打开工程（无第三方 SDK、无联网依赖）。
2. 打开 `Assets/Scenes/SkillEditor.unity`，点 **Play**。
3. **战斗键**：

   | 键 | 作用 | | 键 | 作用 |
   |---|---|---|---|---|
   | `J` | 普攻（连按连段） | | `Q` / `E` | 换人 / 支援 / 招架（按窗口推断） |
   | `K` | 闪避（极限闪避进时空断裂） | | `F` | 谐度破坏（敌人偏谐条满时） |
   | `L` | 战技 / EX | | `Space` | 跳跃 |
   | `I` | 大招（时停演出） | | `Tab` / 鼠标中键 | 锁定切换 |

4. **调试键**（编辑器场景，Play 中）：`F2` 刷杂兵 · `F3` 刷精英 · `F4` 测试小队（4 杂兵 + 2 精英）· `F9` 技能组满级。
5. **两分钟体验路线**：普攻连段 → 极限闪避看断裂 → 黄闪时按 Q 招架撞刃 → 攒满偏谐按 F → 大招时停。

> 键位与招式包规则的权威文档：[Docs/ActSkillKitConfig.md](Docs/ActSkillKitConfig.md)。

## 🧭 文档导航

| 文档 | 内容 | 谁该看 |
|---|---|---|
| [Docs/ProjectOverview.md](Docs/ProjectOverview.md) | 全景：已落地玩法、分层、配表与编辑器、还缺什么 | 所有人先看这个 |
| [Docs/ProjectConventions.md](Docs/ProjectConventions.md) | 目录、分层、硬约定（四根钟 / 出招口 / 热路径） | 改代码前必读 |
| [Docs/ActUnfinishedIndex.md](Docs/ActUnfinishedIndex.md) | **未完成项总索引** + 已放弃清单 | 开工前先看 |
| 小队 · 敌人 AI · 招式包 · 走跑 · 相机 · Buff | 专题文档，完整列表见未完成索引 | 按需 |

## 🏗️ 一句话架构

```
输入 → 招式包表(CharacterSlot/Kit) → Enqueue → Gate → 技能轴(XC)
  → 开盒 → HitPipeline → DamageAction（段表乘区）
  → CombatStateDirector（唯一写状态）→ 表现包（不进结算）
```

- **伤害只查段表** `(SkillId, SegmentIndex)`：时间轴不填倍率，升级/失衡/异常全部挂在段表上。
- **数字进 Luban Excel**，**轴进 Flux 预制体**——两套配置，改错入口会白干（详见 [ProjectConventions](Docs/ProjectConventions.md) §八）。
- **内核分层**：`EGamePlay`（纯逻辑，只认 `ICombatUnit`）↔ `ACTGame`（落地与表现）↔ `XCSkillEditor` / `Flux`（轴运行时与编辑器）。

## 📁 目录速览

```
Assets/Scripts/EGamePlay/        战斗内核：Entity、Buff、伤害乘区、四根钟
Assets/Scripts/EGamePlay.Unity/  动画 / 电机等 Unity 适配
Assets/Scripts/ACTGame/          落地：小队、招架、偏谐、导演 AI、相机、渲染
Assets/Scripts/XCSkillEditor/    XC 轴运行时与编辑器 UI
Assets/Scripts/Flux/             时间轴编辑器（技能工作台）
Tools/Config/Datas/              Luban 源表（xlsx）
Assets/Editor/SkillSequences/    技能轴源（Flux 预制体）
Docs/                            专题文档（已完成归档在 Docs/已完成/）
```

## 🗺️ 接下来做什么

未完成项的统一入口是 [Docs/ActUnfinishedIndex.md](Docs/ActUnfinishedIndex.md)。当前建议下一刀：**安比 13100 新角色流水线** → 延奏（小队 M4.3）→ 走跑步态机 P0 → 属性异常/紊乱。

## ⚠️ 注意事项

- 本项目是**本地学习/研究工程**：招式设计可参考商业游戏，动画资产请使用自有或授权资源；
- 热路径守则：禁 LINQ、禁每帧 `GetComponent`/`new List`、物理用 NonAlloc；
- 文档与代码不一致处以 [ActUnfinishedIndex.md](Docs/ActUnfinishedIndex.md) §五 为准。
