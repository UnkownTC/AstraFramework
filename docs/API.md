# AstraFramework 公开 API 速查

| 项 | 值 |
|---|---|
| 适用版本 | `3.7.0`（`Directory.Build.props`） |
| 面向 | 写业务插件的人；改框架公开签名的人 |
| 事实来源 | §3 由 `tools/ApiDoc.cs` 扫描源码生成；§1、§2 手写，与源码冲突时以源码为准 |

**怎么读**

- 要用框架做事 → §1 看框架在什么时机、按什么规则接管你的类型；§2 按需求查该继承谁、走哪个入口。
- 要改框架 → 先在 §3 确认类型/成员是否属于对外 API。

## 1. 加载与自动注册

### 1.1 入口与生命周期

`AstraPlugin : Plugin<Config>` 是唯一入口，`Priority = LoadPriority.Highest`，保证框架基础设施先于依赖它的插件就绪。

| 阶段 | 行为 |
| --- | --- |
| `Enable()` | 把 `MainManager` 注册为 LabAPI 事件处理器；`Config` 同步到静态访问点 `AstraPlugin.Configuration`，并回写默认配置 |
| `OnServerPluginsEnabled()` | 全部插件启用后触发一次 `AutoRegister.RegisterAll(PluginLoader.Plugins.Values)`；`Initialized` 保证幂等，单个类型失败只记日志、不中断 |
| `Disable()` | 注销 `MainManager` 并调用 `AutoRegister.UnregisterAll()`，回收事件、设置、补丁、令牌、事件方法，并清空 Team / Wave / Item / Role / Ability 注册表 |

### 1.2 注册顺序

`AutoRegister` 先收集模板，再按依赖顺序实例化：

1. **Team**：`CustomTeam.Initialize()`
2. **Wave**：`CustomWaveManager.Initialize()`（需要阵营实例；波次会把自身反向登记到阵营）
3. **Item**：`CustomItemManager.Initialize()`
4. **Role**：`CustomRoleTemplateManager.RegisterRole()`（需要波次实例；实现 `IWaveSpawnRole` 的角色在注册实例时挂到波次）
5. `ServerSpecificSettingManager.SendToAll()` 统一下发服务器专属设置

### 1.3 判定矩阵

`AutoRegister.RegisterType` 对扫描到的每个类型按序判定：

| 类型 / 成员特征 | 注册动作 |
| --- | --- |
| 非抽象、实现 `IIdentifiable`，且继承 `CustomEventsHandler` | 注册为事件处理器；**但**同时实现 `ICustomContent` 的角色/技能模板例外，由各自的模板管理器以「唯一模板实例」注册，避免同一类型被构造两次 |
| 实现 `IIdentifiable` 且继承 `ServerSpecificSettingBase` | `ServerSpecificSettingManager.Register` |
| 实现 `IIdentifiable` 且继承 `CustomAbility` | `CustomAbilityTemplateManager.RegisterAbility` |
| 实现 `IIdentifiable` 且继承 `CustomRole` / `CustomWave` / `CustomTeam` / `CustomItem` | 收集，按 §1.2 顺序注册为模板 |
| 类上标 `[ManagedEvent("id")]`（且前面未作为事件注册过） | 注册为事件处理器，用于没有 `IIdentifiable` 的类 |
| 类上标 `[ManagedPatch]`，且有 `[HarmonyPatch]` 或实现 `IManualPatch` | 由 `PatchManager.PatchType` 托管 |
| 静态方法上标 `[PatchMethod(...)]` | 方法级补丁，无需外层类再标 `[ManagedPatch]` |
| 静态方法上标 `[EventMethod(...)]` | 由 `EventMethodManager` 订阅到目标 .NET 事件；实例方法由 `EnhancedEventsHandler` 在构造时自注册 |
| 类上标 `[DisableAutoRegister]` | 只登记、不启用：事件处理器用 `EventApi.Enable(id)`、补丁用 `PatchApi.Enable(id)` 手动启用 |

> 模板由 `Activator.CreateInstance` 实例化，派生类需要公开无参构造；每个玩家拿到的是 `CreateInstance()` 出来的独立副本，模板实例本身只承载「类型级」信息。

### 1.4 外部内容（实例）注册

C# 模板由自动注册扫描 Type 创建；Lua / 外部框架可直接注册已实例化的模板：

| 内容 | 注册 | 注销 |
| --- | --- | --- |
| 角色 | `CustomRoleApi.Register(role)` | `CustomRoleApi.Unregister(id)` |
| 阵营 | `CustomTeamApi.Register(team)` | `CustomTeamApi.Unregister(id)` |
| 波次 | `CustomWaveApi.Register(wave)` | `CustomWaveApi.Unregister(id)` |
| 物品 | `CustomItemApi.Register(item)` | `CustomItemApi.Unregister(id)` |
| 技能 | `CustomAbilityApi.Register(ability)` | `CustomAbilityApi.Unregister(id)` |

- 必须在 `OnServerPluginsEnabled`（框架自动注册完成）之后调用；阵营 / 波次在初始化前注册会返回 `false`，避免触发实例化早退导致 C# 模板丢失。
- 同 Id 不同实例会被拒绝（不覆盖）；同实例重复注册幂等。
- 注册顺序：阵营 → 波次 → 物品 → 角色；注销顺序相反：角色 → 物品 → 波次 → 阵营。技能无跨域依赖，可随每次注册处理。
- `Unregister` 只注销模板；玩家身上已套用的角色 / 技能 / 物品实例需调用方自行处理。
- 波次角色生成器支持链式：`CustomWaveApi.AddRoleSpawner(...)` 按注册顺序依次尝试；`SetRoleSpawner` 仍为「替换全部」。实现 `ICustomWaveRoleSpawnerV2` 可接收原版 + 自定义角色条目（`RoleEntry`），旧接口只处理自定义角色 Id。
- Entry 化：波次的 `Teams` 用 `TeamRef`、`Roles` / `Table` 用 `RoleEntry`、额外刷新项用 `WaveSpawnEntry`；角色的初始物品 `Items` 用 `ItemEntry`。

### 1.5 按来源撤销（插件生命周期）

`Astra.Lifecycle` 把「经 Astra 登记、且能识别来源」的条目记到台账，用于插件禁用时协作式清理：

```csharp
// 插件 Enable：创建作用域并保存
_scope = AstraLifecycle.CreateScope("MyPlugin", typeof(MyPlugin).Assembly);
CustomRoleApi.Register(role, _scope);
CustomItemApi.Register(item, _scope);

// 插件 Disable：释放作用域
_scope.Dispose();
// 等价：AstraLifecycle.UnregisterSource(typeof(MyPlugin).Assembly);
```

- 自动注册的条目以 `type.Assembly` 为来源；`AstraLifecycle.UnregisterSource(assembly)` 可撤销某程序集在 Astra 登记的条目。
- 按注册逆序撤销：角色 / 物品 / 波次 / 阵营，再撤销技能 / 事件方法 / 补丁 / 设置 / 事件；阵营与波次依赖顺序由内容阶段的逆序保证。
- 只管理 Astra 自己的注册表：插件直接订阅 LabAPI 事件、直接打 Harmony 补丁、直接操作游戏对象，不在此范围。
- `RegistrationScope.Dispose()` 是协作式的：LabAPI 没有「单个插件禁用」事件，必须由插件在 `Disable()` 中自行调用。

## 2. 扩展点速查

| 我要做… | 继承 / 实现 | 用哪个入口 | 要点 |
| --- | --- | --- | --- |
| 自定义角色 | `CustomRole`；要技能 / 护盾 / 效果时用 `StandardRole` | 套用 `CustomRoleApi.AddRole<T>(player)` 或 `AddRole(player, id)`；查询 `GetRole` / `TryGetRole`；移除 `RemoveRole` | `Id` 全局唯一。事件回调跑在模板实例上，判断「该玩家是否正持有同类角色」用 `Check(player)`，不要比 `Owner`（模板恒为 `null`）。阵营归属读 `Team`（`TeamRef`）；初始物品 `Items` 用 `ItemEntry`（原版 ItemType / 自定义物品 Id，自定义物品会走 `Give`） |
| 自定义阵营 | `CustomTeam` | 查询 `CustomTeamApi.Teams` / `Find` / `Find<T>` | 一次性实例化、跨回合复用，可直接持有实例引用 |
| 自定义波次 | `CustomWave`（`Id` / `Name` / `Teams`：`IReadOnlyList<TeamRef>`）；角色侧实现 `IWaveSpawnRole` 声明归属与默认数量，波次可用 `Spawns`（`WaveSpawnEntry`）额外声明 / 覆盖刷新项 | 宿主注册生成器 `ICustomWaveRoleSpawner`（`SetRoleSpawner`）或 `ICustomWaveRoleSpawnerV2`（`AddRoleSpawner`）；执行 `CustomWaveApi.Spawn`；加权调度用 `WaveScheduler` | `Roles` / `Table` 用 `RoleEntry`（原版 + 自定义可混编）；角色可用 `IWaveSpawnCountProvider` 按波次声明数量；波次实现 `IWeightedWave` 后 `WaveScheduler.Load/Advance` 按权重与人数门槛推进；主阵营读 `Team`（`TeamRef`）；无生成器时原版角色由框架直接套用、自定义角色会失败；支援池用 `SupportWavePool`（Register / SetWeight / Pick / GetWithFallback）；原版波次标记用 `VanillaWaveDispatch` / `VanillaWaveAdapter`；`CustomWaveApi.Spawn(id, players)` 可直接执行 |
| 自定义物品 | `CustomItem`（`Id` / `Name` / `BaseItem`），或 `CustomFirearm` / `CustomMicroHid` / `CustomJailbird` / `CustomParticleDisruptor` | 发放 `CustomItemApi.Give<T>(player)` 或 `Give(player, id)`；查询 `Find` / `FindByItem` | 复用原版 `ItemType`，同一 `BaseItem` 只能注册一个自定义物品（重复注册告警并跳过）；射击默认不介入，只有覆写 `OnShooting` 的物品才算接管开火；装备 / 拾取 / 使用 / 投掷 / 换弹 / 瞄准 / 命中均有可覆写钩子 |
| 刷怪点 / 位置 | `PositionEntry` / `SpawnPointEntry` / `ISpawnOnRoundStart` | 构造 `PositionEntry.Find(Vector3)` / `Find(RoomIdentifier, local)` / `Find(RoomName/Zone/Shape/组合, local)` / `Random(local)`；`SpawnPointEntry.Find(...)` / `Random(...)`；解析 `TryGetWorldPosition` / `TryResolve` | 精确世界坐标或具体房间稳定；只给筛选条件时解析阶段随机选房；未指定旋转时 `TryResolve` 返回随机水平朝向；具体房间引用失效时回退到筛选条件重新选房；`ISpawnOnRoundStart.SpawnPoints` 只声明点与概率，由消费方读取后生成 |
| 回合开始生成 | `ISpawnOnRoundStart` | 实现后返回 `IReadOnlyDictionary<SpawnPointEntry, float>` | 键为刷怪点，值为该点概率（0~1，按条目独立判定）；接口只提供契约，不规定生成实体，生成器读取 `SpawnPoints` 后自行决定 |
| 状态效果 | `CustomEffect`（需 `(Player)` 公开构造，或用工厂重载） | 声明 `EffectEntry.Vanilla(...)` / `EffectEntry.Custom<T>(...)`；施加与移除 `CustomEffectApi.Apply` / `Remove` | 原版效果与自定义效果共用同一套 `EffectEntry` 声明；`CanStack = true` 时同类效果经 `CustomEffectController.AddEffect` 叠加层数（强度 ×层数、时长累加） |
| 自定义技能 | `CustomAbility`；需按键 / 冷却 / 条件契约时叠加 `IKeybind` / `ICooldown` / `IPlayerCondition`；标准实现为 `StandardAbility` | 角色侧通过 `IAbilityRole.Abilities` 声明（`StandardRole` 已实现）；玩家侧 `CustomAbilityManager.AddAbility`（`player.TryGetAbilityManager`）；查询 `CustomAbilityApi` | 模板按 Id 注册，每个玩家一份管理器实例 |
| 休谟护盾 | `ICustomHumeShieldStat`（`StandardRole` 已实现） | 随角色套用自动接管生灭 | `HumeShieldCurve` 非空时以曲线为准 |
| 事件处理 | `CustomEventsHandler`；需要射击扩展事件时用 `EnhancedEventsHandler` | 自动注册（见 §1.3）；状态与启停用 `EventApi` | 角色 / 技能的事件在模板实例上回调；`[EventMethod]` 的方法可标注多个、订阅多个签名一致的事件，重复声明同一事件会自动去重（只触发一次） |
| Harmony 补丁 | 类级 `[ManagedPatch]` + `[HarmonyPatch]` 或 `IManualPatch`；方法级 `[PatchMethod]` | 启停 `PatchApi.Enable` / `Disable`；状态 `PatchApi.TryGetStatus` | 可用 `[PatchApiVersion]` / `[PatchGameVersion]` 做版本门禁，`Config` 里有两个忽略开关 |
| 提示 / 音频 / 倒计时 | — | `HintApi` / `AudioApi` / `CountdownApi` | 均按字符串 Id 精确控制；音频在回合重启时自动归还播放器到对象池 |
| 插件生命周期 | — | 注册时传入 `RegistrationScope`，或 `AstraLifecycle.CreateScope(...)`；禁用时 `Dispose` / `AstraLifecycle.UnregisterSource(assembly)` | 只撤销 Astra 登记的内容；直接订阅 LabAPI / 直接 Harmony 不在范围；自动注册条目按 `type.Assembly` 归属 |
| 取消令牌 | — | `CancellationTokenApi` + `CancellationTokenCategory` | 按类别随玩家加入 / 离开、回合重启统一取消 |
| 服务器专属设置 | `ServerSpecificSettingBase` | 实现 `IIdentifiable` 即自动登记，启动后统一下发 | 技能按键复用其中的 SSKeybindSetting |

### 2.1 RA 指令

| 指令 | 别名 | 子指令 | 说明 |
| --- | --- | --- | --- |
| `customrole` | `crole` / `cr` | `list` / `info <Id>` / `get <玩家>` / `give <玩家> <Id>` | 自定义角色 |
| `customability` | `cability` / `ca` | 同上 | 自定义技能 |
| `customitem` | `citem` / `ci` | 同上 | 自定义物品 |
| `customteam` | `cteam` / `ct` | `list` / `info <Id>` | 自定义阵营（不归属玩家，不提供 get / give） |
| `customwave` | `cwave` / `cw` | `list` / `info <Id>` / `spawn <Id> [玩家]` | 自定义波次（不提供 get / give；`spawn` 直接生成，不改动波次池/调度器） |

- 模板匹配：只按 `Id`（忽略大小写），不做名称兜底。
- 玩家匹配：SteamId → PlayerId；任何可能含空格的参数直接跳过，不做昵称兜底。
- 权限：`list` / `info` 需 `PlayerPermissions.GameplayData`；`get` 需 `PlayerSensitiveDataAccess`；`give` 分别需 `ForceclassWithoutRestrictions`（角色）、`Effects`（技能）、`GivingItems`（物品）；`spawn` 需 `PlayerPermissions.RespawnEvents`。

