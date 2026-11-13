# AstraFramework 框架规范

| 项 | 值 |
|---|---|
| 状态 | 现行 |
| 版本基线 | Astra 3.3.1 |
| 日期 | 2026-11-14 |
| 适用范围 | AstraFramework 仓库（SCP: Secret Laboratory 服务端框架库） |
| 事实来源 | `src/` 与 `docs/API.md`；两者冲突时以源码为准 |
| 优先级 | `AGENTS.md` > 本文档 > 代码注释 |

---

## 1. 设计原则

- Astra 是构建在 LabAPI 之上的**服务端框架库**，不是独立插件或业务仓库。
- 框架只提供机制：自定义角色、阵营、波次、物品、效果、技能、补丁、事件、指令、提示等。
- 具体角色/波次/物品的实现属于使用者的业务包，不进入本仓库。
- 提供三种扩展形态：C# 模板自动注册、`CustomXxxApi.Register(instance)` 实例注册、显式 API 调用。
- 任何改动都应可回滚、可追溯，且不破坏已有使用者。

---

## 2. 命名空间总表

| 命名空间 | 职责 | 代表类型 |
|---|---|---|
| `Astra` | 插件入口、配置、自动注册与共享契约 | `AstraPlugin`、`AutoRegister`、`Config`、`MainManager`、`IIdentifiable`、`ICustomContent` |
| `Astra.Loader` | 注册顺序与生命周期契约 | `ICallOnRegister`、`ICallOnUnregister`、`IdentifiableManager<T>`、`MonoSingleton<T>` |
| `Astra.Roles` | 自定义角色 | `CustomRole`、`StandardRole`、`CustomRoleApi`、`RoleEntry`、`IOriginalRole` |
| `Astra.Teams` | 自定义阵营与阵营关系 | `CustomTeam`、`CustomTeamApi`、`CustomTeamResolver`、`CustomTeamRelation`、`RelationEntry`、`RelationEntryKind`、`TeamRef` |
| `Astra.Waves` | 自定义波次、刷新、排序与调度 | `CustomWave`、`CustomWaveApi`、`ICustomWaveRoleSpawner`、`ICustomWaveRoleSpawnerV2`、`IWaveSpawnRole`、`IWaveSpawnCountProvider`、`WaveScheduler`、`SupportWavePool`、`WeightedWaveAdapter`、`WaveComposition`、`WaveSpawnEntry`、`VanillaWaveDispatch`、`VanillaWaveAdapter` |
| `Astra.Items` | 自定义物品与枪械 | `CustomItem`、`CustomFirearm`、`CustomMicroHid`、`CustomJailbird`、`CustomParticleDisruptor`、`CustomItemApi`、`CustomItemEventHandler`、`ItemEntry` |
| `Astra.Effects` | 自定义效果与效果叠加 | `CustomEffect`、`CustomEffectApi`、`CustomEffectController`、`CustomEffectHandler`、`EffectEntry`、`Effect`、`IEffects` |
| `Astra.Abilities` | 自定义技能 | `CustomAbility`、`StandardAbility`、`CustomAbilityApi`、`CustomAbilityManager`、`IAbilityRole`、`IKeybind`、`ICooldown`、`IPlayerCondition` |
| `Astra.HumeShield` | 自定义 Hume Shield | `CustomHumeShieldApi`、`CustomHumeShieldEventHandler`、`ICustomHumeShieldStat`、`ICustomHumeShieldBlocker` |
| `Astra.Patching` | 补丁注册状态与手动补丁 | `PatchApi`、`PatchStatus`、`IManualPatch` |
| `Astra.Events` | 事件注册与事件参数 | `PlayerEvents`、`ShootingEventArgs`、`ShotEventArgs`、`EventApi`、`EventHandlerStatus`、`EnhancedEventsHandler`、`EventMethodManager` |
| `Astra.Commands` | RA 远程管理指令 | `CustomRoleCommand`、`CustomWaveCommand` 等 5 个命令 |
| `Astra.Hints` | HUD 提示 | `HintApi`、`HintManager`、`HintPositionMapping` |
| `Astra.Audio` | 音频播放 | `AudioApi` |
| `Astra.Countdown` | 倒计时 | `CountdownApi`、`CountdownManager` |
| `Astra.Cancellation` | 可取消令牌 | `CancellationTokenApi` |
| `Astra.ServerSpecificSettings` | 服务器专属设置 | `ServerSpecificSettingBase`、`ServerSpecificSettingManager` |
| `Astra.Enums` | 框架运行枚举 | 见 `docs/API.md` |
| `Astra.Attributes` | 声明式标注特性 | `[EventMethod]`、`[PatchMethod]`、`[ManagedPatch]`、`[ManagedEvent]`、`[AutoRegister]` 等 |
| `Astra.Extensions` | 公共扩展方法 | 见 `docs/API.md` |
| `Astra.Patches` | 框架自带补丁（internal） | `CustomTeam*Patch` 等 |

