# Changelog

本项目的重要变更记录。版本号遵循 `MAJOR.MINOR.PATCH`。

## [8.0.0] - 2026-11-22

### Changed

- **BREAKING**：`ISpawnOnRoundStart.MinAmount` 重命名为 `MinSpawnCount`，语义不变。

### Fixed

- 回合开始生成在分配自定义角色前再次检查玩家是否已持有 `CustomRole`；已有则跳过，并按相同条件继续筛选后续候选玩家。

## [7.0.0] - 2026-11-21

### Changed

- **BREAKING**：`ISpawnDefinition` 只保留 `CustomSpawnPoints`；`MinAmount` / `MaxSpawnCount` 拆分到新的 `ISpawnOnRoundStart : ISpawnDefinition`。
- **BREAKING**：`StandardRole` 不再实现 `ISpawnCondition`，删除 `CanSpawnOnRoundStart`、`MinAmount`、`MaxSpawnCount`；它只用 `CustomSpawnPoints` 在 `AddRole` 后选点传送，不参与自动生成。
- `RoundStartSpawnManager` 只收集 `ISpawnOnRoundStart`；仅实现 `ISpawnDefinition` 的 `StandardRole` 不参与回合开始自动生成。需要条件时由自动生成提供者额外实现 `ISpawnCondition`。
- 版本升至 7.0.0。

## [6.1.0] - 2026-11-20

### Added

- 新增 `ISpawnCondition`，提供 `CanSpawnOnRoundStart()`；`RoundStartSpawnManager` 在生成前检查，波次中的角色也会逐个检查。
- `StandardRole` 虚实现 `ISpawnCondition`，默认返回 `true`；可按在线人数/阵营等条件覆写。

## [6.0.0] - 2026-11-20

### Changed

- **BREAKING**：`ISpawnable` 更名为 `ISpawnDefinition`，`SpawnPoints` 更名为 `CustomSpawnPoints`；移除 `SpawnOnRoundStart`，`RoundStartSpawnManager` 不再要求显式 opt-in。
- `StandardRole` 虚实现 `ISpawnDefinition`：默认 `MinAmount = 0`、`MaxSpawnCount = 0`、`CustomSpawnPoints` 为空；需要参与自动生成的角色自行覆写。
- `StandardRole.AddRole` 在 `SpawnFlags` 含 `UseSpawnpoint` 时，从 `CustomSpawnPoints` 按概率选点并传送；空集合不传送；管理器对 `UseSpawnpoint` 角色不再重复传送。
- 版本升至 6.0.0。

## [5.0.0] - 2026-11-20

### Changed

- **BREAKING**：`ISpawnOnRoundStart` 更名为 `ISpawnable`，并新增 `SpawnOnRoundStart`、`MinAmount` 成员；实现方必须补充这两个属性。
- `SpawnOnRoundStart` 默认应为 `false`；`RoundStartSpawnManager` 只执行显式返回 `true` 的提供者。
- `SpawnPoints` 为空时，角色/波次仍允许生成，但不进行传送；物品生成仍需要至少一个刷怪点。
- `MinAmount` 控制单次生成的最少数量；概率未选够时会从有效但未命中的刷怪点中补足，最多到 `MaxSpawnCount`。
- 版本升至 5.0.0。

## [4.0.1] - 2026-11-19

### Fixed

- SCP-096 挥击门禁转译偏移错误：原实现误删 `GetTeam`/`IsEnemy` 而保留 `get_TargetHub`，使 `IsEnemy096HitGate` 的第二个参数收到 `ReferenceHub` 并触发 `NullReferenceException`，玩家被踢线。已改为同时删除 `get_TargetHub`/`GetTeam`，并保留 `CharacterModel` 判空作为防御。
- 原版 `RemoteAdmin.QueryProcessor.OnDestroy` 在 `_sender.OutputId` 为 null 时以 null 调用 `TryRemove` 导致 `ArgumentNullException`；新增前缀补丁在此时跳过清理，避免干扰玩家断线流程。
- 版本升至 4.0.1。

## [4.0.0] - 2026-11-16

### Added

- `RoundStartSpawnManager`：在原版角色分配完成后，按 `ISpawnOnRoundStart` 的刷怪点概率与 `MaxSpawnCount` 生成波次角色或物品。
- `PositionEntry.Teleport` / `SpawnPointEntry.Teleport` 与 `PositionTeleport` 扩展方法。

### Changed

- **BREAKING**：`ISpawnOnRoundStart` 新增 `MaxSpawnCount` 成员；实现方必须补充该属性。
- 版本升至 4.0.0。

## [3.7.0] - 2026-11-15

### Added

- `RoleEntry.Custom<T>()`：按已注册的自定义角色模板类型创建波次角色条目。

### Changed

- `libs/` 移出版本控制并加入 `.gitignore`；构建前需自行准备本地只读依赖目录。
- 清理仓库中与框架无关的文档、二进制和本机路径引用。
- 文档更新到 3.7.0：位置/刷怪点 API 与生命周期注册说明。

## [3.6.0] - 2026-11-15

### Added

- `ISpawnOnRoundStart` 回合开始刷怪点契约。

## [3.5.0] - 2026-11-15

### Added

- `PositionEntry`：精确世界坐标、具体房间与筛选房间的统一位置值对象。
- `SpawnPointEntry`：位置 + 可选旋转的刷怪点值对象，未指定朝向时随机水平朝向。
- `RoomUtils.FindRooms` 的筛选语义修正：`null` 表示不限；默认枚举值不再被当作真实筛选条件；不再复用共享静态列表。

## [3.4.0] - 2026-11-15

### Added

- `Astra.Lifecycle`：`RegistrationSource`、`RegistrationScope`、`AstraLifecycle`，支持按插件程序集记录并撤销 Astra 注册。
- 物品/技能实例注册与注销：`CustomItemApi.Register/Unregister`、`CustomAbilityApi.Register/Unregister`。
- `AutoRegister` 为事件处理器、设置、技能、补丁、事件方法以及角色/阵营/波次/物品登记来源；`UnregisterAll` 同步清理登记台账。

### Changed

- 注册顺序统一为：阵营 → 波次 → 物品 → 角色；注销按依赖逆序执行。
- `CustomItemManager.Initialize` 支持初始化后增量追加模板。

## [3.3.0] - 2026-11-14

### Added

- RA 指令短别名：`cr`、`ca`、`ci`、`ct`、`cw`。
- `customwave spawn <Id> [玩家]`：直接调用 `CustomWaveApi.Spawn`，不改变波池与默认刷新列表。

### Changed

- 文档与 API 索引更新到 3.3.0。