## 3. 公开 API 清单

<!-- AUTO-GEN:BEGIN（由 tools/ApiDoc.cs 生成，勿手改） -->

> 本节由 `tools/ApiDoc.cs` 扫描 `src/Astra.Framework/**/*.cs` 生成，**请勿手改**；改动公开签名后重新生成。

- **统计**：公开类型 113 个（class 75 / enum 8 / interface 20 / struct 10），命名空间 22 个；公开成员 776 个。
- **口径**：`public` 与 `protected` 成员均属对外 API（后者可被子类使用）；`internal` / `private` / `file` / `private protected` 不收录。

### 命名空间索引

| 命名空间 | 公开类型 |
| --- | --- |
| `Astra` | `AstraPlugin`、`AutoRegister`、`Config`、`ICustomContent`、`IIdentifiable`、`MainManager` |
| `Astra.Abilities` | `CustomAbility`、`CustomAbilityApi`、`CustomAbilityManager`、`IAbilityRole`、`ICooldown`、`IKeybind`、`IPlayerCondition`、`StandardAbility` |
| `Astra.Attributes` | `DisableAutoRegisterAttribute`、`EventMethodAttribute`、`ManagedEventAttribute`、`ManagedPatchAttribute`、`PatchApiVersionAttribute`、`PatchGameVersionAttribute`、`PatchMethodAttribute` |
| `Astra.Audio` | `AudioApi` |
| `Astra.Cancellation` | `CancellationTokenApi` |
| `Astra.Commands` | `CustomAbilityCommand`、`CustomItemCommand`、`CustomRoleCommand`、`CustomTeamCommand`、`CustomWaveCommand` |
| `Astra.Countdown` | `CountdownApi`、`CountdownManager` |
| `Astra.Effects` | `CustomEffect`、`CustomEffectApi`、`CustomEffectController`、`CustomEffectHandler`、`Effect`、`EffectEntry`、`IEffects` |
| `Astra.Enums` | `AbilityFailReason`、`CancellationTokenCategory`、`EffectClassification`、`EffectType`、`HintPosition`、`KeybindType` |
| `Astra.Events` | `EnhancedEventsHandler`、`EventApi`、`EventHandlerStatus`、`EventMethodManager`、`PlayerEvents`、`ShootingEventArgs`、`ShotEventArgs` |
| `Astra.Extensions` | `CollectionExtensions`、`MapExtensions`、`PlayerEffectExtensions`、`PlayerExtensions` |
| `Astra.Hints` | `HintApi`、`HintManager`、`HintPositionMapping` |
| `Astra.HumeShield` | `CustomHumeShieldApi`、`CustomHumeShieldEventHandler`、`ICustomHumeShieldBlocker`、`ICustomHumeShieldStat` |
| `Astra.Items` | `CustomFirearm`、`CustomItem`、`CustomItemApi`、`CustomItemEventHandler`、`CustomJailbird`、`CustomMicroHid`、`CustomParticleDisruptor`、`ItemEntry` |
| `Astra.Lifecycle` | `AstraLifecycle`、`RegistrationScope`、`RegistrationSource` |
| `Astra.Loader` | `ICallOnRegister`、`ICallOnUnregister`、`IdentifiableManager<T>`、`MonoSingleton<T>` |
| `Astra.Patching` | `IManualPatch`、`PatchApi`、`PatchStatus` |
| `Astra.Positions` | `ISpawnOnRoundStart`、`PositionEntry`、`SpawnPointEntry` |
| `Astra.Roles` | `CustomRole`、`CustomRoleApi`、`IOriginalRole`、`RoleEntry`、`StandardRole` |
| `Astra.ServerSpecificSettings` | `ServerSpecificSettingBase`、`ServerSpecificSettingManager` |
| `Astra.Teams` | `CustomTeam`、`CustomTeamApi`、`CustomTeamRelation`、`CustomTeamResolver`、`RelationEntry`、`RelationEntryKind`、`TeamRef` |
| `Astra.Waves` | `CustomWave`、`CustomWaveApi`、`DelegatedWaveSorter<T>`、`ICustomWaveRoleSpawner`、`ICustomWaveRoleSpawnerV2`、`IWaveSorter<in T>`、`IWaveSpawnCountProvider`、`IWaveSpawnRole`、`IWeightedWave`、`SupportWavePool`、`VanillaWaveAdapter`、`VanillaWaveDispatch`、`WaveComposition`、`WaveScheduler`、`WaveSorterExtensions`、`WaveSpawnEntry`、`WeightedWaveAdapter` |

---

### `Astra`

#### `AstraPlugin`

`public class AstraPlugin : Plugin<Config>`

源码：`src/Astra.Framework/AstraPlugin.cs`

> Azure Framework（晴空框架）的 LabAPI 插件入口。 加载优先级为 Highest，确保框架基础设施先于依赖它的插件就绪。

- `public static Config Configuration { get; private set; }` — 当前生效的框架配置。
- `public override string Author { get; }`
- `public override string Name { get; }`
- `public override string Description { get; }`
- `public override Version Version { get; }`
- `public override Version RequiredApiVersion { get; }`
- `public override LoadPriority Priority { get; }`
- `public override void LoadConfigs()` — 加载配置后同步到静态访问点，供各管理器读取。
- `public override void Enable()`
- `public override void Disable()` — 禁用时注销主管理器并销毁所有自动注册的资源。

#### `AutoRegister`

`public static class AutoRegister`

源码：`src/Astra.Framework/Loader/AutoRegister.cs`

> 自动注册核心：扫描所有已加载插件程序集，按类型自动注册事件处理器、补丁、技能、设置与事件方法。 单个程序集或类型的注册失败只记录日志，不会中断其余组件的注册。

- `public static bool RegisterAll(IEnumerable<Assembly> assemblies)` — 扫描并注册所有组件。幂等：重复调用返回 false。
- `public static void UnregisterAll()` — 注销所有自动注册的资源（事件、设置、补丁、令牌、事件方法）。

#### `Config`

`public class Config`

源码：`src/Astra.Framework/Config.cs`

> 晴空框架的配置文件模型（与插件目录下的 config.yml 对应）。

- `public bool IsDebug { get; set; }` — 调试输出开关。
- `public bool IgnorePatchApiMajorVersionDiff { get; set; }` — 忽略补丁声明的 LabApi 主版本差异。
- `public bool IgnorePatchGameMajorVersionDiff { get; set; }` — 忽略补丁声明的游戏主版本差异。

#### `ICustomContent`

`public interface ICustomContent : IIdentifiable`

源码：`src/Astra.Framework/ICustomContent.cs`

（无公开成员）

#### `IIdentifiable`

`public interface IIdentifiable`

源码：`src/Astra.Framework/IIdentifiable.cs`

> 表示一个具有全局唯一字符串标识（Id）的组件。 实现该接口的类型可被框架通过 Id 精确注册、注销与查找。

- `string Id { get; }` — 获取该组件的全局唯一标识。

#### `MainManager`

`public class MainManager : CustomEventsHandler`

源码：`src/Astra.Framework/Loader/MainManager.cs`

> 框架主管理器：在服务器插件全部启用后触发一次自动注册流程。

- `public override void OnServerPluginsEnabled()`


### `Astra.Abilities`

#### `CustomAbility`

`public abstract class CustomAbility : CustomEventsHandler, ICustomContent, ICallOnRegister, ICallOnUnregister`

源码：`src/Astra.Framework/Abilities/CustomAbility.cs`

> 自定义技能的抽象基类。 继承该类并实现 Id 即可注册为全局技能模板， 框架会为每个玩家创建独立的技能实例并管理其生命周期。

- `public abstract string Id { get; }` — 获取技能的全局唯一标识。
- `public int AssignedSettingId { get; internal set; }` — 该实例实际生效的 SettingId 覆盖值；-1 表示沿用模板声明的 SettingId。 仅在把技能授予玩家且模板 SettingId 与该玩家既有按键冲突时由框架改写， **只作用于当前玩家专属实例，模板自身不受影响**。
- `public int EffectiveSettingId { get; }` — 获取该实例实际生效的 SettingId：已改配则返回改配值，否则回落到模板的 SettingId（非按键技能恒为 -1）。
- `public virtual string Name { get; }` — 获取技能的显示名称。
- `public virtual string Description { get; }` — 获取技能的描述。
- `protected Player Owner { get; set; }` — 技能当前归属的玩家。
- `protected virtual void OnAdded(Player player)` — 技能被添加到玩家时调用。
- `protected virtual void OnRemoved(Player player)` — 技能从玩家移除时调用。
- `protected virtual void OnTriggered(Player player)` — 技能被触发时调用。
- `public override bool Equals(object obj)`
- `public override int GetHashCode()`
- `public virtual void OnRegisterEvents()` — 技能模板被框架登记后调用；默认空实现，可覆写做模板级初始化。
- `public virtual void OnUnregisterEvents()` — 技能模板从框架注销前调用；默认空实现，可覆写清理模板级资源。
- `public virtual CustomAbility CreateInstance()` — 基于当前模板创建一个技能实例。默认使用反射无参构造，可覆写以自定义实例化逻辑。

#### `CustomAbilityApi`

`public static class CustomAbilityApi`

源码：`src/Astra.Framework/Abilities/CustomAbilityApi.cs`

> 自定义技能对外静态 API，提供技能模板与玩家技能实例的查询能力。

- `public static IReadOnlyCollection<CustomAbility> AbilityTemplates { get; }` — 获取当前已注册的全部技能模板。
- `public static bool Register(CustomAbility ability)` — 按实例注册技能模板（外部框架 / JSON 适配器用）。
- `public static bool Register(CustomAbility ability, RegistrationScope scope)` — 在指定注册作用域内按实例注册技能模板。
- `public static bool Unregister(string id)` — 按 Id 注销技能模板。
- `public static bool TryGetAbilityTemplate(string id, out CustomAbility ability)` — 按 Id 获取全局技能模板。
- `public static bool TryGetAbilityTemplate(Type type, out CustomAbility ability)` — 按类型获取全局技能模板。
- `public static bool TryGetAbilityTemplate<T>(out T ability) where T : CustomAbility, new()` — 按泛型类型获取全局技能模板。
- `public static bool TryGetAbilityManager(Player player, out CustomAbilityManager manager)` — 获取指定玩家的技能管理器。
- `public static bool TryGetPlayerAbility<T>(Player player, out T ability) where T : CustomAbility, new()` — 按泛型类型获取指定玩家的技能实例。
- `public static bool TryGetPlayerAbility(Player player, string id, out CustomAbility ability)` — 按 Id 获取指定玩家的技能实例。
- `public static bool TryGetPlayerAbility(Player player, int settingId, out CustomAbility ability)` — 按 SettingId 获取指定玩家的技能实例。

#### `CustomAbilityManager`

`public sealed class CustomAbilityManager : CustomEventsHandler, IDisposable`

源码：`src/Astra.Framework/Abilities/CustomAbilityManager.cs`

> 单个玩家的技能管理器：负责该玩家技能实例的增删查与触发。 每个玩家在加入服务器时由框架创建一个实例，离开时释放。

- `public CustomAbilityManager(Player owner)` — 初始化技能管理器。
- `public Player Owner { get; }` — 归属玩家。
- `public IReadOnlyCollection<CustomAbility> Abilities { get; }` — 该玩家当前拥有的所有技能。
- `public int AbilityCount { get; }` — 技能总数。
- `public int KeybindAbilityCount { get; }` — 按键技能总数。
- `public bool TryGetAbility<T>(out T ability) where T : CustomAbility` — 按泛型类型获取技能实例。
- `public bool TryGetAbility(string id, out CustomAbility ability)` — 按 Id 获取技能实例。
- `public bool TryGetAbility(int settingId, out CustomAbility ability)` — 按 SettingId 获取技能实例。
- `public void TriggerAbility<T>() where T : CustomAbility` — 按泛型类型触发技能。
- `public void TriggerAbility(string id)` — 按 Id 触发技能。
- `public void TriggerAbility(int settingId)` — 按 SettingId 触发技能。
- `public static bool TryGetAbilityManager(Player player, out CustomAbilityManager manager)` — 获取指定玩家的技能管理器。
- `public static CustomAbilityManager GetAbilityManager(Player player)` — 获取指定玩家的技能管理器（不存在时返回 null）。
- `public static void AddManager(Player player)` — 为指定玩家创建并初始化技能管理器。
- `public static void RemoveManager(Player player)` — 移除并释放指定玩家的技能管理器。
- `public void Init()` — 将管理器登记到全局表。
- `public void ClearAll()` — 清空该玩家的所有技能。
- `public CustomAbility AddAbility<T>() where T : CustomAbility, new()` — 按泛型类型添加技能实例。
- `public CustomAbility AddAbility(string id)` — 按 Id 添加技能实例。
- `public void RemoveAbility(string id)` — 按 Id 移除技能。
- `public void RemoveAbility<T>() where T : CustomAbility` — 按泛型类型移除技能。
- `public static void ProcessUserInput(ReferenceHub hub, ServerSpecificSettingBase settings)` — 处理玩家的按键设置输入，触发对应技能的 KeyDown / KeyUp。 通过 EventMethodAttribute 订阅到 ServerOnSettingValueReceived。
- `public void Dispose()` — 释放管理器：清空技能并从全局表移除。

#### `IAbilityRole`

`public interface IAbilityRole`

源码：`src/Astra.Framework/Abilities/IAbilityRole.cs`

- `CustomAbility[] Abilities { get; }`

#### `ICooldown`

`public interface ICooldown`

源码：`src/Astra.Framework/Abilities/ICooldown.cs`

> 表示一个带有冷却时间的能力。

- `bool IsReady { get; }` — 获取能力当前是否处于可用（冷却已结束）状态。
- `double CooldownTime { get; }` — 获取冷却时长（秒）。
- `double Remaining { get; }` — 获取剩余冷却时间（秒）。
- `AbilityCooldown Cooldown { get; }` — 获取底层冷却对象。
- `void TriggerCooldown(double cooldown)` — 触发一次冷却。

#### `IKeybind`

`public interface IKeybind`

源码：`src/Astra.Framework/Abilities/IKeybind.cs`

> 表示一个通过按键触发的能力。

- `int SettingId { get; }` — 获取该能力绑定的服务器专属设置 SettingId（对应 SSKeybindSetting）。
- `KeybindType KeybindType { get; }` — 获取按键触发类型（按下 / 按住）。
- `bool SyncIsPressed { get; set; }` — 获取或设置按键当前是否被按下。
- `void KeyDown()` — 按键按下时调用。
- `void KeyUp()` — 按键松开时调用。

#### `IPlayerCondition`

`public interface IPlayerCondition`

源码：`src/Astra.Framework/Abilities/ICondition.cs`

> 表示一个带有玩家触发条件的能力。

- `Func<Player, bool> Condition { get; }` — 获取用于判断能力是否可用的玩家条件委托。

#### `StandardAbility`

`public abstract class StandardAbility : CustomAbility, IKeybind, ICooldown, IPlayerCondition`