> 说明：`Astra.Patching`（补丁注册与管理）与 `Astra.Patches`（实际补丁实现）是两个不同命名空间，不要混用。

---

## 3. 目录与命名空间

### 3.1 框架目录

```text
src/Astra.Framework/
├── AstraPlugin.cs, Config.cs, IIdentifiable.cs, ICustomContent.cs   → Astra
├── Loader/                                                          → Astra.Loader
│   ├── AutoRegister.cs, MainManager.cs                              → Astra（历史例外）
│   └── ICallOnRegister.cs, ICallOnUnregister.cs, ...                → Astra.Loader
├── Roles/        → Astra.Roles
├── Teams/        → Astra.Teams
├── Waves/        → Astra.Waves
├── Items/        → Astra.Items
├── Effects/      → Astra.Effects
├── Abilities/    → Astra.Abilities
├── HumeShield/   → Astra.HumeShield
├── Patching/     → Astra.Patching
├── Events/       → Astra.Events
├── Commands/     → Astra.Commands
├── Hints/        → Astra.Hints
├── Audio/        → Astra.Audio
├── Countdown/    → Astra.Countdown
├── Cancellation/ → Astra.Cancellation
├── ServerSpecificSettings/ → Astra.ServerSpecificSettings
├── Enums/        → Astra.Enums
├── Attributes/   → Astra.Attributes
├── Extensions/   → Astra.Extensions
├── Patches/      → Astra.Patches（internal）
└── Properties/
```

### 3.2 规则

- 一个目录一个命名空间，目录名 = 命名空间末段；新能力新建域目录，不塞进已有域。
- `AutoRegister.cs` 与 `MainManager.cs` 位于 `Loader/` 但命名空间为 `Astra`，是唯一历史例外；新增文件不要模仿。
- `src/Astra.Analyzers/` 是独立分析器程序集，不放入 `Astra.Framework`。
- `libs/` 是只读依赖区：游戏与 LabAPI 公开化程序集，不修改；需要新版本时新增目录并登记。

---

## 4. 可见性

### 4.1 公开类型（public）

| 类别 | 类型 |
|---|---|
| 基类 | `CustomRole`、`StandardRole`、`CustomTeam`、`CustomWave`、`CustomItem`、`CustomFirearm`、`CustomMicroHid`、`CustomJailbird`、`CustomParticleDisruptor`、`CustomEffect`、`CustomAbility`、`StandardAbility`、`ServerSpecificSettingBase` |
| 扩展接口 | `IIdentifiable`、`ICustomContent`、`ICallOnRegister`、`ICallOnUnregister`、`ICustomWaveRoleSpawner`、`ICustomWaveRoleSpawnerV2`、`IWaveSpawnRole`、`IWaveSpawnCountProvider`、`IWaveSorter<T>`、`IWeightedWave`、`IAbilityRole`、`IKeybind`、`ICooldown`、`IPlayerCondition`、`IEffects`、`ICustomHumeShieldStat`、`ICustomHumeShieldBlocker`、`IOriginalRole`、`IManualPatch` |
| API 入口 | `CustomRoleApi`、`CustomTeamApi`、`CustomWaveApi`、`CustomItemApi`、`CustomAbilityApi`、`CustomEffectApi`、`CustomHumeShieldApi`、`PatchApi`、`EventApi`、`HintApi`、`AudioApi`、`CountdownApi`、`CancellationTokenApi` |
| 公共运行时 | `CustomAbilityManager`、`HintManager`、`CountdownManager`、`ServerSpecificSettingManager`、`EventMethodManager`、`CustomEffectController`、`CustomEffectHandler`、`CustomItemEventHandler`、`CustomHumeShieldEventHandler`、`EnhancedEventsHandler` |
| 值对象 / DTO | `EffectEntry`、`ItemEntry`、`RoleEntry`、`WaveSpawnEntry`、`WaveComposition`、`RelationEntry`、`TeamRef`、`CustomTeamRelation`、`PatchStatus`、`EventHandlerStatus` |
| 事件与标注 | `PlayerEvents`、`ShootingEventArgs`、`ShotEventArgs`；`Astra.Attributes` 下的特性；`Astra.Enums` 下的枚举 |

### 4.2 内部类型（internal）

以下类型只服务框架自身调度，默认不公开：

- 生命周期与注册：`VersionGuard`
- 各域管理器：`CustomRoleManager`、`CustomRoleTemplateManager`、`CustomWaveManager`、`CustomItemManager`、`CustomAbilityTemplateManager`、`CustomHumeShieldManager`、`AudioManager`、`TokenManager`
- 调度与辅助：`CustomWaveRoleSpawner`、`EffectApplier`、`EffectCatalog`、`PatchManager`、`EventManager`、`CommandSupport`、`TokenSources`、`Shooting`、`ShotTarget`
- 框架自带补丁：`Astra.Patches` 下的 `CustomTeam*Patch` 等

