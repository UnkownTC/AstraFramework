# Changelog

本项目的重要变更记录。版本号遵循 `MAJOR.MINOR.PATCH`。

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