源码：`src/Astra.Framework/Abilities/StandardAbility.cs`

> 标准技能的抽象基类：在 CustomAbility 基础上集成按键（IKeybind）、 冷却（ICooldown）与玩家条件（IPlayerCondition）。

- `public virtual int SettingId { get; }` — 获取技能绑定的服务器专属设置 SettingId（对应 SSKeybindSetting）。 已降级为兜底值：正常情况下框架会在技能加入玩家时按「技能槽顺序」 自动分配并写入 AssignedSettingId，本声明值仅在 槽位耗尽（该玩家技能数超过 CustomAbilityManager.MaxAbilitySlots）时生效。 -1 表示该技能没有按键绑定，适用于仅提供元数据的占位技能（不参与槽位分配）。
- `public virtual Func<Player, bool> Condition { get; protected set; }` — 获取或设置触发条件（默认始终为 true）。
- `public virtual KeybindType KeybindType { get; }` — 获取按键触发类型（默认按下触发）。
- `public bool SyncIsPressed { get; set; }` — 获取或设置按键当前是否被按下。
- `public bool IsReady { get; }` — 获取技能是否就绪（冷却结束且条件满足）。
- `public double Remaining { get; }` — 获取剩余冷却时间（秒）。
- `public AbilityCooldown Cooldown { get; }` — 获取底层冷却对象。
- `public virtual double CooldownTime { get; set; }` — 获取或设置冷却时长（秒）；默认值为 0（无冷却）。
- `public void KeyUp()` — 按键松开时调用。
- `protected virtual void OnKeyUp(Player player)` — 按键松开后的扩展钩子。
- `public void KeyDown()` — 按键按下时调用。
- `protected virtual void OnKeyDown(Player player)` — 按键按下后的扩展钩子。
- `protected virtual void OnTriggerFailed(Player player, AbilityFailReason failReason)` — 触发失败时的扩展钩子。
- `public virtual void TriggerCooldown(double cooldown)` — 触发一次冷却。
- `public virtual StringBuilder AbilityStringBuilder(StringBuilder stringBuilder)` — 将技能的显示状态写入 StringBuilder（形如 "[名称] 已就绪"）。


### `Astra.Attributes`

#### `DisableAutoRegisterAttribute`

`public class DisableAutoRegisterAttribute : Attribute`

特性：`[AttributeUsage(AttributeTargets.Class, Inherited = false)]`

源码：`src/Astra.Framework/Attributes/DisableAutoRegisterAttribute.cs`

> 标记一个组件「仅登记、不自动注册」。 应用于事件处理器类时：框架会实例化并记录它，但不会自动调用 CustomHandlersManager.RegisterEventsHandler，需之后通过 EventManager.RegisterHandler 手动启用。 应用于补丁类时：补丁仅被登记，不立即应用，需之后通过 PatchManager.Patch 手动启用。

（无公开成员）

#### `EventMethodAttribute`

`public class EventMethodAttribute : Attribute`

特性：`[AttributeUsage(AttributeTargets.Method | AttributeTargets.Delegate, AllowMultiple = true, Inherited = true)]`

源码：`src/Astra.Framework/Attributes/EventMethodAttribute.cs`

> 将标注的方法（或委托）自动订阅到指定的 .NET 事件。 框架在自动注册阶段通过 DeclaredEvent 解析目标事件并完成订阅， 框架禁用时自动取消订阅。

- `public EventMethodAttribute(Type eventType, string eventName)` — 初始化事件订阅特性（静态事件）。

#### `ManagedEventAttribute`

`public class ManagedEventAttribute : Attribute`

特性：`[AttributeUsage(AttributeTargets.Class)]`

源码：`src/Astra.Framework/Attributes/ManagedEventAttribute.cs`

> 将事件处理器类纳入框架自动注册，并以指定的 Id 作为该处理器的标识。 主要用于未实现 IIdentifiable 的处理器类； 若类同时实现了 IIdentifiable，则以接口返回的 Id 优先。

- `public ManagedEventAttribute(string id)` — 初始化 ManagedEventAttribute 并指定事件处理器 Id。
- `public string Id { get; }` — 获取事件处理器的唯一标识。

#### `ManagedPatchAttribute`

`public class ManagedPatchAttribute : Attribute`

特性：`[AttributeUsage(AttributeTargets.Class, Inherited = false)]`

源码：`src/Astra.Framework/Attributes/ManagedPatchAttribute.cs`

> 标记一个类为框架托管的 Harmony 补丁。 需配合 HarmonyPatch（声明式）或 IManualPatch（手动式）使用， 由框架在自动注册阶段统一应用，并在框架禁用时统一卸载。

（无公开成员）

#### `PatchApiVersionAttribute`

`public class PatchApiVersionAttribute : Attribute`

特性：`[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]`

源码：`src/Astra.Framework/Attributes/PatchApiVersionAttribute.cs`

- `public Version ApiVersion { get; }`

#### `PatchGameVersionAttribute`

`public class PatchGameVersionAttribute : Attribute`

特性：`[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]`

源码：`src/Astra.Framework/Attributes/PatchGameVersionAttribute.cs`

- `public string GameVersion { get; }`
- `public int MajorVersion { get; }`
- `public int MinorVersion { get; }`
- `public int RevisionVersion { get; }`
- `public string VersionSuffix { get; }`

#### `PatchMethodAttribute`

`public sealed class PatchMethodAttribute : Attribute`

特性：`[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]`

源码：`src/Astra.Framework/Attributes/PatchMethodAttribute.cs`

> 标记一个**静态方法**为框架托管的方法级 Harmony 补丁。

- `public PatchMethodAttribute(Type targetType, string targetMethodName) : this(targetType, targetMethodName, MethodType.Normal, Type.EmptyTypes)` — 按类型与成员名声明目标（无参数）。
- `public PatchMethodAttribute(Type targetType, string targetMethodName, params Type[] parameterTypes) : this(targetType, targetMethodName, MethodType.Normal, parameterTypes ?? Type.EmptyTypes)` — 按类型、成员名与参数类型声明目标。
- `public PatchMethodAttribute(Type targetType, string targetMethodName, MethodType methodType) : this(targetType, targetMethodName, methodType, Type.EmptyTypes)` — 按类型、成员名与成员种类声明目标。
- `public PatchMethodAttribute(Type targetType, MethodType methodType, params Type[] parameterTypes) : this(targetType, null, methodType, parameterTypes ?? Type.EmptyTypes)` — 按类型与成员种类声明目标。
- `public PatchMethodAttribute(Type targetType, params Type[] parameterTypes) : this(targetType, null, MethodType.Constructor, parameterTypes ?? Type.EmptyTypes)` — 按类型声明构造函数目标。
- `public PatchMethodAttribute(Type targetType) : this(targetType, null, MethodType.Constructor, Type.EmptyTypes)` — 按类型声明无参构造函数目标。
- `public PatchMethodAttribute(string typeName, string targetMethodName, MethodType methodType = MethodType.Normal) : this(FindType(typeName) ?? throw new ArgumentException($"找不到类型 {typeName}", nameof(typeName)), targetMethodName, methodType, Type.EmptyTypes)` — 按类型全名声明目标。
- `public Type TargetType { get; }` — 被补丁的类型。
- `public string? TargetMethodName { get; }` — 目标成员名；构造函数类目标为 null。
- `public MethodType MethodType { get; }` — 目标成员种类。
- `public Type[] ParameterTypes { get; }` — 目标方法参数类型。


### `Astra.Audio`

#### `AudioApi`

`public static class AudioApi`

源码：`src/Astra.Framework/Audio/AudioApi.cs`

> 音频播放对外静态 API，基于 SecretLabNAudio。

- `public static byte PlayAudio(Player player, string file, float volume = 1f)` — 向指定玩家播放音频。
- `public static byte PlayAudio(string file, float volume = 1f)` — 向所有玩家全局播放音频。
- `public static void Stop(Player player)` — 停止指定玩家正在播放的音频。
- `public static void Stop(byte id)` — 按 Id 停止全局音频。
- `public static void Stop(AudioPlayer player)` — 停止并归还指定的音频播放器到对象池。
- `public static void Stop()` — 停止所有全局音频。


### `Astra.Cancellation`

#### `CancellationTokenApi`

`public static class CancellationTokenApi`

源码：`src/Astra.Framework/Cancellation/CancellationTokenApi.cs`

> 取消令牌对外静态 API，内部委托给 TokenManager。

- `public static CancellationToken GetPlayersToken(Player player)` — 获取与指定玩家绑定的取消令牌，玩家离开时自动取消。
- `public static CancellationToken GetRoundRestartToken()` — 获取回合重启时取消的令牌。
- `public static CancellationToken GetFrameworkDisableToken()` — 获取框架禁用时取消的令牌。


### `Astra.Commands`

#### `CustomAbilityCommand`

`public sealed class CustomAbilityCommand : ICommand`

特性：`[CommandHandler(typeof(RemoteAdminCommandHandler))]`

源码：`src/Astra.Framework/Commands/CustomAbilityCommand.cs`

> 自定义技能（CustomAbility）的 RA 指令。

- `public string Command { get; }`
- `public string[] Aliases { get; }`
- `public string Description { get; }`
- `public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)`

#### `CustomItemCommand`

`public sealed class CustomItemCommand : ICommand`

特性：`[CommandHandler(typeof(RemoteAdminCommandHandler))]`

源码：`src/Astra.Framework/Commands/CustomItemCommand.cs`

> 自定义物品（CustomItem）的 RA 指令。

- `public string Command { get; }`
- `public string[] Aliases { get; }`
- `public string Description { get; }`
- `public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)`

#### `CustomRoleCommand`

`public sealed class CustomRoleCommand : ICommand`

特性：`[CommandHandler(typeof(RemoteAdminCommandHandler))]`

源码：`src/Astra.Framework/Commands/CustomRoleCommand.cs`

> 自定义角色（CustomRole）的 RA 指令。

- `public string Command { get; }`
- `public string[] Aliases { get; }`
- `public string Description { get; }`
- `public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)`

#### `CustomTeamCommand`

`public sealed class CustomTeamCommand : ICommand`

特性：`[CommandHandler(typeof(RemoteAdminCommandHandler))]`

源码：`src/Astra.Framework/Commands/CustomTeamCommand.cs`

> 自定义阵营（CustomTeam）的 RA 指令；阵营不归属玩家，因此不提供 get / give。

- `public string Command { get; }`
- `public string[] Aliases { get; }`
- `public string Description { get; }`
- `public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)`

#### `CustomWaveCommand`

`public sealed class CustomWaveCommand : ICommand`

特性：`[CommandHandler(typeof(RemoteAdminCommandHandler))]`

源码：`src/Astra.Framework/Commands/CustomWaveCommand.cs`

> 自定义波次（CustomWave）的 RA 指令；波次不归属玩家，因此不提供 get / give。

- `public string Command { get; }`
- `public string[] Aliases { get; }`
- `public string Description { get; }`
- `public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)`


### `Astra.Countdown`

#### `CountdownApi`

`public static class CountdownApi`

源码：`src/Astra.Framework/Countdown/CountdownApi.cs`

> 倒计时系统对外静态 API，内部委托给 CountdownManager 单例。

- `public static int Start(float delay, Action action, Action tickAction = null, Func<bool> repeat = null)` — 启动一个倒计时。
- `public static void Cancel(int uuid)` — 取消一个尚未到期的倒计时；已到期或被取消的标识调用无副作用。 供需要在效果中途提前收口的调用方使用（如锚点 / 迷雾区被提前销毁）。

#### `CountdownManager`

`public class CountdownManager : MonoSingleton<CountdownManager>`

源码：`src/Astra.Framework/Countdown/CountdownManager.cs`

> 计时器管理单例。 内部使用二叉最小堆（按触发时间排序）实现优先级队列， 使 AddTimer 为 O(log n)、Update 处理到期计时器为 O(k log n)， 移除计时器通过惰性删除（Cancelled 标记）降为 O(1)。 TickAction 的生命周期**独立于「到期」**：计时器在堆中等待触发期间， 由 ProcessTicks 每 TickInterval 秒回调一次； 到期时执行 Action，再按 Repeat 决定是否重新入队。 因此计时器必须同时登记在最小堆（排序）与活动表（逐帧 tick）中。

- `public const float TickInterval = 1f`
- `public void Init()` — 初始化
- `public int AddTimer(float delay, Action action, Action tickAction = null, Func<bool> repeat = null)` — 添加计时器
- `public void RemoveTimer(int uuid)` — 移除计时器（惰性删除，O(1)）
- `public void ClearTimer()` — 清除计时器


### `Astra.Effects`

#### `CustomEffect`

`public abstract class CustomEffect`

源码：`src/Astra.Framework/Effects/CustomEffect.cs`

- `public CustomEffect(Player player)`
- `public Player Owner { get; }`
- `public byte Intensity { get; private set; }`
- `public float Duration { get; private set; }`
- `public float TimeLeft { get; private set; }`
- `public virtual byte MaxIntensity { get; }`
- `public virtual bool AllowEnabling { get; }`
- `public virtual bool CanStack { get; }` — 是否允许同类效果叠加层数；默认 false，保持原有行为。
- `public int StackCount { get; private set; }` — 当前叠加层数；未叠加时为 1，禁用时重置为 1。
- `public bool IsEnabled { get; }`
- `public void SetState(byte intensity, float duration = 0f, bool addDuration = false)`
- `public void ForceIntensity(byte value)`
- `public void ChangeDuration(float duration, bool addDuration)`
- `public void Update()`
- `public void DisableEffect()`
- `public virtual void OnInitialize()`
- `public virtual void OnDestroy()`
- `public virtual void Enabled()`
- `public virtual void Disabled()`
- `public virtual void OnEffectUpdate()`
- `public virtual void IntensityChanged(byte oldIntensity, byte newIntensity)`
- `public virtual void OnOwnerDeath()`
- `public virtual void OnOwnerRoleChanged()`

#### `CustomEffectApi`

`public static class CustomEffectApi`

源码：`src/Astra.Framework/Effects/CustomEffectApi.cs`

> 自定义效果对外 API：效果施加/移除与控制器访问。

- `public static void Apply(Player player, IReadOnlyList<EffectEntry>? effects)` — 施加一组效果条目。
- `public static void Remove(Player player, IReadOnlyList<EffectEntry>? effects)` — 移除一组效果条目。
- `public static bool TryGetController(Player player, out CustomEffectController controller)` — 取玩家的自定义效果控制器。
- `public static T? GetOrAddEffect<T>(Player player, Func<Player, T> factory) where T : CustomEffect` — 取或添加一个自定义效果实例。
- `public static void Disable(Player player, Type effectType)` — 禁用一个自定义效果。
- `public static bool IsAvailable(EffectType effect)` — 查询原版效果是否可用（版本兼容）。

#### `CustomEffectController`

`public class CustomEffectController : MonoBehaviour`

源码：`src/Astra.Framework/Effects/CustomEffectController.cs`

> 承载玩家级"纯托管"自定义效果（CustomEffect 家族）的组件。 效果不进入 PlayerEffectsController.AllEffects，因此不改变原版按索引对齐的强度同步契约。

