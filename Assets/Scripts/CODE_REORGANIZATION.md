# 代码整理说明

本文档记录了对 `Scripts` 文件夹的代码整理（排除 Flux、RPGCharacterAnimationPack、SuperCharacterController）。

## 整理后的目录结构

```
Scripts/
├── ACTGame/                 # ACT 游戏运行时框架
│   ├── Animation/           # [新增] 动画相关
│   │   └── AnimStateMachine.cs
│   ├── AssetBundle/
│   ├── Config/              # [新增] 配置
│   │   └── InputMappingConfig.cs
│   ├── ExampleScripts/
│   ├── Manager/
│   ├── Skill/
│   ├── Tool/
│   └── UI/
├── EGamePlay/               # ECS 风格战斗框架
│   ├── Config/              # [新增] Luban 配置
│   │   └── Luban/           # [从 XCSkillEditor/Config 迁移]
│   │       ├── item/
│   │       ├── test/
│   │       └── *.cs
│   ├── Combat/
│   ├── Entity/
│   ├── Helper/              # [新增] FastStaticExecutor
│   │   └── FastStaticExecutor.cs  # [从 ACTGame/Tool 迁移]
│   └── ...
├── EGamePlay.Unity/         # EGamePlay 的 Unity 绑定
│   ├── Editor/              # [新增] 编辑器工具
│   │   └── ExecutionMoveEditor/  # [从 ACTGame 迁移]
│   │       ├── DMDrawCurve.cs
│   │       ├── CurvePointControl.cs
│   │       └── Test.cs
│   └── ...
├── Input/                   # [新增] 输入相关
│   ├── RPGFREEInputActions.cs
│   └── RPGFREEInputActions.inputactions
├── XCSkillEditor/           # 技能编辑器
│   └── Config/              # Luban 已迁移至 EGamePlay/Config
├── XiaoCaoTools/
├── Flux/                    # [未改动]
├── RPGCharacterAnimationPack/ # [未改动]
└── SuperCharacterController/  # [未改动]
```

## 主要变更

### 1. FastStaticExecutor → EGamePlay/Helper/
- **原因**：命名空间为 `EGamePlay.Combat`，被 EGamePlay 框架使用
- **原位置**：`ACTGame/Tool/FastStaticExecutor.cs`

### 2. ExecutionMoveEditor → EGamePlay.Unity/Editor/
- **原因**：命名空间为 `EGamePlay`，使用 UnityEditor（AssetDatabase），属于编辑器工具
- **原位置**：`ACTGame/ExecutionMoveEditor/`
- **包含**：DMDrawCurve.cs, CurvePointControl.cs, Test.cs

### 3. AnimStateMachine → ACTGame/Animation/
- **原因**：动画状态机逻辑，归类到 Animation 子目录

### 4. InputMappingConfig → ACTGame/Config/
- **原因**：输入映射配置，归类到 Config 子目录

### 5. RPGFREEInputActions → Scripts/Input/
- **原因**：Input System 相关资源，独立于游戏逻辑
- **注意**：若需重新生成 C# 类，请在 Unity 中选中 `.inputactions` 资产，将 Generate C# Class 路径更新为 `Assets/Scripts/Input/RPGFREEInputActions.cs`

### 6. Luban 配置 → EGamePlay/Config/Luban/
- **原因**：Luban 生成的配置使用 `EGamePlay.Combat` 命名空间，属于战斗框架数据层
- **原位置**：`XCSkillEditor/Config/Luban/`
- **注意**：Luban 数据源（Resources/Config/Luban/ 下的 JSON 等）未改动。若使用 Luban 工具生成代码，请将输出路径配置为 `Assets/Scripts/EGamePlay/Config/Luban`

## 命名空间与引用

所有文件保持原有命名空间，C# 引用通过类型和命名空间解析，不受文件路径影响。Asset、Prefab 等通过 GUID 引用，移动后 GUID 随 `.meta` 保留，引用保持有效。