规则：

- `internal` 类型不得出现在任何 `public` 签名中；编译器会拦截，不要用 `InternalsVisibleTo` 绕过。
- 公开类型必须有 XML 文档（`/// <summary>`），公开方法至少一行说明。
- 新增公开 API 前先确认业务是否真的需要直接调用；能 `internal` 就 `internal`。

---

## 5. 依赖方向

| 允许 | 避免 |
|---|---|
| 域内自由引用 | 业务域之间互相静态调用 |
| 任意域 → `Enums` / `Attributes` / `Extensions` / `Events`（参数） | 基类反向依赖业务域管理器 |
| `Loader` → 各域（决定注册顺序） | 域之间循环依赖 |
| `Commands` → 各域 API（指令入口） | 事件参数持有业务域的可变状态 |
| `Patches` → 各域（框架自带补丁） | `public` 签名暴露 `internal` 类型 |

跨域协作优先使用：注册顺序 → 事件 → 显式注入。

当前实例注册顺序为 **阵营 → 波次 → 物品 → 角色**，注销反向。业务需要跨域引用时，先检查是否可由注册顺序或事件解决。

---

## 6. 程序集与版本

| 程序集 | 说明 |
|---|---|
| `Astra.Framework.dll` | net481 运行时框架库，引用 LabAPI 与游戏公开化程序集 |
| `Astra.Analyzers.dll` | Roslyn 分析器，随源码一起编译，不要求使用方在运行时引用 |

- `Astra.Testing`、`Astra.Abstractions`、`Astra.Interfaces`、`Astra.Apis`、`Astra.Systems` 均不存在；旧文档或旧代码中的这些名字已失效。
- 当前不发布 NuGet 包（`IsPackable=false`），通过程序集引用使用。
- `libs/` 下的程序集只读；升级游戏/LabAPI 时新增目录并登记，不原地覆盖。

版本规则：

| 变更 | 版本 |
|---|---|
| 新增公开类型/方法/可选参数 | MINOR |
| 修改公开签名、删除公开成员、改变默认行为 | MAJOR（提交信息标注 `BREAKING`，同步 `docs/API.md`） |
| 修内部实现、文档、注释 | PATCH |

---

## 7. Analyzer 规则

| ID | 内容 | 级别 |
|---|---|---|
| AST1001~1002 | `[EventMethod]` 签名、override/virtual 约定 | Error |
| AST1003 | `[EventMethod]` 可访问性 | Warning |
| AST1004 | `[EventMethod]` 声明的事件不存在 | Error |
| AST2001 | `[PatchMethod]` 方法必须为 `static` | Error |
| AST2002 | `[PatchMethod]` 缺少补丁类型（`HarmonyPatch`） | Error |
| AST2003 | `[PatchMethod]` 指定的目标不存在 | Error |
| AST2101 | `[ManagedPatch]` 缺少目标声明 | Error |
| AST3001 | 版本特性用在非补丁成员上 | Info |
| AST5001 | `[ManagedEvent]` 类型不是事件处理器 | Error |
| AST5002 | `[ManagedPatch]` 类型形状不匹配 | Error |
| AST5003 | `[PatchMethod]` 所在类型不是 `static` | Warning |
| AST5004 | `[EventMethod]` 不在事件处理器中 | Error |
| AST5005 | 反射实例化契约 | Error |

> 不存在 AST6001/6002、AST6101/6102 或 AZF 系列规则；遇到旧文档中的这些编号请忽略。
>
> 确有正当理由时，用 `#pragma warning disable ASTxxxx` 并附一行注释说明原因，不要全局关闭。

---

## 8. 构建与验证

```bash
python tools/vsbuild.py src/Astra.Framework/Astra.Framework.csproj -p:Configuration=Release -p:Platform=x64
python tools/vsbuild.py src/Astra.Analyzers/Astra.Analyzers.csproj -p:Configuration=Release
```

| 要求 | 强度 |
|---|---|
| 编译 0 error | 必须 |
| 不新增 warning | 建议 |
| `-warnaserror` | 发布前跑一次 |
| 分析器自测 | 改规则时必须（正例、反例各一） |

- `net481` 与公开化程序集必须走 `tools/vsbuild.py`；`dotnet build` 会报 MSB4803 或 RuntimeIdentifier 错误。
- `docs/API.md` 的 `AUTO-GEN` 段由 `tools/ApiDoc.cs` 扫描源码生成，不要手改：
  - 重新生成：`dotnet run tools/ApiDoc.cs`
  - 校验漂移：`dotnet run tools/ApiDoc.cs -- --check`
- 公开 API 变更后，至少执行一次漂移校验并提交更新后的 `docs/API.md`。