- `public Player? Owner { get; private set; }` — 效果宿主玩家；未 Bind 前为 null。
- `public void Bind(Player player)` — 绑定宿主玩家并重建默认效果集合。由 ComponentApplyPatch 在 ReferenceHub.Awake 之后调用。
- `public void AddEffect(CustomEffect effect)` — 向控制器追加一个效果实例（已存在则忽略）。
- `public T AddEffect<T>(T effect) where T : CustomEffect` — 追加效果实例并返回它，便于链式使用。
- `public CustomEffect? GetEffect(Type effectType)` — 按类型取得效果实例（供统一包装类使用）。
- `public void DisableEffect(Type effectType)` — 按类型停用效果实例，实例保留以便复用。
- `public T? GetOrAddEffect<T>(Func<Player, T> factory) where T : CustomEffect` — 取得指定类型的效果实例，不存在时用工厂创建并挂载。
- `public T? GetEffect<T>() where T : CustomEffect` — 取得指定类型的首个效果实例。
- `public bool TryGetEffect<T>(out T? effect) where T : CustomEffect` — 尝试取得指定类型的首个效果实例。
- `public T? ChangeState<T>(byte intensity, float duration = 0f, bool addDuration = false) where T : CustomEffect` — 修改指定类型效果的强度与时长。
- `public void DisableAllEffects()` — 关闭全部效果（含视觉载体清理）。
- `public void Cleanup()` — 释放全部效果并解除事件订阅（可重复调用）。

#### `CustomEffectHandler`

`public class CustomEffectHandler : CustomEventsHandler`

特性：`[ManagedEvent("Astra - CustomEffectHandler")]`

源码：`src/Astra.Framework/Effects/CustomEffectHandler.cs`

> 玩家离开时清理其身上的自定义效果，避免宿主对象销毁后效果实例残留。

- `public override void OnPlayerLeft(PlayerLeftEventArgs ev)`

#### `Effect`

`public readonly struct Effect : IEquatable<Effect>`

源码：`src/Astra.Framework/Effects/Effect.cs`

> 玩家身上一个状态效果的轻量视图。

- `public Effect(StatusEffectBase effect)` — 包装一个游戏效果实例。
- `public StatusEffectBase Base { get; }` — 底层游戏对象。
- `public EffectType Type { get; }` — 对应的效果枚举；未登记时为 None。
- `public float Duration { get; set; }` — 剩余持续时间（秒）。
- `public byte Intensity { get; set; }` — 强度。
- `public bool IsEnabled { get; set; }` — 是否处于启用状态。
- `public bool Equals(Effect other)`
- `public override bool Equals(object? obj)`
- `public override int GetHashCode()`

#### `EffectEntry`

`public readonly struct EffectEntry`

源码：`src/Astra.Framework/Effects/EffectEntry.cs`

> 统一的「效果条目」：原版状态效果与自定义效果共用同一种声明方式。

- `public bool IsCustom { get; }` — 是否为自定义效果。
- `public EffectType VanillaType { get; }` — 原版效果枚举；自定义效果时为 None。
- `public Type? CustomType { get; }` — 自定义效果类型；原版效果时为 null。
- `public byte Intensity { get; }` — 强度。
- `public float Duration { get; }` — 持续时间（秒），0 表示沿用效果默认值。
- `public bool AddDuration { get; }` — 是否在剩余时长上累加。
- `public static EffectEntry Vanilla(EffectType type, byte intensity = 1, float duration = 0f, bool addDuration = false)` — 声明一个原版状态效果。
- `public static EffectEntry Custom<T>(byte intensity = 1, float duration = 0f, bool addDuration = false) where T : CustomEffect` — 声明一个自定义效果；要求目标类型具备 (Player) 公开构造函数。
- `public static EffectEntry Custom<T>(Func<Player, T> factory, byte intensity = 1, float duration = 0f, bool addDuration = false) where T : CustomEffect` — 用显式工厂声明一个自定义效果，适合构造函数带额外参数的场景。
- `public void Apply(Player player)` — 对指定玩家施加该效果。
- `public void Remove(Player player)` — 从指定玩家身上解除该效果。
- `public override string ToString()`

#### `IEffects`

`public interface IEffects`

源码：`src/Astra.Framework/Effects/IEffects.cs`

- `EffectEntry[] Effects { get; }`


### `Astra.Enums`

#### `AbilityFailReason`

`public enum AbilityFailReason`

源码：`src/Astra.Framework/Enums/AbilityFailReason.cs`

> 能力触发失败的原因。

- `None` — 无失败。
- `ConditionFail` — 玩家条件不满足。
- `NotReady` — 冷却未结束。

#### `CancellationTokenCategory`

`public enum CancellationTokenCategory`

源码：`src/Astra.Framework/Enums/CancellationTokenCategory.cs`

> 取消令牌（CancellationToken）的类别。

- `FrameworkDisabled` — 框架禁用时取消。
- `RoundRestarted` — 回合重启时取消。

#### `EffectClassification`

`public enum EffectClassification`

源码：`src/Astra.Framework/Enums/EffectClassification.cs`

- `Technical`
- `Negative`
- `Mixed`
- `Positive`

#### `EffectType`

`public enum EffectType`

源码：`src/Astra.Framework/Enums/EffectType.cs`

> 玩家状态效果的类型标识。

- `None` — 无效果。
- `AmnesiaItems`
- `AmnesiaVision`
- `Asphyxiated`
- `Bleeding`
- `Blinded`
- `Burned`
- `Concussed`
- `Corroding`
- `Deafened`
- `Decontaminating`
- `Disabled`
- `Ensnared`
- `Exhausted`
- `Flashed`
- `Hemorrhage`
- `Invigorated`
- `BodyshotReduction`
- `Poisoned`
- `Scp207`
- `Invisible`
- `SinkHole`
- `DamageReduction`
- `MovementBoost`
- `RainbowTaste`
- `SeveredHands`
- `Stained`
- `Vitality`
- `Hypothermia`
- `Scp1853`
- `CardiacArrest`
- `InsufficientLighting`
- `SoundtrackMute`
- `SpawnProtected`
- `Traumatized`
- `AntiScp207`
- `Scanned`
- `PocketCorroding`
- `SilentWalk`
- `Marshmallow`
- `Strangled`
- `Ghostly`
- `FogControl`
- `Slowness`
- `Scp1344`
- `SeveredEyes`
- `PitDeath`
- `Blurred`
- `BecomingFlamingo`
- `Scp559`
- `Scp956Target`
- `Snowed`
- `Scp1344Detected`
- `Lightweight`
- `HeavyFooted`
- `Fade`
- `NightVision`
- `Scp1509Resurrected`
- `TraumatizedEvil`
- `SlowMetabolism`
- `TemporaryBypass`
- `Spicy`
- `SugarCrave`
- `OrangeWitness`
- `Metal`
- `SugarHigh`
- `SugarRush`
- `OrangeCandy`
- `WhiteCandy`
- `Prismatic`
- `AnomalousTarget`
- `FocusedVision`
- `AnomalousRegeneration`

#### `HintPosition`

`public enum HintPosition : byte`

源码：`src/Astra.Framework/Enums/HintPosition.cs`

> 提示（Hint）在屏幕上的显示位置。

- `Center` — 屏幕中央（垂直居中区域）；适合醒目的单条通知。
- `Left` — 屏幕左侧、垂直居中区域；适合侧边状态栏。
- `Right` — 屏幕右侧、垂直居中区域；适合侧边状态栏。
- `CenterTop` — 中上部；框架默认位置，适合标题类提示。
- `CenterTopLeft` — 中上偏左；适合左上区域的状态信息。
- `CenterTopRight` — 中上偏右；适合右上区域的状态信息。
- `CenterBottom` — 中下部；适合副标题或补充说明。
- `CenterBottomLeft` — 中下偏左。
- `CenterBottomRight` — 中下偏右。
- `Top` — 顶部（贴屏幕上沿）；适合常驻状态栏。
- `TopLeft` — 顶部偏左。
- `TopRight` — 顶部偏右。
- `Bottom` — 底部（贴屏幕下沿）；适合操作提示与倒计时。
- `BottomLeft` — 底部偏左。
- `BottomRight` — 底部偏右。
- `Custom` — 自定义位置占位值；当前映射回退到 Center 的坐标（650），不会自动生效。

#### `KeybindType`

`public enum KeybindType`

源码：`src/Astra.Framework/Enums/KeybindType.cs`

> 按键能力的触发类型。

- `Press` — 按下时触发。
- `Hold` — 按住式（松开时触发）。


### `Astra.Events`

#### `EnhancedEventsHandler`

`public abstract class EnhancedEventsHandler : CustomEventsHandler`

源码：`src/Astra.Framework/Events/EnhancedEventsHandler.cs`

> 增强事件管理器：继承 CustomEventsHandler 并额外提供框架扩展的射击事件虚方法。 继承该类并重写相应方法即可自动接收射击事件。

- `public EnhancedEventsHandler()`
- `public virtual void OnExiledShooting(ShootingEventArgs ev)` — 玩家开火前触发。
- `public virtual void OnExiledShot(ShotEventArgs ev)` — 子弹命中后触发。

#### `EventApi`

`public static class EventApi`

源码：`src/Astra.Framework/Events/EventApi.cs`

> 事件处理器对外 API：状态查询与启停。

- `public static IReadOnlyList<EventHandlerStatus> Handlers { get; }` — 全部已登记事件处理器的状态快照。
- `public static bool TryGetStatus(string id, out EventHandlerStatus status)` — 按 Id 查询处理器状态。
- `public static bool Enable(string id)` — 启用处理器。
- `public static bool Disable(string id)` — 禁用处理器。

#### `EventHandlerStatus`

`public sealed class EventHandlerStatus`

源码：`src/Astra.Framework/Events/EventManager.cs`

- `public string Id { get; }`
- `public Type HandlerType { get; }`
- `public string TypeFullName { get; }`
- `public bool IsEnabled { get; }`

#### `EventMethodManager`

`public static class EventMethodManager`

源码：`src/Astra.Framework/Events/EventMethodManager.cs`

> 事件方法管理器：负责将带 EventMethodAttribute 的方法订阅到对应 .NET 事件， 并在框架禁用时统一取消订阅。

- `public static void Register(object? instance)` — 扫描实例的公开方法，把带 EventMethodAttribute 的实例方法订阅到其声明的事件。 静态方法不在此处理，统一由 AutoRegister 按类型扫描注册。
- `public static void Register(MethodInfo methodInfo, object? instance = null)` — 将指定方法订阅到其 EventMethodAttribute 声明的所有事件。
- `public static void Unregister(MethodInfo methodInfo, [CanBeNull] EventInfo eventInfo = null)` — 取消订阅指定方法（可选按事件过滤）。
- `public static void UnregisterAll()` — 取消订阅所有已登记的事件方法。

#### `PlayerEvents`

`public static class PlayerEvents`

源码：`src/Astra.Framework/Events/PlayerEvents.cs`

> 框架扩展的玩家事件集合（射击相关）。 事件由 IL 织入的补丁触发，用法与 LabAPI 静态事件一致。

- `public static event LabEventHandler<ShootingEventArgs> Shooting`
- `public static event LabEventHandler<ShotEventArgs> Shot`
- `public static void OnShooting(ShootingEventArgs ev)` — 触发 Shooting 事件（框架内部调用）。
- `public static void OnShot(ShotEventArgs ev)` — 触发 Shot 事件（框架内部调用）。

#### `ShootingEventArgs`

`public class ShootingEventArgs : System.EventArgs, IPlayerEvent, IFirearmItemEvent, IItemEvent, ICancellableEvent`

源码：`src/Astra.Framework/Events/EventArgs/ShootingEventArgs.cs`

> Contains all information before a player fires a weapon. ClaimedTarget and Player transform values are modified by FpcBacktracker according to ShotBacktrackData sent by the Player and do not match the actual values.

- `public ShootingEventArgs(BaseFirearm firearm, ref ShotBacktrackData shotBacktrackData)` — Initializes a new instance of the ShootingEventArgs class.
- `public Player Player { get; }` — Gets the player who is shooting.
- `public Player ClaimedTarget { get; }` — Gets the target that client claims it hit.
- `public ShotBacktrackData ShotBacktrackData { get; }` — Gets the ShotBacktrackData. This object contains the data sent by the client to the server.
- `public Vector3 Direction { get; set; }` — Gets or sets the exact direction of the shot before the bullet spread is applied.
- `public FirearmItem FirearmItem { get; }` — Gets the firearm that is being fired.
- `public Item Item { get; }`
- `public bool IsAllowed { get; set; }` — Gets or sets a value indicating whether the shot can be fired.

#### `ShotEventArgs`

`public class ShotEventArgs : System.EventArgs, IPlayerEvent, IFirearmItemEvent, IItemEvent`

源码：`src/Astra.Framework/Events/EventArgs/ShotEventArgs.cs`

> Contains all information after a player has fired a weapon.

- `public ShotEventArgs(HitscanHitregModuleBase hitregModule, RaycastHit hitInfo, Firearm firearm, IDestructible destructible, float damage)` — Initializes a new instance of the ShotEventArgs class.
- `public Player Player { get; }` — Gets the player who fired the shot.
- `public FirearmItem FirearmItem { get; }` — Gets the firearm used to fire the shot.
- `public Item Item { get; }`
- `public HitscanHitregModuleBase HitregModule { get; }` — Gets the firearm hitreg module responsible for the shot.
- `public RaycastHit RaycastHit { get; }` — Gets the raycast info.
- `public float Distance { get; }` — Gets the bullet travel distance.
- `public Vector3 Position { get; }` — Gets the position of the hit.
- `public float Damage { get; }` — Gets the firearm base damage at the hit distance. Actual inflicted damage may vary.
- `public Player Target { get; }` — Gets the target player. Can be null.
- `public HitboxIdentity Hitbox { get; }` — Gets the HitboxIdentity component of the target player that was hit. Can be null.
- `public IDestructible Destructible { get; }` — Gets the IDestructible component of the hit collider. Can be null.
- `public bool CanHurt { get; set; }` — Gets or sets a value indicating whether the shot can deal damage.
- `public bool CanSpawnImpactEffects { get; set; }` — Gets or sets a value indicating whether the shot can produce impact effects (e.g. bullet holes).


### `Astra.Extensions`

#### `CollectionExtensions`

`public static class CollectionExtensions`

源码：`src/Astra.Framework/Extensions/CollectionExtensions.cs`

> 集合随机取值扩展。

- `public static T? GetRandom<T>(this IReadOnlyList<T> list)` — 从只读列表随机取一个元素。
- `public static T? GetRandom<T>(this IEnumerable<T> enumerable)` — 从可枚举序列随机取一个元素；非集合序列会先物化为数组。
- `public static T? GetRandom<T>(this T[] array)` — 从数组随机取一个元素。

#### `MapExtensions`

`public static class MapExtensions`

源码：`src/Astra.Framework/Extensions/MapExtensions.cs`

> 地图环境音相关的扩展方法。

- `public static AmbientSoundPlayer AmbientSoundPlayer { get; }` — 获取主机（Host）的 AmbientSoundPlayer 组件。
- `public static void PlayAmbientSound()` — Plays a random ambient sound.
- `public static void PlayAmbientSound(int id)` — Plays an ambient sound.

#### `PlayerEffectExtensions`

`public static class PlayerEffectExtensions`

源码：`src/Astra.Framework/Extensions/PlayerEffectExtensions.cs`

> 以 EffectType 为入口的玩家状态效果操作。

- `public static StatusEffectBase? FindEffect(this Player player, EffectType effect)` — 在玩家身上定位指定效果实例。
- `public static void EnableEffect(this Player player, EffectType effect, byte intensity = 1, float duration = 0f, bool addDuration = false)` — 启用（或刷新）一个效果。
- `public static void DisableEffect(this Player player, EffectType effect)` — 关闭一个效果。
- `public static bool TryGetEffect(this Player player, EffectType effect, out StatusEffectBase? instance)` — 尝试取得玩家的指定效果实例。
- `public static Effect? GetEffect(this Player player, EffectType effect)` — 取得玩家的指定效果视图。
- `public static IReadOnlyList<Effect> GetEffects(this Player player)` — 取得玩家当前所有生效中的效果视图。
- `public static byte GetEffectIntensity(this Player player, EffectType effect)` — 读取效果强度。
- `public static void ChangeEffectIntensity(this Player player, EffectType effect, byte intensity, float duration = 0f)` — 修改效果强度；duration 大于 0 时同时续期。

#### `PlayerExtensions`

`public static class PlayerExtensions`

源码：`src/Astra.Framework/Extensions/PlayerExtensions.cs`

> 针对 Player 的技能相关扩展方法。

- `public static bool TryGetAbilityManager(this Player player, out CustomAbilityManager manager)` — 获取玩家的技能管理器。
- `public static bool TryGetPlayerAbility<T>(this Player player, out T ability) where T : CustomAbility, new()` — 按泛型类型获取玩家的技能实例。
- `public static bool TryGetPlayerAbility(this Player player, string id, out CustomAbility ability)` — 按 Id 获取玩家的技能实例。
- `public static bool TryGetPlayerAbility(this Player player, int settingId, out CustomAbility ability)` — 按 SettingId 获取玩家的技能实例。


### `Astra.Hints`

#### `HintApi`

`public static class HintApi`

源码：`src/Astra.Framework/Hints/HintApi.cs`

> 对外暴露的静态提示 API，内部委托给 HintManager 单例。

- `public static void SendTo(Player player, string message, float duration = 3f, HintPosition position = HintPosition.CenterTop, int fontSize = 20, string messageId = null)` — 向指定玩家发送普通静态提示。
- `public static void SendDynamic(Player player, string message, float duration = 3f, HintPosition position = HintPosition.CenterTop, int fontSize = 20, string messageId = null)` — 向指定玩家发送动态提示。
- `public static void SendDynamic(Player player, Func<string> textGenerator, float duration = 3f, HintPosition position = HintPosition.CenterTop, int fontSize = 20, string messageId = null)` — 向指定玩家发送动态提示。
- `public static void SendDynamic(Player player, Func<AutoContentUpdateArg, string> textGenerator, float duration = 3f, HintPosition position = HintPosition.CenterTop, int fontSize = 20, string messageId = null)` — 向指定玩家发送动态提示。
- `public static void SendTo(Player player, Func<string> textGenerator, float duration = 3f, HintPosition position = HintPosition.CenterTop, int fontSize = 20, string messageId = null, HintSyncSpeed syncSpeed = HintSyncSpeed.Normal)` — 向指定玩家发送自动文本提示（支持动态更新文本）。
- `public static void SendTo(Player player, Func<AutoContentUpdateArg, string> textGenerator, float duration = 3f, HintPosition position = HintPosition.CenterTop, int fontSize = 20, string messageId = null, HintSyncSpeed syncSpeed = HintSyncSpeed.Normal)` — 向指定玩家发送自动文本提示（支持动态更新文本）。
- `public static void SendAbstractHint(Player player, AbstractHint hint, float duration)` — 向指定玩家直接发送Hint类。
- `public static void RemoveHint(Player player, string messageId)` — 移除指定玩家身上、由本框架发送的、带指定标识的提示。

#### `HintManager`

`public static class HintManager`

源码：`src/Astra.Framework/Hints/HintManager.cs`

> 统一管理游戏中各种提示（Hint）的发送与生命周期。

- `public static void SendTo(Player player, string message, float duration = 3f, HintPosition position = HintPosition.CenterTop, int fontSize = 20, string messageId = null)` — 发送普通静态提示。
- `public static void SendDynamic(Player player, string message, float duration = 3f, HintPosition position = HintPosition.CenterTop, int fontSize = 20, [CanBeNull] string messageId = null)` — 发送动态提示（DynamicHint）。
- `public static void SendDynamic(Player player, Func<string> textGenerator, float duration = 3f, HintPosition position = HintPosition.CenterTop, int fontSize = 20, [CanBeNull] string messageId = null)` — 发送动态提示（DynamicHint）。
- `public static void SendDynamic(Player player, Func<AutoContentUpdateArg, string> textGenerator, float duration = 3f, HintPosition position = HintPosition.CenterTop, int fontSize = 20, [CanBeNull] string messageId = null)` — 发送动态提示（DynamicHint）。
- `public static void SendTo(Player player, Func<string> textGenerator, float duration = 3f, HintPosition position = HintPosition.CenterTop, int fontSize = 20, string messageId = null, HintSyncSpeed syncSpeed = HintSyncSpeed.Normal)` — 发送自动文本提示（AutoText），可根据委托动态更新文本。
- `public static void SendTo(Player player, Func<AutoContentUpdateArg, string> textGenerator, float duration = 3f, HintPosition position = HintPosition.CenterTop, int fontSize = 20, string messageId = null, HintSyncSpeed syncSpeed = HintSyncSpeed.Normal)` — 发送自动文本提示（AutoText），可根据委托动态更新文本。
- `public static void SendAbstractHint(Player player, AbstractHint hint, float duration)` — 核心发送逻辑：移除重复提示，添加新提示，并在 duration 后自动移除。
- `public static void RemoveHint(Player player, string messageId)` — 移除指定玩家身上由本程序集发送的、带指定标识的提示。

#### `HintPositionMapping`

`public static class HintPositionMapping`

源码：`src/Astra.Framework/Hints/HintPositionMapping.cs`

> 将 HintPosition 映射为 HintServiceMeow 的对齐方式与坐标。

- `public static (HintAlignment alignment, float y) GetForHint(HintPosition position)` — 获取静态提示的对齐方式与 Y 坐标；未知位置回退为居中（Center, 650）。
- `public static (float x, float y) GetForDynamicHint(HintPosition position, float originXPos, float textWidth)` — 获取动态提示的 X/Y 坐标（根据对齐方式与文本宽度计算 X）。


### `Astra.HumeShield`

#### `CustomHumeShieldApi`

`public static class CustomHumeShieldApi`

源码：`src/Astra.Framework/HumeShield/CustomHumeShieldApi.cs`

> 自定义护盾对外 API：护盾驱动器的挂载与移除。

- `public static void Apply(Player player, ICustomHumeShieldStat stat)` — 给玩家挂载自定义护盾驱动器。
- `public static void Remove(Player player)` — 移除玩家的自定义护盾驱动器（护盾清零并解除覆盖锁）。

#### `CustomHumeShieldEventHandler`

`public class CustomHumeShieldEventHandler : CustomEventsHandler`

特性：`[ManagedEvent("Astra - CustomHumeShieldStat")]`

源码：`src/Astra.Framework/HumeShield/CustomHumeShieldEventHandler.cs`

- `public override void OnPlayerChangingRole(PlayerChangingRoleEventArgs ev)`

#### `ICustomHumeShieldBlocker`

`public interface ICustomHumeShieldBlocker`

源码：`src/Astra.Framework/HumeShield/ICustomHumeShieldBlocker.cs`

> 可临时阻断休谟护盾恢复的来源（技能、区域、状态）。

- `bool HumeShieldBlocked { get; }` — 当前是否正在阻断恢复。

#### `ICustomHumeShieldStat`

`public interface ICustomHumeShieldStat`

源码：`src/Astra.Framework/HumeShield/ICustomHumeShieldStat.cs`

> 自定义角色的休谟护盾参数。

- `bool IsDestroyed { get; }` — 角色是否已销毁。销毁后护盾立即收口。
- `float MaxHumeShield { get; }` — 护盾上限。仅在 HumeShieldCurve 为 null 时生效。
- `AnimationCurve HumeShieldCurve { get; }` — 上限随血量比例的曲线，复刻原版 ShieldOverHealth。
- `float HumeShieldRegenRate { get; }` — 每秒恢复量。
- `float HumeShieldRegenDelay { get; }` — 受伤后延迟多少秒才开始恢复。


### `Astra.Items`

#### `CustomFirearm`

`public abstract class CustomFirearm : CustomItem`

源码：`src/Astra.Framework/Items/CustomFirearm.cs`

> 自定义枪械：在 CustomItem 之上补充射击数值，以及开火 / 换弹 / 空枪 / 瞄准 / 命中结算等扩展点。

- `protected CustomFirearm(ItemType baseItem)` — 构造：指定承载的原版枪械类型。
- `public override ItemType BaseItem { get; }`
- `public virtual float Damage { get; }` — 单发伤害。
- `public virtual float FireRate { get; }` — 射速（发/分钟）。
- `public virtual int MagazineSize { get; }` — 弹匣容量；-1 表示沿用原版。
- `public virtual float ReloadTime { get; }` — 换弹时间（秒）；-1 表示沿用原版。
- `public virtual int Penetration { get; }` — 穿透力（可穿透的目标数）。
- `public virtual float Spread { get; }` — 扩散角（度）。
- `public float FireInterval { get; }` — 射击间隔（秒），由 FireRate 推导。
- `public virtual bool OnReloading(Player player, FirearmItem firearm)` — 换弹开始时调用；返回 false 阻止本次换弹。
- `public virtual void OnReloaded(Player player, FirearmItem firearm)` — 换弹完成时调用。
- `public virtual bool OnDryFiring(Player player, FirearmItem firearm)` — 空仓扣扳机时调用；返回 false 阻止原版空枪反馈。
- `public virtual void OnAiming(Player player, FirearmItem firearm)` — 玩家举枪瞄准时调用。
- `public virtual void OnShot(ShotEventArgs ev)` — 子弹命中结算时调用，可修改 CanHurt 等字段或自行结算伤害。

#### `CustomItem`

`public abstract class CustomItem : ICustomContent, ICallOnRegister, ICallOnUnregister`

源码：`src/Astra.Framework/Items/CustomItem.cs`

> 自定义物品基类：以一个原版 ItemType 为承载体，附加自定义行为与数值。

- `public abstract string Id { get; }`
- `public abstract string Name { get; }`
- `public abstract ItemType BaseItem { get; }` — 承载本自定义物品的原版物品类型。
- `public virtual string Description { get; }` — 物品描述，默认空。
- `public virtual float Weight { get; }` — 物品重量；-1 表示使用原版默认值。
- `public virtual bool OnShooting(ShootingEventArgs ev)` — 射击事件（仅对枪械类物品有意义）：返回 false 阻止原版射击。
- `public virtual void OnAcquired(Player player, Item item)` — 物品被玩家获得时调用。
- `public virtual void OnEquipped(Player player, Item item)` — 物品被装备（切换到手持）时调用。
- `public virtual void OnHolstered(Player player, Item item)` — 物品被收起（从手持切走）时调用。
- `public virtual void OnPickedUp(Player player, Item item)` — 物品被拾取时调用。
- `public virtual void OnDropped(Player player, Pickup pickup)` — 物品被丢弃时调用。
- `public virtual bool OnUsing(Player player, UsableItem item)` — 物品被使用前调用；返回 false 阻止本次使用。
- `public virtual void OnUsed(Player player, UsableItem item)` — 物品使用完成后调用。
- `public virtual bool OnThrowing(Player player, Pickup pickup)` — 物品被投掷前调用；返回 false 阻止本次投掷。
- `public virtual Item? Give(Player player)` — 给玩家发放本物品。
- `protected virtual Item? CreateItem(Player player)` — 创建待发放的物品实例；默认发放 BaseItem。
- `public virtual void OnRegisterEvents()` — 物品定义被框架登记后调用；默认空实现，可覆写做定义级初始化。
- `public virtual void OnUnregisterEvents()` — 物品定义从框架注销前调用；默认空实现，可覆写清理定义级资源。

#### `CustomItemApi`

`public static class CustomItemApi`

源码：`src/Astra.Framework/Items/CustomItemApi.cs`

> 自定义物品对外 API：定义查询、发放与实例溯源。

- `public static IReadOnlyDictionary<string, CustomItem> Items { get; }` — 当前已实例化的自定义物品定义。
- `public static CustomItem? Find(string id)` — 按 Id 查找自定义物品定义。
- `public static T? Find<T>() where T : CustomItem` — 按类型查找自定义物品定义。
- `public static CustomItem? FindByItemType(ItemType itemType)` — 按原版物品类型查找自定义定义。
- `public static CustomItem? FindByItem(Item? item)` — 按物品实例查找其自定义定义。
- `public static Item? Give(Player player, string id)` — 按 Id 给玩家发放自定义物品。
- `public static Item? Give<T>(Player player) where T : CustomItem` — 按类型给玩家发放自定义物品。
- `public static void RegisterTemplate(Type type)` — 登记物品模板（框架自动扫描时调用，通常无需手动）。
- `public static bool Register(CustomItem item)` — 按实例注册自定义物品定义（外部框架 / JSON 适配器用）。
- `public static bool Register(CustomItem item, RegistrationScope scope)` — 在指定注册作用域内按实例注册自定义物品定义。
- `public static bool Unregister(string id)` — 按 Id 注销自定义物品定义。
- `public static void Initialize()` — 按模板实例化全部自定义物品（幂等，框架启动时自动调用）。

#### `CustomItemEventHandler`

`public class CustomItemEventHandler : EnhancedEventsHandler`

特性：`[ManagedEvent("Astra - CustomItem")]`

源码：`src/Astra.Framework/Items/CustomItemEventHandler.cs`

> 自定义物品的事件转发：把玩家与物品的交互交给对应的 CustomItem 处理。

- `public override void OnExiledShooting(ShootingEventArgs ev)` — 射击：仅当自定义物品覆写了 OnShooting 时转发，返回 false 则阻止原版开火。
- `public override void OnExiledShot(ShotEventArgs ev)` — 命中结算：转发给自定义枪械。
- `public override void OnPlayerChangedItem(PlayerChangedItemEventArgs ev)` — 切换手持物品：向旧物品转发收起、向新物品转发装备。
- `public override void OnPlayerPickedUpItem(PlayerPickedUpItemEventArgs ev)` — 拾取物品：转发给自定义物品。
- `public override void OnPlayerDroppedItem(PlayerDroppedItemEventArgs ev)` — 丢弃物品：转发给自定义物品。
- `public override void OnPlayerUsingItem(PlayerUsingItemEventArgs ev)` — 使用物品前：返回 false 可阻止本次使用。
- `public override void OnPlayerUsedItem(PlayerUsedItemEventArgs ev)` — 使用完成：转发给自定义物品。
- `public override void OnPlayerThrowingItem(PlayerThrowingItemEventArgs ev)` — 投掷前：返回 false 可阻止本次投掷。
- `public override void OnPlayerReloadingWeapon(PlayerReloadingWeaponEventArgs ev)` — 开始换弹：转发给自定义枪械，返回 false 可阻止。
- `public override void OnPlayerReloadedWeapon(PlayerReloadedWeaponEventArgs ev)` — 换弹完成：转发给自定义枪械。
- `public override void OnPlayerDryFiringWeapon(PlayerDryFiringWeaponEventArgs ev)` — 空仓扣扳机：转发给自定义枪械，返回 false 可阻止。
- `public override void OnPlayerAimedWeapon(PlayerAimedWeaponEventArgs ev)` — 举枪瞄准：转发给自定义枪械。

#### `CustomJailbird`

`public abstract class CustomJailbird : CustomItem`

源码：`src/Astra.Framework/Items/CustomJailbird.cs`

> 自定义 Jailbird：在 CustomItem 之上补充充能/放电参数。

- `protected CustomJailbird(ItemType baseItem)` — 构造：指定承载的原版物品类型。
- `public override ItemType BaseItem { get; }`
- `public virtual float Damage { get; }` — 单次攻击伤害。
- `public virtual float ChargeCost { get; }` — 每次使用消耗的电量百分比。
- `public virtual float RechargeRate { get; }` — 每秒回充百分比。

#### `CustomMicroHid`

`public abstract class CustomMicroHid : CustomItem`

源码：`src/Astra.Framework/Items/CustomMicroHid.cs`

> 自定义 Micro-HID：在 CustomItem 之上补充蓄能/放电参数。

- `protected CustomMicroHid(ItemType baseItem)` — 构造：指定承载的原版物品类型。
- `public override ItemType BaseItem { get; }`
- `public virtual float Damage { get; }` — 单次放电伤害。
- `public virtual float Range { get; }` — 有效射程（米）。
- `public virtual float ChargeRate { get; }` — 每秒蓄能百分比。
- `public float ChargeSeconds { get; }` — 蓄满所需秒数。

#### `CustomParticleDisruptor`

`public abstract class CustomParticleDisruptor : CustomItem`

源码：`src/Astra.Framework/Items/CustomParticleDisruptor.cs`

> 自定义 Particle Disruptor（轨道炮类）：蓄力、穿透与射线伤害。

- `protected CustomParticleDisruptor(ItemType baseItem)` — 构造：指定承载的原版物品类型。
- `public override ItemType BaseItem { get; }`
- `public virtual float ChargeTime { get; }` — 蓄力时间（秒）。
- `public virtual float Damage { get; }` — 单目标伤害。
- `public virtual float Range { get; }` — 射程（米）。
- `public virtual int PierceCount { get; }` — 可穿透的目标数量；0 表示命中即止。
- `public virtual float DamageFalloff { get; }` — 穿透时每层的伤害衰减系数（0~1）。
- `public virtual Color BeamColor { get; }` — 光束颜色（供客户端/后续表现层使用）。
- `public override bool OnShooting(ShootingEventArgs ev)` — 射击事件：阻止原版开火并立即结算自定义射线。
- `public virtual IReadOnlyList<Player> Fire(Player player)` — 结算一次穿透射线：从玩家视角沿朝向发射，按 PierceCount 穿透并逐层衰减。
- `public virtual void OnFired(Player player, IReadOnlyList<Player> hits)` — 射线结算完成后的回调。

#### `ItemEntry`

`public readonly struct ItemEntry : IEquatable<ItemEntry>`

源码：`src/Astra.Framework/Items/ItemEntry.cs`

> 物品条目：同时表达「原版物品类型」与「自定义物品定义」，供角色初始物品等场景统一声明。

- `public ItemType VanillaType { get; }` — 原版物品类型；自定义条目为其承载类型；未解析的 Id-only 条目为 None。
- `public CustomItem? CustomItem { get; }` — 自定义物品定义；原版条目或尚未注册时为 null。
- `public string? CustomItemId { get; }` — 自定义物品 Id；原版条目为 null。
- `public bool IsCustom { get; }` — 是否为自定义物品条目。
- `public static ItemEntry Vanilla(ItemType type)` — 声明一个原版物品条目。
- `public static ItemEntry Custom(CustomItem item)` — 用自定义物品定义声明条目。
- `public static ItemEntry Custom(string id)` — 用自定义物品 Id 声明条目；应用时再解析定义。
- `public bool Equals(ItemEntry other)`
- `public override bool Equals(object? obj)`
- `public override int GetHashCode()`
- `public override string ToString()`
- `public static implicit operator ItemEntry(ItemType type)` — 原版物品类型隐式转换为条目。
- `public static implicit operator ItemEntry(CustomItem item)` — 自定义物品定义隐式转换为条目。


### `Astra.Lifecycle`

#### `AstraLifecycle`

`public static class AstraLifecycle`

源码：`src/Astra.Framework/Lifecycle/AstraLifecycle.cs`

> Astra 注册项的生命周期入口：按插件程序集创建作用域，或直接撤销某个来源的注册。

- `public static RegistrationScope CreateScope(string pluginId, Assembly? assembly = null)` — 按插件 Id 与程序集创建注册作用域。
- `public static RegistrationScope CreateScope(Type anchorType, string? pluginId = null)` — 以锚点类型所在程序集创建注册作用域。
- `public static int UnregisterSource(Assembly assembly)` — 撤销指定程序集在 Astra 登记的全部可撤销内容。
- `public static int UnregisterSource(Type anchorType)` — 撤销锚点类型所在程序集的全部可撤销内容。
- `public static int UnregisterSource(RegistrationSource source)` — 按来源撤销登记项；来源带程序集时按程序集匹配。

#### `RegistrationScope`

`public sealed class RegistrationScope : IDisposable`

源码：`src/Astra.Framework/Lifecycle/RegistrationScope.cs`

> 一次性注册作用域：Dispose 时撤销该来源在 Astra 登记的全部可撤销内容。

- `public RegistrationSource Source { get; }` — 作用域对应的注册来源。
- `public bool IsDisposed { get; }` — 作用域是否已经释放。
- `public int Unregister()` — 立即撤销该来源的全部登记项。重复调用不会重复撤销。
- `public void Dispose()`

#### `RegistrationSource`

`public readonly struct RegistrationSource : IEquatable<RegistrationSource>`

源码：`src/Astra.Framework/Lifecycle/RegistrationSource.cs`

> 一次注册的来源标识。框架用它把注册内容与插件程序集关联起来，供按来源撤销使用。

- `public RegistrationSource(string? pluginId, Assembly? assembly)` — 创建一个来源标识。
- `public string PluginId { get; }` — 来源插件 Id；未显式提供时为空字符串。
- `public Assembly? Assembly { get; }` — 来源程序集；未能解析时为 null。
- `public bool IsValid { get; }` — 是否具备可撤销的来源（必须能解析到程序集）。
- `public static RegistrationSource FromAssembly(Assembly assembly, string? pluginId = null)` — 按程序集创建来源。
- `public static RegistrationSource FromType(Type type, string? pluginId = null)` — 按类型所在程序集创建来源。
- `public static RegistrationSource For(string? pluginId, Assembly? assembly = null)` — 按插件 Id 与可选程序集创建来源。
- `public bool Matches(Assembly? assembly)` — 判断来源是否指向指定程序集。
- `public bool Equals(RegistrationSource other)`
- `public override bool Equals(object? obj)`
- `public override int GetHashCode()`
- `public override string ToString()`


### `Astra.Loader`

#### `ICallOnRegister`

`public interface ICallOnRegister`

源码：`src/Astra.Framework/Loader/ICallOnRegister.cs`

> 表示组件在被框架登记时执行一次初始化回调。

- `void OnRegisterEvents()` — 在组件被注册到事件系统时调用。

#### `ICallOnUnregister`

`public interface ICallOnUnregister`

源码：`src/Astra.Framework/Loader/ICallOnUnregister.cs`

> 表示组件在被框架注销时执行一次清理回调。

- `void OnUnregisterEvents()` — 在组件从事件系统注销时调用。

#### `IdentifiableManager<T>`

`public class IdentifiableManager<T> where T : IIdentifiable`

源码：`src/Astra.Framework/Loader/IdentifiableManager.cs`

> 基于字符串 Id 的组件管理器，提供 IIdentifiable 组件的增删查。

- `protected readonly Dictionary<string, T> Items`
- `public void Add(T item)` — 添加组件，Id 不能为空。
- `public void Remove(string id)` — 按 Id 移除组件。
- `public T Get(string id)` — 按 Id 获取组件，不存在时返回默认值。
- `public IEnumerable<T> GetAll()` — 获取所有组件的快照。
- `public bool Contains(string id)` — 判断是否存在指定 Id 的组件。

#### `MonoSingleton<T>`

`public abstract class MonoSingleton<T> : MonoBehaviour where T : MonoSingleton<T>`

源码：`src/Astra.Framework/Loader/MonoSingleton.cs`

> Unity 懒加载单例基类：首次访问 Instance 时自动创建 GameObject 并 DontDestroyOnLoad， 保证场景切换时单例不被销毁。

- `protected GameObject GameObject { get; private set; }` — 宿主 GameObject。
- `public static T Instance { get; }` — 获取单例实例，不存在时自动创建。
- `protected virtual void Awake()` — Unity Awake：确保单例唯一，重复创建的实例会被销毁。
- `protected virtual void OnDestroy()` — Unity OnDestroy：单例销毁时清空静态引用。


### `Astra.Patching`

#### `IManualPatch`

`public interface IManualPatch`

源码：`src/Astra.Framework/Patching/IManualPatch.cs`

> 表示一个手动应用 Harmony 补丁的组件。 与声明式 [HarmonyPatch] 不同，实现该接口可在代码中自行决定如何打补丁。

- `void ApplyPatch(Harmony harmony)` — 应用补丁。

#### `PatchApi`

`public static class PatchApi`

源码：`src/Astra.Framework/Patching/PatchApi.cs`

> 托管补丁对外 API：状态查询与启停。

- `public static IReadOnlyList<PatchStatus> Patches { get; }` — 全部已登记补丁的状态快照。
- `public static bool TryGetStatus(string id, out PatchStatus status)` — 按 Id 查询补丁状态。
- `public static bool Enable(string id)` — 启用补丁。
- `public static bool Disable(string id)` — 禁用补丁。
- `public static void DisableAll()` — 卸载全部补丁。

#### `PatchStatus`

`public sealed class PatchStatus`

源码：`src/Astra.Framework/Patching/PatchManager.cs`

- `public string Id { get; }`
- `public Type PatchType { get; }`
- `public string TypeFullName { get; }`
- `public bool IsEnabled { get; }`


### `Astra.Positions`

#### `ISpawnOnRoundStart`

`public interface ISpawnOnRoundStart`

源码：`src/Astra.Framework/Positions/ISpawnOnRoundStart.cs`

> 表示可以在回合开始时于若干刷怪点生成的内容。

- `IReadOnlyDictionary<SpawnPointEntry, float> SpawnPoints { get; }` — 刷怪点与对应的生成概率。

#### `PositionEntry`

`public readonly struct PositionEntry`

源码：`src/Astra.Framework/Positions/PositionEntry.cs`

> 位置条目：表示精确世界坐标，或「房间筛选条件 + 房间内本地坐标」。

- `public RoomName RoomName { get; }` — 房间名称；具体房间条目为其实际名称，筛选条目为筛选值。
- `public FacilityZone FacilityZone { get; }` — 区域；具体房间条目为其实际区域，筛选条目为筛选值。
- `public RoomShape RoomShape { get; }` — 房间形状；具体房间条目为其实际形状，筛选条目为筛选值。
- `public Vector3 LocalPosition { get; }` — 房间内本地坐标；纯世界坐标条目为 zero。
- `public bool HasWorldPosition { get; }` — 是否持有精确世界坐标。
- `public Vector3 WorldPosition { get; }` — 解析后的世界坐标。房间条目会随机选择一个匹配房间；无法解析时返回 zero。
- `public IReadOnlyList<RoomIdentifier> Rooms { get; }` — 匹配的房间集合。具体房间条目只包含该房间；世界坐标条目只包含包含该点的房间。
- `public RoomIdentifier? Room { get; }` — 随机选择一个匹配房间；具体房间条目返回该房间。
- `public static PositionEntry Find(Vector3 worldPosition)` — 按世界坐标创建；若坐标位于房间内，会同时记录具体房间和本地坐标。
- `public static PositionEntry Find(RoomIdentifier room, Vector3 localPosition)` — 按具体房间 + 房间内本地坐标创建。
- `public static PositionEntry Find(RoomName roomName, Vector3 localPosition)` — 按房间名称 + 本地坐标创建；多个匹配房间时解析阶段随机选房。
- `public static PositionEntry Find(FacilityZone zone, Vector3 localPosition)` — 按区域 + 本地坐标创建；多个匹配房间时解析阶段随机选房。
- `public static PositionEntry Find(RoomShape shape, Vector3 localPosition)` — 按房间形状 + 本地坐标创建；多个匹配房间时解析阶段随机选房。
- `public static PositionEntry Find(RoomName? roomName, FacilityZone? zone, RoomShape? shape, Vector3 localPosition)` — 按组合筛选条件 + 本地坐标创建；未给出的条件视为不限，解析阶段随机选房。
- `public static PositionEntry Random(Vector3 localPosition = default)` — 创建「随机房间 + 本地坐标」条目。
- `public bool TryGetWorldPosition(out Vector3 position)` — 尝试解析世界坐标；筛选条目会随机选择一个匹配房间。

#### `SpawnPointEntry`

`public readonly struct SpawnPointEntry`

源码：`src/Astra.Framework/Positions/SpawnPointEntry.cs`

> 刷怪点条目：位置 + 可选旋转。

- `public SpawnPointEntry(PositionEntry position, Quaternion? rotation = null)` — 构造刷怪点条目。
- `public PositionEntry Position { get; }` — 位置条目。
- `public Quaternion? Rotation { get; }` — 显式旋转；未指定时为 null。
- `public bool HasRotation { get; }` — 是否显式指定了旋转。
- `public Vector3 WorldPosition { get; }` — 解析后的世界坐标；条件不明确时可能随机变化。
- `public bool TryResolve(out Vector3 position, out Quaternion rotation)` — 解析为具体的世界坐标与旋转。
- `public Quaternion ResolveRotation()` — 解析旋转；未指定时返回一个随机水平朝向。
- `public static SpawnPointEntry Find(Vector3 worldPosition, Quaternion? rotation = null)` — 按世界坐标创建刷怪点。
- `public static SpawnPointEntry Find(RoomIdentifier room, Vector3 localPosition, Quaternion? rotation = null)` — 按具体房间 + 本地坐标创建刷怪点。
- `public static SpawnPointEntry Find(RoomName roomName, Vector3 localPosition, Quaternion? rotation = null)` — 按房间名称 + 本地坐标创建刷怪点；多个匹配房间时解析阶段随机选择。
- `public static SpawnPointEntry Find(FacilityZone zone, Vector3 localPosition, Quaternion? rotation = null)` — 按区域 + 本地坐标创建刷怪点；多个匹配房间时解析阶段随机选择。
- `public static SpawnPointEntry Find(RoomShape shape, Vector3 localPosition, Quaternion? rotation = null)` — 按房间形状 + 本地坐标创建刷怪点；多个匹配房间时解析阶段随机选择。
- `public static SpawnPointEntry Find(RoomName? roomName, FacilityZone? zone, RoomShape? shape, Vector3 localPosition, Quaternion? rotation = null)` — 按组合筛选条件 + 本地坐标创建刷怪点；未给出的条件视为不限，解析阶段随机选房。
- `public static SpawnPointEntry Random(Vector3 localPosition = default, Quaternion? rotation = null)` — 创建「随机房间 + 本地坐标」刷怪点。


### `Astra.Roles`

#### `CustomRole`

`public abstract class CustomRole : CustomEventsHandler, ICustomContent, IOriginalRole, ICallOnRegister, ICallOnUnregister`

源码：`src/Astra.Framework/Roles/CustomRole.cs`

- `public abstract string Id { get; }`
- `public abstract string Name { get; }`
- `public virtual string Description { get; set; }`
- `public Player? Owner { get; private set; }` — 该实例绑定的玩家。
- `public virtual RoleTypeId RoleType { get; set; }`
- `public virtual RoleSpawnFlags SpawnFlags { get; }`
- `public virtual RoleChangeReason ChangeReason { get; }`
- `public virtual Vector3 Scale { get; }`
- `public virtual Vector3 Gravity { get; }`
- `public virtual bool ShowCustomInfo { get; set; }`
- `public virtual float MaxHealth { get; set; }`
- `public virtual IReadOnlyList<ItemEntry> Items { get; set; }` — 套用角色时发放的初始物品：可混用原版物品与自定义物品定义。
- `public virtual bool ClearInventory { get; set; }`
- `public bool IsDestroyed { get; private set; }`
- `public virtual string RoleColor { get; }` — 角色配色，由 RoleType 推导，只读。
- `public virtual TeamRef Team { get; }` — 角色所属阵营：自定义阵营优先，未声明时由 RoleType 推导原版阵营。
- `public bool Check(Player player)` — 判断指定玩家当前是否持有本角色（按 Id 比对）。
- `protected virtual void OnAdded(Player player)`
- `protected virtual void OnRemoved(Player player)`
- `public override void OnPlayerChangingRole(PlayerChangingRoleEventArgs ev)` — 角色变更前收口。
- `public virtual void OnRegisterEvents()` — 角色模板被框架登记后调用；默认空实现，可覆写做模板级初始化。
- `public virtual void OnUnregisterEvents()` — 角色模板从框架注销前调用；默认空实现，可覆写清理模板级资源。
- `public virtual CustomRole CreateInstance()`

#### `CustomRoleApi`

`public static class CustomRoleApi`

源码：`src/Astra.Framework/Roles/CustomRoleApi.cs`

> 自定义角色对外 API：模板查询与玩家角色增删。

- `public static IReadOnlyCollection<CustomRole> RoleTemplates { get; }` — 当前已注册的全部角色模板。
- `public static bool TryGetRoleTemplate(string id, out CustomRole role)` — 按 Id 获取角色模板。
- `public static bool TryGetRoleTemplate<T>(out T role) where T : CustomRole, new()` — 按类型获取角色模板。
- `public static bool Register(CustomRole template)` — 按实例注册角色模板（外部框架 / 动态定义用）。
- `public static bool Register(CustomRole template, RegistrationScope scope)` — 在指定注册作用域内按实例注册角色模板。
- `public static bool Unregister(string id)` — 按 Id 注销角色模板。
- `public static CustomRole? AddRole<T>(Player player) where T : CustomRole, new()` — 按泛型类型给玩家套用角色。
- `public static CustomRole? AddRole(Player player, string id)` — 按 Id 给玩家套用角色。
- `public static CustomRole? GetRole(Player player)` — 取玩家当前的自定义角色。
- `public static bool TryGetRole(Player player, out CustomRole role)` — 尝试取玩家当前的自定义角色。
- `public static void RemoveRole(Player player)` — 移除玩家当前的自定义角色。

#### `IOriginalRole`

`public interface IOriginalRole`

源码：`src/Astra.Framework/Roles/IOriginalRole.cs`

- `RoleTypeId RoleType { get; }`

#### `RoleEntry`

`public readonly struct RoleEntry : IEquatable<RoleEntry>`

源码：`src/Astra.Framework/Roles/RoleEntry.cs`

> 角色条目：同时表达「原版角色类型」与「自定义角色定义」，用于波次组成等需要混编的场景。

- `public RoleTypeId VanillaRole { get; }` — 原版角色类型；自定义条目为其 RoleType；未解析时为 None。
- `public CustomRole? CustomRole { get; }` — 自定义角色定义；原版条目或尚未注册时为 null。
- `public string? CustomRoleId { get; }` — 自定义角色 Id；原版条目为 null。
- `public bool IsCustom { get; }` — 是否为自定义角色条目。
- `public static RoleEntry Vanilla(RoleTypeId role)` — 声明一个原版角色条目。
- `public static RoleEntry Custom(CustomRole role)` — 用自定义角色定义声明条目。
- `public static RoleEntry Custom<T>() where T : CustomRole` — 用已注册的自定义角色模板类型声明条目。
- `public static RoleEntry Custom(string id)` — 用自定义角色 Id 声明条目；解析时再查模板注册表。
- `public bool Equals(RoleEntry other)`
- `public override bool Equals(object? obj)`
- `public override int GetHashCode()`
- `public override string ToString()`
- `public static implicit operator RoleEntry(RoleTypeId role)` — 原版角色隐式转换为条目。
- `public static implicit operator RoleEntry(CustomRole role)` — 自定义角色隐式转换为条目。

#### `StandardRole`

`public abstract class StandardRole : CustomRole, IAbilityRole, ICustomHumeShieldStat, IEffects`

源码：`src/Astra.Framework/Roles/StandardRole.cs`

- `public virtual CustomAbility[] Abilities { get; }`
- `public virtual float MaxHumeShield { get; set; }` — 护盾上限；HumeShieldCurve 非空时以曲线为准。
- `public virtual AnimationCurve HumeShieldCurve { get; set; }` — 护盾上限随血量比例的曲线（纵轴为护盾绝对值），复刻原版 ShieldOverHealth。
- `public virtual float HumeShieldRegenRate { get; set; }` — 每秒恢复量。
- `public virtual float HumeShieldRegenDelay { get; set; }` — 受伤后延迟多少秒开始恢复。
- `public virtual EffectEntry[] Effects { get; }` — 套用角色时一并施加的效果集合，原版效果与自定义效果可混用。


### `Astra.ServerSpecificSettings`

#### `ServerSpecificSettingBase`

`public abstract class ServerSpecificSettingBase : IIdentifiable`

源码：`src/Astra.Framework/ServerSpecificSettings/ServerSpecificSettingBase.cs`

> 服务器专属设置（ServerSpecificSettings）的抽象基类。 继承该类并按 Id 返回一组设置项，框架会自动统一下发给客户端。

- `public abstract string Id { get; }` — 获取该设置组的全局唯一标识。
- `public abstract UserSettings.ServerSpecific.ServerSpecificSettingBase[] ActiveSettings { get; }` — 获取该设置组包含的服务器专属设置项。
- `public virtual int Priority { get; }` — 获取设置组的排序优先级（数值越小越靠前，-1 表示排最后）。

#### `ServerSpecificSettingManager`

`public static class ServerSpecificSettingManager`

源码：`src/Astra.Framework/ServerSpecificSettings/ServerSpecificSettingManager.cs`

> 服务器专属设置管理器单例：负责设置组的登记、注销与统一下发。

- `public static void Register(string id)` — 按 Id 登记设置组。
- `public static void Unregister(string id)` — 按 Id 注销设置组。
- `public static void SendToAll()` — 将所有设置组按优先级排序、按 SettingId 去重后统一下发给客户端。


### `Astra.Teams`

#### `CustomTeam`

`public abstract class CustomTeam : IEquatable<CustomTeam>, ICustomContent, ICallOnRegister, ICallOnUnregister`

源码：`src/Astra.Framework/Teams/CustomTeam.cs`

> 自定义阵营：声明本阵营与「原版阵营 / 其它自定义阵营」是敌对还是友好。

- `public abstract string Id { get; }` — 阵营唯一标识。
- `public abstract string Name { get; }` — 阵营显示名。
- `public virtual Color TeamColor { get; }` — 阵营配色，默认白色。
- `public virtual CustomTeamRelation Relation { get; }` — 关系模式，默认 Custom。
- `public virtual bool KeepVanillaRelate { get; }` — 是否保留原版阵营关系（仅 Custom 下生效）。
- `public virtual Team VanillaTeam { get; }` — 本阵营对应的原版 Team：既是「我方在原版体系中的代表」， 也是名单里引用我方时的匹配键之一。
- `public virtual Faction VanillaFaction { get; }` — 本阵营对应的原版 Faction：用于派系级别的匹配与比较。
- `public virtual IReadOnlyList<RelationEntry> Blacklist { get; }` — 敌对名单。
- `public virtual IReadOnlyList<RelationEntry> Whitelist { get; }` — 友好名单。
- `public IReadOnlyList<CustomWave> Waves { get; }` — 归属本阵营的自定义波次（由波次侧的 Teams 反向填充）。
- `public bool IsHostileTowards(CustomTeam? other, Team otherVanillaTeam)` — 判断本阵营是否敌对指定目标。
- `public bool? TryGetExplicitRelation(CustomTeam? other, Team otherVanillaTeam, RoleTypeId? otherRole = null)` — 取得本阵营对目标的明确态度；未明确声明时返回 null。
- `protected virtual bool IsVanillaEnemy(Team otherTeam)` — 判断本阵营与原版阵营的关系（仅 KeepVanillaRelate 为 true 时使用）。
- `protected virtual bool IsCustomEnemy(CustomTeam other)` — 判断两个自定义阵营在「原版关系」下的敌友（仅 KeepVanillaRelate 为 true 时使用）。
- `public virtual void OnRegisterEvents()` — 阵营实例被框架登记后调用；默认空实现，可覆写做实例级初始化。
- `public virtual void OnUnregisterEvents()` — 阵营实例从框架注销前调用；默认空实现，可覆写清理实例级资源。
- `public bool Equals(CustomTeam? other)`
- `public override bool Equals(object? obj)`
- `public override int GetHashCode()`
- `public override string ToString()`

#### `CustomTeamApi`

`public static class CustomTeamApi`

源码：`src/Astra.Framework/Teams/CustomTeamApi.cs`

> 自定义阵营对外 API：注册表查询与每回合实例化。

- `public static IReadOnlyDictionary<string, CustomTeam> Teams { get; }` — 当前回合的全部阵营实例。
- `public static CustomTeam? Find(string id)` — 按 Id 查找当前回合的阵营实例。
- `public static T? Find<T>() where T : CustomTeam` — 按类型查找阵营实例。
- `public static bool TryFind<T>(out T team) where T : CustomTeam` — 按类型查找阵营实例。
- `public static void Initialize()` — 按模板实例化阵营（幂等，框架启动时自动调用；跨回合复用不重建）。
- `public static void RegisterTemplate(Type type)` — 登记阵营模板（框架自动扫描时调用，通常无需手动）。
- `public static bool Register(CustomTeam team)` — 按实例注册阵营（外部框架 / 动态定义用）。
- `public static bool Register(CustomTeam team, RegistrationScope scope)` — 在指定注册作用域内按实例注册阵营。
- `public static bool Unregister(string id)` — 按 Id 注销阵营。

#### `CustomTeamRelation`

`public enum CustomTeamRelation`

源码：`src/Astra.Framework/Teams/CustomTeamRelation.cs`

> 自定义阵营与外界的关系模式。

- `Custom = 0` — 自定义关系：由 Blacklist / Whitelist 与 KeepVanillaRelate 共同决定。
- `AllFriendly` — 与所有其它阵营友好（忽略黑白名单与 KeepVanillaRelate）。
- `AllHostile` — 与所有其它阵营敌对（忽略黑白名单与 KeepVanillaRelate）。

#### `CustomTeamResolver`

`public static class CustomTeamResolver`

源码：`src/Astra.Framework/Teams/CustomTeamResolver.cs`

> 把玩家映射到自定义阵营，并计算双方敌对关系。

- `public static CustomTeam? GetTeam(ReferenceHub? hub)` — 取得玩家的自定义阵营；没有自定义角色或未声明阵营时为 null。
- `public static Team GetVanillaTeam(ReferenceHub? hub)` — 取得玩家的原版阵营。
- `public static bool IsExplicitFriendlyWith(ReferenceHub? hub, Team team)` — 判断玩家所属自定义阵营是否与指定原版阵营明确友好。
- `public static bool IsExplicitFriendlyWithScp(ReferenceHub? hub, RoleTypeId scpRole)` — 判断玩家所属自定义阵营是否豁免某个 SCP 的特殊技能。
- `public static bool IsEnemy096HitGate(Team attackerTeam, HitboxIdentity hitbox)` — SCP-096 挥击门禁：原版此处只有 IsEnemy(Team, Team)（拿不到玩家实例）， 由转译器把调用点替换为本方法，用受击者 hub 做完整的自定义阵营判定。
- `public static bool TryResolveHostility(ReferenceHub? attacker, ReferenceHub? victim, out bool result)` — 计算两名玩家是否敌对。

#### `RelationEntry`

`public readonly struct RelationEntry : IEquatable<RelationEntry>`

源码：`src/Astra.Framework/Teams/RelationEntry.cs`

> 名单条目：把 RoleTypeId / Team / Faction / 自定义阵营 Id 四种键**包成强类型**，杜绝非法字符串静默失配。

- `public RelationEntryKind Kind { get; }` — 条目类别。
- `public RoleTypeId? Role { get; }` — Role 类别的值；其它类别为 null。
- `public Team? Team { get; }` — Team 类别的值；其它类别为 null。
- `public Faction? Faction { get; }` — Faction 类别的值；其它类别为 null。
- `public string? CustomTeamId { get; }` — 自定义阵营类别的 Id；其它类别为 null。
- `public static RelationEntry For(RoleTypeId role)` — 创建 Role 条目。
- `public static RelationEntry For(Team team)` — 创建 Team 条目。
- `public static RelationEntry For(Faction faction)` — 创建 Faction 条目。
- `public static RelationEntry For(string customTeamId)` — 创建自定义阵营条目。
- `public static RelationEntry Parse(string raw)` — 解析字符串为条目（隐式转换的底层实现）。
- `public bool Equals(RelationEntry other)`
- `public override bool Equals(object? obj)`
- `public override int GetHashCode()`
- `public override string ToString()`
- `public static implicit operator RelationEntry(string raw)` — 字符串到条目的隐式转换（会校验）。
- `public static implicit operator RelationEntry(RoleTypeId role)` — RoleTypeId 到条目的隐式转换。
- `public static implicit operator RelationEntry(Team team)` — Team 到条目的隐式转换。
- `public static implicit operator RelationEntry(Faction faction)` — Faction 到条目的隐式转换。

#### `RelationEntryKind`

`public enum RelationEntryKind`

源码：`src/Astra.Framework/Teams/RelationEntry.cs`

> 名单条目的类别。

- `Role` — 原版角色（最细粒度，如 Scp049）。
- `Team` — 原版阵营（如 SCPs）。
- `Faction` — 原版派系（如 SCP）。
- `CustomTeam` — 自定义阵营 Id。

#### `TeamRef`

`public readonly struct TeamRef : IEquatable<TeamRef>`

源码：`src/Astra.Framework/Teams/TeamRef.cs`

> 阵营引用：把「自定义阵营」与「原版阵营」收敛到同一种值类型，供角色等组件统一表达阵营归属。

- `public TeamRef(CustomTeam? customTeam, Team vanillaTeam)` — 用自定义阵营（可选）与原版阵营构造阵营引用。
- `public CustomTeam? CustomTeam { get; }` — 自定义阵营；为 null 表示只使用原版阵营。
- `public Team VanillaTeam { get; }` — 原版阵营：自定义阵营存在时为其声明的 VanillaTeam 锚点。
- `public bool IsCustom { get; }` — 是否由自定义阵营接管。
- `public static TeamRef FromVanilla(Team team)` — 用原版阵营构造阵营引用。
- `public static TeamRef FromCustom(CustomTeam team)` — 用自定义阵营构造阵营引用，原版阵营取其锚点。
- `public bool Equals(TeamRef other)`
- `public override bool Equals(object? obj)`
- `public override int GetHashCode()`
- `public override string ToString()`
- `public static implicit operator TeamRef(Team team)` — 原版阵营隐式转换为阵营引用。
- `public static implicit operator TeamRef(CustomTeam team)` — 自定义阵营隐式转换为阵营引用。
- `public static implicit operator Team(TeamRef team)` — 阵营引用隐式转换为原版阵营（自定义阵营时取其 VanillaTeam 锚点）。
- `public static bool operator ==(TeamRef left, TeamRef right)` — 比较两个阵营引用是否相同（自定义阵营按实例、原版阵营按枚举值）。
- `public static bool operator !=(TeamRef left, TeamRef right)` — 比较两个阵营引用是否不同。


### `Astra.Waves`

#### `CustomWave`

`public abstract class CustomWave : ICustomContent, ICallOnRegister, ICallOnUnregister`

源码：`src/Astra.Framework/Waves/CustomWave.cs`

> 自定义刷新波次的框架基类：声明「刷新哪些自定义角色、各多少个」以及所属阵营与播报。

- `public abstract string Id { get; }`
- `public abstract string Name { get; }`
- `public virtual IReadOnlyDictionary<RoleEntry, int> Table { get; }` — 刷新配置：角色条目 → 数量（-1 表示填充剩余玩家）。
- `public virtual TeamRef Team { get; }` — 波次的主阵营：自定义阵营优先，未声明时回落到原版 OtherAlive。
- `public virtual IReadOnlyList<TeamRef> Teams { get; }` — 本波次归属的阵营条目列表（自定义阵营与原版阵营可混编）。
- `public IReadOnlyList<RoleEntry> Roles { get; }` — 本波次包含的角色条目（自定义角色由 IWaveSpawnRole 反向登记）。
- `public virtual IReadOnlyList<WaveSpawnEntry> Spawns { get; }` — 额外声明的刷新型条目：可混编原版 / 自定义角色，并覆盖角色侧声明的数量。
- `public virtual bool ModifyThreat { get; }` — 是否参与设施威胁指数调整。
- `public virtual float CustomThreat { get; }` — 自定义威胁调整值；小于零时由处理器按波次威胁等级计算。
- `public virtual bool SpawnProtect { get; }` — 是否为生成的玩家附带出生保护。
- `public virtual float SpawnProtectDuration { get; }` — 出生保护时长（秒）；-1 表示使用框架默认值。
- `public virtual string? Announcement { get; }` — Announcer 播报正文；null 表示不播报。
- `public virtual string? AnnouncementTranslation { get; }` — Announcer 播报的翻译键或译文；null 表示不翻译。
- `public virtual Color WaveColor { get; }` — 波次配色：取主阵营中自定义阵营的配色，无自定义阵营时为白色。
- `protected virtual float DefaultSpawnProtectDuration { get; }` — 默认出生保护时长（秒）。
- `public virtual IReadOnlyList<Player> Spawn(IEnumerable<Player> players)` — 按 Table 把玩家分配到各角色并套用。
- `public virtual bool CheckAllowSpawn(Player player)` — 判断玩家是否允许被本波次刷新。
- `protected virtual void OnWavePlayerSpawned(Player player)` — 单个玩家生成完成后的钩子。
- `protected virtual void OnWaveSpawned(IReadOnlyList<Player> players)` — 全部玩家生成完成后的钩子。
- `public virtual void OnRegisterEvents()` — 波次实例被框架登记后调用；默认空实现，可覆写做实例级初始化。
- `public virtual void OnUnregisterEvents()` — 波次实例从框架注销前调用；默认空实现，可覆写清理实例级资源。

#### `CustomWaveApi`

`public static class CustomWaveApi`

源码：`src/Astra.Framework/Waves/CustomWaveApi.cs`

> 自定义波次对外 API：角色生成器注册与波次执行。

- `public static IReadOnlyDictionary<string, CustomWave> Waves { get; }` — 当前回合的全部波次实例。
- `public static ICustomWaveRoleSpawner? RoleSpawner { get; }` — 当前生效的角色生成器。
- `public static CustomWave? Find(string id)` — 按 Id 查找当前回合的波次实例。
- `public static T? Find<T>() where T : CustomWave` — 按类型查找波次实例。
- `public static bool TryFind<T>(out T wave) where T : CustomWave` — 按类型查找波次实例。
- `public static void Initialize()` — 按模板实例化波次（幂等，框架启动时自动调用，须晚于阵营实例化）。
- `public static void RegisterTemplate(Type type)` — 登记波次模板（框架自动扫描时调用，通常无需手动）。
- `public static bool Register(CustomWave wave)` — 按实例注册波次（外部框架 / 动态定义用）。
- `public static bool Register(CustomWave wave, RegistrationScope scope)` — 在指定注册作用域内按实例注册波次。
- `public static bool Unregister(string id)` — 按 Id 注销波次。
- `public static void SetRoleSpawner(ICustomWaveRoleSpawner spawner)` — 注册角色生成器（宿主插件在启动时调用一次；会替换已有的生成器链）。
- `public static void SetRoleSpawner(Func<string, Player, bool> trySpawn)` — 用委托注册角色生成器（会替换已有的生成器链）。
- `public static void AddRoleSpawner(ICustomWaveRoleSpawner spawner)` — 追加角色生成器：按注册顺序依次尝试，首个成功即结束。
- `public static IReadOnlyList<Player> Spawn(string waveId, IEnumerable<Player> players)` — 按 Id 立即执行波次。
- `public static IReadOnlyList<Player> Spawn(CustomWave wave, IEnumerable<Player> players)` — 立即执行指定波次实例。

#### `DelegatedWaveSorter<T>`

`public sealed class DelegatedWaveSorter<T> : IWaveSorter<T>`

源码：`src/Astra.Framework/Waves/DelegatedWaveSorter.cs`

> 用委托实现的排序器，便于调用方临时指定比较逻辑而不必新建类型。

- `public DelegatedWaveSorter(Comparison<T> comparison)` — 用比较委托构造排序器。
- `public int Compare(T left, T right)`

#### `ICustomWaveRoleSpawner`

`public interface ICustomWaveRoleSpawner`

源码：`src/Astra.Framework/Waves/ICustomWaveRoleSpawner.cs`

> 波次刷新角色的抽象出口：把「自定义角色 Id + 玩家」交给宿主实现。

- `bool TrySpawn(string roleId, Player player)` — 把玩家套用为指定自定义角色。

#### `ICustomWaveRoleSpawnerV2`

`public interface ICustomWaveRoleSpawnerV2 : ICustomWaveRoleSpawner`

源码：`src/Astra.Framework/Waves/ICustomWaveRoleSpawnerV2.cs`

> 扩展的角色生成器：同时接收原版与自定义角色条目。

- `bool TrySpawn(RoleEntry role, Player player)` — 把玩家套用为指定角色条目（原版或自定义）。

#### `IWaveSorter<in T>`

`public interface IWaveSorter<in T>`

源码：`src/Astra.Framework/Waves/IWaveSorter.cs`

> 波次排序器：决定候选波次的优先级顺序。

- `int Compare(T left, T right)` — 比较两个波次的优先级。

#### `IWaveSpawnCountProvider`

`public interface IWaveSpawnCountProvider : IWaveSpawnRole`

源码：`src/Astra.Framework/Waves/IWaveSpawnCountProvider.cs`

> 角色在「按波次」粒度声明刷新数量的可选契约：优先于 WaveSpawnCount。

- `int GetWaveSpawnCount(CustomWave wave)` — 获取本角色在指定波次中的刷新数量。

#### `IWaveSpawnRole`

`public interface IWaveSpawnRole`

源码：`src/Astra.Framework/Waves/IWaveSpawnRole.cs`

> 参与波次刷新的角色契约：声明归属的波次与刷新数量。

- `IReadOnlyList<CustomWave> Waves { get; }` — 归属的波次实例列表。
- `int WaveSpawnCount { get; }` — 在每个归属波次中的刷新数量；-1 表示填充剩余玩家。

#### `IWeightedWave`

`public interface IWeightedWave`

源码：`src/Astra.Framework/Waves/IWeightedWave.cs`

> 波次的权重与人数门槛：供 WeightedWaveAdapter 与 WaveScheduler 使用。

- `int Weight { get; }` — 权重；越大越优先。
- `int MinPlayers { get; }` — 触发所需最少存活人数；<= 0 表示不限。
- `int MaxPlayers { get; }` — 触发允许的最大存活人数；< 0 表示不限。

#### `SupportWavePool`

`public sealed class SupportWavePool`

源码：`src/Astra.Framework/Waves/SupportWavePool.cs`

> 支援波次池：按权重轮换支援波次，并在人数门槛不满足时提供降级序列。

- `public IReadOnlyCollection<CustomWave> Waves { get; }` — 池内全部波次。
- `public event Action<CustomWave>? WaveSpawned`
- `public void Register(CustomWave wave, int weight)` — 登记支援波次；实现 IWeightedWave 时权重取其声明值，否则取传入值。
- `public bool SetWeight(string waveId, int weight)` — 调整指定波次的权重。
- `public CustomWave? Pick(int playerCount)` — 按权重随机抽取一个满足人数门槛的支援波次。
- `public IReadOnlyList<CustomWave> GetWithFallback(int playerCount)` — 取得满足门槛的支援波次（权重降序）；全部不满足时返回全部候选作为降级序列。
- `public void NotifySpawned(CustomWave wave)` — 在波次生成完成后调用，触发 WaveSpawned。

#### `VanillaWaveAdapter`

`public static class VanillaWaveAdapter`

源码：`src/Astra.Framework/Waves/VanillaWaveAdapter.cs`

> 原版波次适配辅助：查询分发状态，或从原版事件源尽量解析波次序号。

- `public static bool IsDispatchingRespawnEvent { get; }` — 本次刷新是否来自原版波次（转发 IsDispatching）。
- `public static bool TryGetVanillaWave(object? source, out int waveIndex)` — 尝试从原版事件源解析波次序号。

#### `VanillaWaveDispatch`

`public static class VanillaWaveDispatch`

源码：`src/Astra.Framework/Waves/VanillaWaveDispatch.cs`

> 原版波次分发的状态标记：用 Begin 包裹原版 RespawnWave 调用， 期间可通过 IsDispatching 区分「原版波次」与「自定义波次」。

- `public static bool IsDispatching { get; }` — 当前线程是否处于原版波次分发过程中。
- `public static IDisposable Begin()` — 进入原版波次分发作用域；可嵌套，Dispose 时逐层退出。

#### `WaveComposition`

`public sealed class WaveComposition`

源码：`src/Astra.Framework/Waves/ICustomWaveRoleSpawner.cs`

> 波次角色统计：某个波次刷新了哪些角色、各多少。

- `public void Add(string roleId, int count)` — 累加一个角色的刷新数量。
- `public IReadOnlyDictionary<string, int> ToTable()` — 导出为只读字典。

#### `WaveScheduler`

`public sealed class WaveScheduler`

源码：`src/Astra.Framework/Waves/WaveScheduler.cs`

> 加权波次调度器：只决定「顺序 + 是否跳过」，实际生成仍由 Spawn 执行。

- `public WaveScheduler(Func<int>? playerCountProvider = null)` — 构造调度器。
- `public CustomWave? Current { get; private set; }` — 当前波次；Advance 成功推进后更新，队列耗尽时为 null。
- `public int Remaining { get; }` — 尚未推进的波次数量。
- `public SupportWavePool Support { get; }` — 内置支援波次池。
- `public CustomWave? PickSupportWave(int playerCount)` — 从支援池抽取一个波次，并记录为当前波次。
- `public int Load(IEnumerable<CustomWave> waves)` — 覆盖式装载波次，按权重排序并重置游标。
- `public CustomWave? Advance(int? playerCount = null)` — 推进到下一个满足人数门槛的波次。
- `public void Reset()` — 清空队列与当前波次。

#### `WaveSorterExtensions`

`public static class WaveSorterExtensions`

源码：`src/Astra.Framework/Waves/DelegatedWaveSorter.cs`

> 排序器的便捷扩展。

- `public static List<T> Sort<T>(this IWaveSorter<T> sorter, IEnumerable<T> waves)` — 按排序器的优先级返回一个新列表（靠前者优先）。

#### `WaveSpawnEntry`

`public readonly struct WaveSpawnEntry`

源码：`src/Astra.Framework/Waves/WaveSpawnEntry.cs`

> 波次的刷新型条目：角色（原版或自定义）+ 数量。

- `public WaveSpawnEntry(RoleEntry role, int count)` — 构造刷新型条目。
- `public RoleEntry Role { get; }` — 目标角色（原版或自定义）。
- `public int Count { get; }` — 刷新数量；-1 表示填充剩余玩家。
- `public static WaveSpawnEntry Custom(CustomRole role, int count)` — 用自定义角色实例创建条目。
- `public static WaveSpawnEntry Custom(string roleId, int count)` — 用自定义角色 Id 创建条目。
- `public static WaveSpawnEntry Vanilla(RoleTypeId role, int count)` — 用原版角色创建条目。

#### `WeightedWaveAdapter`

`public sealed class WeightedWaveAdapter : IWaveSorter<CustomWave>`

源码：`src/Astra.Framework/Waves/WeightedWaveAdapter.cs`

> 把 IWeightedWave 的权重转换为 IWaveSorter{T} 的排序器。

- `public static readonly WeightedWaveAdapter Instance`
- `public int Compare(CustomWave left, CustomWave right)`

<!-- AUTO-GEN:END -->
