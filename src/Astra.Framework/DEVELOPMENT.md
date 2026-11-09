# Azure Framework - 晴空框架 开发文档

> SCP: Secret Laboratory 服务器插件框架，基于 [Northwood.LabAPI](https://github.com/northwood-studios/LabAPI) 构建。
> 提供一套**标准化、低耦合**的架构，用于自动管理自定义事件处理器、Harmony 补丁、倒计时、提示、服务器专属设置、自定义技能与音频等系统，所有组件均支持通过**字符串 Id** 进行精确控制。

---

## 目录

1. [概述](#1-概述)
2. [目录结构](#2-目录结构)
3. [快速开始](#3-快速开始)
4. [核心概念：自动注册](#4-核心概念自动注册)
5. [事件处理器系统](#5-事件处理器系统)
6. [事件方法订阅系统](#6-事件方法订阅系统)
7. [Harmony 补丁系统](#7-harmony-补丁系统)
8. [提示系统](#8-提示系统)
9. [倒计时系统](#9-倒计时系统)
10. [取消令牌系统](#10-取消令牌系统)
11. [自定义技能系统](#11-自定义技能系统)
12. [服务器专属设置系统](#12-服务器专属设置系统)
13. [音频系统](#13-音频系统)
14. [射击事件](#14-射击事件)
15. [接口契约](#15-接口契约)
16. [特性（Attribute）](#16-特性attribute)
17. [核心工具](#17-核心工具)
18. [构建与部署](#18-构建与部署)
19. [设计约定与最佳实践](#19-设计约定与最佳实践)
20. [已知问题与注意事项](#20-已知问题与注意事项)

---

## 1. 概述

Azure Framework（晴空框架）是一个面向 SCP:SL 服务器的 LabAPI 插件框架。它的核心价值在于：

- **自动注册**：扫描所有已加载插件的程序集，按类型自动完成事件处理器、补丁、技能、设置的注册，插件作者无需手写样板注册代码。
- **字符串 Id 控制**：事件处理器、技能等组件都以 `IIdentifiable.Id` 作为唯一标识，可在运行时按 Id 精确注册 / 注销 / 查找。
- **低耦合**：各系统（事件、补丁、提示、倒计时、技能、音频、设置）相互独立，通过静态 `*Api` 门面暴露能力。
- **生命周期完整**：框架禁用时自动反向注销所有已注册资源（事件、补丁、令牌、技能）。

框架自身是一个 LabAPI 插件，加载优先级为 `LoadPriority.Highest`，确保其基础设施先于依赖它的插件就绪。

---

## 2. 目录结构

```
Astra/
├── AstraPlugin.cs        # LabAPI 插件入口
├── MainManager.cs                 # 服务器插件启用后的总调度器
├── AutoRegister.cs                # 自动注册核心（扫描程序集）
├── Abstractions/
│   ├── CustomAbility.cs           # 自定义技能抽象基类
│   ├── StandardAbility.cs         # 标准技能（带按键/冷却/条件）
│   ├── EnhancedEventsManager.cs   # 增强事件管理器（射击事件虚方法）
│   └── ServerSpecificSettingBase.cs # 服务器专属设置抽象基类
├── Apis/                          # 对外静态门面 API
│   ├── AudioApi.cs
│   ├── CancellationTokenApi.cs
│   ├── CountdownApi.cs
│   ├── CustomAbilityApi.cs
│   └── HintApi.cs
├── Attributes/                    # 标记特性
│   ├── DisableAutoRegisterAttribute.cs
│   ├── EventMethodAttribute.cs
│   ├── ManagedEventAttribute.cs
│   └── ManagedPatchAttribute.cs
├── Core/
│   ├── IdentifiableManager.cs     # 基于 Id 的字典管理器
│   └── MonoSingleton.cs           # Unity 懒加载单例
├── Enums/
│   ├── AbilityFailReason.cs
│   ├── CancellationTokenCategory.cs
│   ├── HintPosition.cs
│   └── KeybindAbilityType.cs
├── Events/
│   ├── PlayerEvents.cs            # 框架扩展的玩家事件
│   └── EventArgs/
│       ├── ShotEventArgs.cs
│       └── ShootingEventArgs.cs
├── Extensions/
│   ├── MapExtensions.cs
│   └── PlayerExtensions.cs
├── Interfaces/                    # 能力契约接口
│   ├── ICallOnRegister.cs
│   ├── ICallOnUnregister.cs
│   ├── ICondition.cs              # 文件名为 ICondition，接口实为 IPlayerCondition
│   ├── ICooldown.cs
│   ├── IIdentifiable.cs
│   ├── IKeybind.cs
│   ├── IManualPatch.cs
│   └── IOriginalRole.cs
├── Patches/
│   ├── HitscanHitregModuleBasePatch.cs # Shot / ShotMiss 补丁
│   └── ShotBacktrackDataPatch.cs       # Shooting 补丁
├── Systems/
│   ├── AudioSystem/AudioManager.cs
│   ├── CancellationTokenSource/TokenManager.cs, TokenSources.cs
│   ├── CountdownSystem/CountdownManager.cs
│   ├── CustomAbilitySystem/CustomAbilityManager.cs, CustomAbilityTemplateManager.cs
│   ├── EventSystem/EventManager.cs, EventMethodManager.cs
│   ├── HintSystem/HintManager.cs, HintPositionMapping.cs
│   ├── PatchSystem/PatchManager.cs
│   └── ServerSpecificSettingSystem/ServerSpecificSettingManager.cs
├── Astra.csproj          # 旧式 csproj（显式 Compile Include）
├── app.config / packages.config
└── Properties/AssemblyInfo.cs
```

---

## 3. 快速开始

### 3.1 作为依赖引用

Astra 编译后会把 DLL 通过 PostBuildEvent 复制到 `$(SolutionDir)/Libraries`。其他插件通常通过**项目引用（ProjectReference）**或**直接 DLL 引用**依赖它。

在你的插件中使用框架能力，只需实现相应接口 / 继承相应基类，并在类上打标记特性即可，**无需手动调用注册代码**。

### 3.2 最小示例：注册一个事件处理器

```csharp
using Astra.Interfaces;
using LabApi.Events.CustomHandlers;
using LabApi.Events.Arguments.ServerEvents;

public class MyHandler : CustomEventsHandler, IIdentifiable
{
    public string Id => "MyHandler"; // 全局唯一 Id，用于运行时控制

    public override void OnServerRoundStarted()
    {
        // 回合开始时的逻辑
    }
}
```

框架在服务器插件启用时会自动实例化 `MyHandler` 并注册它，因为：
1. 它实现了 `IIdentifiable`；
2. 它继承自 `CustomEventsHandler`。

### 3.3 最小示例：注册一个技能

```csharp
using Astra.Abstractions;

public class MyAbility : CustomAbility
{
    public override string Id => "MyAbility";
    public override string Name => "我的技能";
    public override string Description => "这是一个示例技能。";

    protected override void OnTriggered(LabApi.Features.Wrappers.Player player)
    {
        // 触发逻辑
    }
}
```

### 3.4 运行时控制

```csharp
// 注销/重新注册事件处理器
EventManager.Instance.UnregisterHandler("MyHandler");
EventManager.Instance.RegisterHandler("MyHandler");

// 给玩家添加技能
player.TryGetAbilityManager(out var manager);
manager.AddAbility<MyAbility>();
```

---

## 4. 核心概念：自动注册

`AutoRegister` 是框架的心脏。它在 `MainManager.OnServerPluginsEnabled()` 中被调用一次：

```csharp
var assemblies = PluginLoader.Plugins.Values;
AutoRegister.RegisterAll(assemblies);
```

`RegisterAll` 遍历**每一个已加载插件程序集**中的**每一个类型**，按以下优先级规则处理：

| 条件 | 动作 |
|---|---|
| 实现 `IIdentifiable` 且非抽象 | 进入「可识别组件」分支（见下） |
| 有 `[ManagedEvent]` 特性（且未在上一步作为事件注册过） | 注册为事件处理器 |
| 有 `[ManagedPatch]` 且（有 `[HarmonyPatch]` 或实现 `IManualPatch`） | 注册为 Harmony 补丁 |
| 方法上有 `[EventMethod]` | 订阅该方法到对应事件 |

「可识别组件」分支内又根据基类细分：

| 基类 | 注册到 |
|---|---|
| `CustomEventsHandler` | `EventManager` |
| `ServerSpecificSettingBase` | `ServerSpecificSettingManager` |
| `CustomAbility` | `CustomAbilityTemplateManager` |

全部完成后调用 `ServerSpecificSettingManager.Instance.SendToAll()` 统一下发设置。

`UnregisterAll()` 则按相反顺序销毁一切：事件、设置、补丁、令牌、事件方法。

> **注意**：`RegisterAll` 是幂等的（内部有 `Initialized` 标志），重复调用会直接返回 `false`。

---

## 5. 事件处理器系统

### 5.1 EventManager

单例（`MonoSingleton<EventManager>`），内部维护 `Dictionary<string, CustomEventsHandler>`。

**自动注册流程**（`Register(Type)`）：
1. `Activator.CreateInstance(type)` 实例化处理器；
2. 若实现 `ICallOnRegister`，先调用 `OnRegisterEvents()`（注册前的钩子）；
3. 解析 Id：优先取 `IIdentifiable.Id`，若为空则取 `[ManagedEvent].Id`；
4. 存入字典并打日志；
5. **除非**类型标了 `[DisableAutoRegister]`，否则调用 `CustomHandlersManager.RegisterEventsHandler(handler)` 真正挂载到 LabAPI。

**运行时控制**：

```csharp
EventManager.Instance.RegisterHandler("Id");     // 按 Id 重新挂载
EventManager.Instance.UnregisterHandler("Id");   // 按 Id 卸载
```

### 5.2 生命周期接口

- `ICallOnRegister.OnRegisterEvents()`：注册时回调。
- `ICallOnUnregister.OnUnregisterEvents()`：注销时回调。

> 在自定义处理器中**必须把 `OnUnregister` 实现为接口方法 `OnUnregisterEvents()`**（C# 8 接口成员），否则 LabAPI 的 `OnUnregister` 会与框架的注销流程脱钩。

### 5.3 EnhancedEventsManager

若需要响应框架扩展的射击事件，继承 `EnhancedEventsManager` 并重写虚方法即可（它本身就是 `CustomEventsHandler`，会被自动注册）：

```csharp
public class MyCombatHandler : EnhancedEventsManager, IIdentifiable
{
    public string Id => "MyCombatHandler";

    public override void OnExiledShot(ShotEventArgs ev)
    {
        // 命中后逻辑
    }
}
```

---

## 6. 事件方法订阅系统

`EventMethodAttribute` 允许**把任意静态方法直接订阅到任意 .NET 事件**（例如 LabAPI 或游戏程序集的事件），无需继承 `CustomEventsHandler`。

```csharp
using Astra.Attributes;

public static class MySubscriber
{
    // 订阅 ServerSpecificSettingsSync.ServerOnSettingValueReceived 事件
    [EventMethod(typeof(ServerSpecificSettingsSync), nameof(ServerSpecificSettingsSync.ServerOnSettingValueReceived))]
    public static void OnSettingReceived(ReferenceHub hub, ServerSpecificSettingBase setting)
    {
        // 处理设置输入
    }
}
```

`AutoRegister` 会扫描所有带 `[EventMethod]` 的方法，交给 `EventMethodManager.Register(MethodInfo)`：

1. 通过 `AccessTools.DeclaredEvent` 反射出目标 `EventInfo`；
2. `Delegate.CreateDelegate` 生成与事件委托签名兼容的订阅者；
3. `TargetEvent.AddEventHandler(...)` 完成订阅；
4. 记录订阅元数据，供 `UnregisterAll` 精确撤销。

---

## 7. Harmony 补丁系统

### 7.1 声明式补丁

在补丁类上同时打 `[ManagedPatch]` 和 `[HarmonyPatch(...)]` 即可自动应用：

```csharp
using Astra.Attributes;
using HarmonyLib;

[ManagedPatch, HarmonyPatch(typeof(TargetClass), nameof(TargetClass.Method))]
internal static class MyPatch
{
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        // ...
    }
}
```

### 7.2 手动补丁

若逻辑复杂，实现 `IManualPatch` 并在 `ApplyPatch(Harmony)` 中自行完成打补丁：

```csharp
[ManagedPatch]
public class MyManualPatch : IManualPatch
{
    public void ApplyPatch(Harmony harmony)
    {
        // harmony.Patch(...)
    }
}
```

### 7.3 PatchManager

- 自动模式：`entry.Harmony.CreateClassProcessor(type).Patch()`；
- 手动模式：实例化 `IManualPatch` 并调用 `ApplyPatch`；
- `[DisableAutoRegister]` 可让补丁「只登记、不立即应用」，之后用 `Patch("FullName")` 手动启用。

Harmony 实例策略：

- **声明式补丁**（`[ManagedPatch] + [HarmonyPatch]`）共用框架的统一实例 `com.azure.framework`；
- **手动补丁**（`[ManagedPatch] + IManualPatch`）使用专属实例 `com.azure.framework.manual.<FullName>`，保证 `Unpatch` 能按条目精确卸载。

`Unpatch("FullName")` 只卸载对应条目：声明式补丁走 `PatchClassProcessor.Unpatch()`，手动补丁走 `UnpatchAll(自身 Id)`。`UnpatchAll()`（框架禁用时调用）按每个条目自身 Id 卸载，幂等可重复。

---

## 8. 提示系统

基于 [HintServiceMeow](https://github.com/MeowServer/HintServiceMeow) 实现，统一入口为静态门面 `HintApi`。

```csharp
// 静态提示（3 秒，顶部居中）
HintApi.SendTo(player, "欢迎回来", duration: 3f, position: HintPosition.CenterTop);

// 动态提示（文本可随时间变化）
HintApi.SendDynamic(player, () => $"当前血量 {player.Health}", duration: 5f);

// 直接发送 HintServiceMeow 的 AbstractHint
HintApi.SendAbstractHint(player, myHint, 3f);
```

**位置**由 `HintPosition` 枚举表达，`HintPositionMapping` 将其映射为 HintServiceMeow 的对齐方式与坐标（`Custom` 位置需自行处理）。

**去重规则**（`HintManager.ShouldReplace`）：
- 新提示有 `Id` → 替换所有相同 `Id` 的旧提示；
- 新提示无 `Id` → 替换所有相同文本的旧提示。

`duration <= 0` 表示永久显示；`duration > 0` 则到期自动移除。

---

## 9. 倒计时系统

`CountdownManager` 是单例 MonoBehaviour，内部用**二叉最小堆**（按触发时间排序）实现优先级队列：

- `AddTimer(delay, action, tickAction, repeat)`：`O(log n)` 入队，返回计时器 Uuid。
- `RemoveTimer(uuid)`：`O(1)` 惰性删除（仅标记取消）。
- `ClearTimer()` / `Init()`：清空。

```csharp
int uuid = CountdownManager.Instance.AddTimer(
    delay: 10f,
    action: () => { /* 到期执行 */ },
    tickAction: () => { /* 到期后每秒执行一次，可空 */ },
    repeat: () => false); // 返回 true 则按原 delay 重新入队
```

静态门面 `CountdownApi.Start(...)` 是等价简写。

---

## 10. 取消令牌系统

框架提供三类取消令牌，用于在协程 / 异步任务中感知「框架禁用」「回合重启」「玩家离开」：

```csharp
var token = CancellationTokenApi.GetPlayersToken(player);        // 玩家离开时取消
var token = CancellationTokenApi.GetRoundRestartToken();         // 回合重启时取消
var token = CancellationTokenApi.GetFrameworkDisableToken();     // 框架禁用时取消
```

内部 `TokenManager`（`[ManagedEvent]` 内部事件处理器）监听玩家加入 / 离开 / 回合重启，维护 `TokenSources` 中的 `CancellationTokenSource`，在对应时机调用 `Cancel()`。

---

## 11. 自定义技能系统

### 11.1 层次结构

```
CustomAbility            // 抽象基类：Id / Name / Description / Owner / 生命周期
   └── StandardAbility   // 标准技能：+ 按键(IKeybind) + 冷却(ICooldown) + 条件(IPlayerCondition)
```

### 11.2 CustomAbility 生命周期

| 方法 | 说明 |
|---|---|
| `OnAdded(Player)` | 技能添加到玩家时 |
| `OnRemoved(Player)` | 技能从玩家移除时 |
| `OnTriggered(Player)` | 技能触发时 |
| `CreateInstance()` | 创建玩家实例（默认反射新建，可覆写） |

### 11.3 StandardAbility

```csharp
public class Dash : StandardAbility
{
    public override string Id => "Dash";
    public override string Name => "冲刺";
    public override int SettingId => 1050;                 // 对应 ServerSpecificSettings 的按键 SettingId
    public override double CooldownTime { get; set; } = 8; // 冷却秒数
    public override Func<Player, bool> Condition { get; protected set; } = p => p.IsAlive;

    protected override void OnTriggered(Player player)
    {
        // 冲刺逻辑
    }
}
```

- `IsReady` = 冷却就绪 **且** 条件满足。
- 触发时若未就绪，会回调 `OnTriggerFailed`，失败原因由 `AbilityFailReason` 区分：
  - `NotReady`：冷却未结束；
  - `ConditionFail`：条件不满足。
- `IKeybind.KeybindAbilityType` 区分 `Press`（按下触发）与 `Hold`（松开触发）。
- `SettingId` 是**模板声明值**。授予玩家时若与该玩家既有按键冲突，框架会只给新实例改配一个空闲槽位；此时读实际生效值请用 `EffectiveSettingId`（详见 §20.4）。

### 11.4 CustomAbilityManager（按玩家）

每个玩家一个 `CustomAbilityManager`，负责该玩家的技能实例增删查：

```csharp
player.TryGetAbilityManager(out var manager);
manager.AddAbility<Dash>();          // 添加技能
manager.TriggerAbility<Dash>();      // 触发技能
manager.TryGetAbility("Dash", out var ability);
manager.RemoveAbility("Dash");       // 移除技能
```

`CustomAbilityTemplateManager` 保存全局**技能模板**（按 Id / Type 索引），在玩家换角色时：
- 清空旧技能；
- 自动为实现了 `IOriginalRole`、且 `RoleType` 与新手匹配的技能添加实例。

静态门面 `CustomAbilityApi` 提供等价简写，并通过只读集合 `AbilityTemplates` 暴露当前已注册的全部技能模板；
`PlayerExtensions` 提供了 `player.TryGetPlayerAbility<T>(...)` 等扩展方法。

---

## 12. 服务器专属设置系统

继承 `ServerSpecificSettingBase` 并返回 LabAPI 的 `ServerSpecificSettingBase[]`：

```csharp
public class MySettings : ServerSpecificSettingBase
{
    public override string Id => "MySettings";
    public override int Priority { get; } = 0; // 数值越小越靠前，-1 表示排最后
    public override ServerSpecificSettingBase[] ActiveSettings => new ServerSpecificSettingBase[]
    {
        new SSKeybindSetting(1050, "冲刺按键", KeyCode.LeftShift)
    };
}
```

`ServerSpecificSettingManager.SendToAll()` 会把所有注册的设置按 `Priority` 排序、按 `SettingId` 去重后统一下发给客户端。

---

## 13. 音频系统

静态门面 `AudioApi` 基于 SecretLabNAudio：

```csharp
byte id = AudioApi.PlayAudio(player, "path/to/sound.ogg", volume: 0.8f); // 仅该玩家听到
byte id = AudioApi.PlayAudio("path/to/sound.ogg");                        // 全局播放
AudioApi.Stop(player);  // 停止某玩家正在播放的音频
AudioApi.Stop(id);      // 按 Id 停止全局音频
AudioApi.Stop();        // 停止所有全局音频
```

`AudioManager`（`[ManagedEvent]`）在回合重启时自动归还所有音频播放器到对象池。

---

## 14. 射击事件

框架通过 IL 织入（Transpiler）扩展了两个射击相关事件，并提供了与 LabAPI 风格一致的 EventArgs：

| 事件 | 触发时机 | 可取消 |
|---|---|---|
| `PlayerEvents.Shooting` (`ShootingEventArgs`) | 玩家开火**前** | `IsAllowed = false` |
| `PlayerEvents.Shot` (`ShotEventArgs`) | 子弹命中**后** | `CanHurt` / `CanSpawnImpactEffects` |

对应的补丁：
- `ShotBacktrackDataPatch.Shooting`：织入 `ShotBacktrackData.ProcessShot`；
- `HitscanHitregModuleBasePatch.ShotTarget`：织入 `ServerApplyDestructibleDamage`（命中）；
- `HitscanHitregModuleBasePatch.ShotMiss`：织入 `ServerAppendPrescan`（未命中）。

> 这些事件参数类移植自 ExMod 团队（CC BY-SA 3.0），注意 `ShootingEventArgs` 中的 `ClaimedTarget` / `ShotBacktrackData` 由客户端提供，**不可信任**。

---

## 15. 接口契约

| 接口 | 成员 | 用途 |
|---|---|---|
| `IIdentifiable` | `string Id { get; }` | 提供全局唯一 Id |
| `ICallOnRegister` | `void OnRegisterEvents()` | 注册时回调 |
| `ICallOnUnregister` | `void OnUnregisterEvents()` | 注销时回调 |
| `IManualPatch` | `void ApplyPatch(Harmony)` | 手动应用 Harmony 补丁 |
| `ICooldown` | `IsReady` / `CooldownTime` / `Remaining` / `Cooldown` / `TriggerCooldown(double)` | 冷却能力 |
| `IKeybind` | `SettingId` / `KeybindAbilityType` / `SyncIsPressed` / `KeyDown()` / `KeyUp()` | 按键能力 |
| `IPlayerCondition` | `Func<Player, bool> Condition { get; }` | 触发条件 |
| `IOriginalRole` | `RoleTypeId RoleType { get; }` | 声明技能对应的原始角色 |

---

## 16. 特性（Attribute）

| 特性 | 目标 | 说明 |
|---|---|---|
| `[ManagedEvent(string id)]` | 类 | 把非 `IIdentifiable` 的事件处理器纳入自动注册，`id` 作为回退 Id |
| `[ManagedPatch]` | 类 | 标记为框架托管的 Harmony 补丁 |
| `[EventMethod(Type eventType, string eventName[, object eventInstance])]` | 方法 / 委托 | 把方法订阅到指定事件 |
| `[DisableAutoRegister]` | 类 | 登记组件但**不自动挂载**，需手动 `RegisterHandler` / `Patch` 启用 |

---

## 17. 核心工具

### 17.1 MonoSingleton\<T\>

Unity 懒加载单例基类：首次访问 `Instance` 时自动创建 GameObject 并 `DontDestroyOnLoad`，保证场景切换不销毁。

### 17.2 IdentifiableManager\<T\>

`Dictionary<string, T>` 的薄封装，提供 `Add` / `Remove` / `Get` / `GetAll` / `Contains`。`Add` 会校验 Id 非空。

---

## 18. 构建与部署

### 18.1 项目配置

- **项目类型**：旧式（non-SDK）csproj，`.NET Framework 4.8.1`，输出为类库（`Library`）。
- **C# 语言版本**：`LangVersion 9.0` 已在主 PropertyGroup 全局设置，**所有配置**（含 AnyCPU）均可编译。
- **平台**：推荐 **x64**（`PlatformTarget=x64`）；AnyCPU 现在也能编译，但部署请使用 x64 产物。
- **PostBuildEvent**：构建后自动把 DLL 复制到解决方案的 `Libraries` 目录。目标目录通过 `$(AstraLibrariesDir)` 解析：在解决方案内构建取 `$(SolutionDir)Libraries`，单独构建 csproj（如 `dotnet build` 直接构建项目）时回退到项目上层的 `..\Libraries`，两种方式都无需额外传参。

### 18.2 引用说明

**游戏程序集**（`Assembly-CSharp-Publicized`、`Mirror-Publicized`、`UnityEngine.CoreModule`、`CommandSystem.Core` 等）的 HintPath 统一由 `$(AstraGameLibs)` 属性控制：

- 默认值为本机 Steam 服务器目录 `D:\Programs\Steam\steamapps\common\SCP Secret Laboratory Dedicated Server\SCPSL_Data\Managed`；
- 其他机器 / 其他版本可用 `-p:AstraGameLibs=<Managed 目录>` 覆盖（相对路径按项目目录解析，可写 `..\Libraries`）。

> ⚠️ **游戏版本必须与代码目标一致**：`Site19/Libraries` 中的游戏程序集是历史快照，与当前代码依赖的 API 并不完全匹配（直接指向它会编译失败）。若要迁移到 Libraries 目录，请先确认快照版本已同步到当前服务器版本。

**NuGet 依赖**（`packages.config`）：

| 包 | 版本 | 用途 |
|---|---|---|
| Lib.Harmony | 2.4.2 | 运行时补丁 |
| Northwood.LabAPI | 1.1.7 | 插件 API |
| HintServiceMeow | 5.5.0 | 提示系统 |
| SecretLabNAudio.Core | 2.1.0 | 音频播放 |
| NAudio.Core | 2.2.1 | 音频解码 |
| YamlDotNet | 18.1.0 | YAML 配置 |

### 18.3 构建命令

```powershell
# 直接构建项目（推荐；PostBuild 会自动复制到 Site19\Libraries）
dotnet build "Astra\Astra.csproj" -c Release -p:Platform=x64

# 或使用完整 MSBuild（可指定解决方案根目录）
& "D:\Programs\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" `
  "Astra\Astra.csproj" `
  /p:SolutionDir="<解决方案根目录>" `
  /p:Configuration=Release /p:Platform=x64 `
  /t:Build /m /nologo /v:minimal

# 使用其它游戏程序集目录构建时：
dotnet build "Astra\Astra.csproj" -c Release -p:Platform=x64 -p:AstraGameLibs=<Managed 目录>
```

### 18.4 部署

构建产物 `Astra.dll` 部署到 LabAPI 的全局插件目录：

```
<SCP Secret Laboratory>\LabAPI\plugins\global\Astra.dll
```

框架的公共能力通过 `Astra.Apis` / `Astra.Interfaces` 等命名空间暴露给其他插件，依赖它的插件将 `Astra.dll` 放入自己的依赖目录或全局依赖目录即可。

---

## 19. 设计约定与最佳实践

1. **Id 唯一性**：所有 `IIdentifiable.Id` 必须在全局范围内唯一，冲突会导致注册互相覆盖或警告。
2. **生命周期成对**：实现了 `ICallOnRegister` 就应实现 `ICallOnUnregister`，保证资源对称释放。
3. **注销幂等**：`UnregisterAll` / `ClearAll` / `CancelAll` 均应幂等，可安全重复调用。
4. **协程 / 异步**：长任务优先使用 `CancellationTokenApi` 提供的令牌，确保回合重启或框架禁用时能及时停止。
5. **补丁谨慎**：Transpiler 织入依赖 IL 布局，游戏更新后需重新校验注入点；优先使用 `[ManagedPatch] + [HarmonyPatch]` 声明式写法。
6. **性能**：热点路径（如提示去重、计时器调度）避免每次调用产生 LINQ / 闭包分配，框架内部已使用 `ListPool` 与二叉堆等优化。
7. **日志**：各系统统一使用 `LabApi.Features.Console.Logger`，并带 `[模块名]` 前缀，便于排查。

---

## 20. 已知问题与注意事项

1. **`ICondition.cs` 命名不符**：文件名为 `ICondition.cs`，但其中定义的接口实际是 `IPlayerCondition`，阅读时注意。
2. **游戏程序集版本需匹配**：`AstraGameLibs` 指向的 `Managed` 目录必须与代码依赖的游戏 API 版本一致；`Site19/Libraries` 中的游戏程序集是历史快照，直接指向它会编译失败（详见 18.2）。
3. **`CustomAbilityTemplateManager` / `TokenManager` / `AudioManager` 为 internal**：它们通过 `[ManagedEvent]` 自动注册，不对外暴露类型，仅通过对应 `*Api` / `*Manager` 门面使用。
4. **按键冲突改配实例 SettingId（不再覆盖旧实例）**：`CustomAbilityManager.AddAbility` 遇到 SettingId 冲突时，会为**新实例**从已下发的 `SSKeybindSetting` SettingId 池中按升序改配第一个空闲值（只写实例的 `CustomAbility.AssignedSettingId`，**模板不变**），并告警「已改配」；池中已无空闲值时**拒绝添加**并告警「已拒绝添加」。旧实例始终保持占用，不再被静默替换。
   - 读实例的实际按键槽位请用 `CustomAbility.EffectiveSettingId`（已改配则返回改配值，否则回落模板 `IKeybind.SettingId`）。
   - 候选池取自 `ServerSpecificSettingsSync.DefinedSettings` 中全部 `SSKeybindSetting`，**按「技能模板声明过的键位优先」排序**：冲突改配会尽量落在技能槽（`Ability1~4`）上，技能槽全部占满才会按升序借用其它功能键位（如投票键 `VoteApprove/VoteReject`，F9/F10）。
5. **`PermissionHandlerPatch` 等手动补丁**（`IManualPatch`）使用专属 Harmony 实例（`com.azure.framework.manual.<FullName>`），重打补丁时注意不要与其它 Harmony 实例的 Id 冲突。

---

## 附录：重构变更记录（2026-09 提交）

本次提交收录自上一 git 基线（`6e2394d`，2026-07-11）以来未入库的全部重构，并做了最终收敛。**兼容性已逐项验证**：与服务器部署版 `Astra.dll`（2026-08-19 构建）用 dnSpyEx 反编译对比，公开类型 / 方法 / 属性签名**零变化**，外部依赖方不会出现 `MethodNotFoundException`。

相对 git 基线的主要变更：

- 目录与命名空间整理：`Hint\` → `Systems\HintSystem\`、`Core\IIdentifiable` → `Interfaces\IIdentifiable`、`Events\Handlers\Player` → `Events\PlayerEvents`、`Patching` → `Systems\PatchSystem`；删除废弃的 `Events\EventArgs\Interfaces\*`（已由 LabAPI 原生事件参数接口取代）与 `PluginLoaderPatch`（由 `MainManager.OnServerPluginsEnabled` 取代）。
- 新增系统：`EventMethodManager`（`[EventMethod]` 订阅任意 .NET 事件）、`CustomAbilityTemplateManager` / `StandardAbility`、`TokenManager` 取消令牌、`ServerSpecificSettingManager` 统一下发、`AudioApi` 门面等。
- `CountdownManager`：排序列表改为**二叉最小堆 + O(1) 惰性删除**，对外方法签名与语义不变。

提交前最终收敛与修复：

- `PatchManager`：声明式补丁共用框架 Harmony 实例、手动补丁使用专属实例；`Unpatch` 修复为按条目精确卸载（部署版实现会连同其它补丁一起卸载）。
- 构建系统：`LangVersion 9.0` 全局化（AnyCPU 亦可编译）；游戏程序集路径抽象为 `AstraGameLibs` 属性（默认 Steam 路径，可覆盖）；PostBuildEvent 在单独构建 csproj 时也能正确复制到 `Site19\Libraries`。
- 删除与 `Attributes\EventMethodAttribute.cs` 重复的根目录遗留文件（`EventMethodAttribute.cs`）。
- 验证：`dotnet build`（Release|x64）0 警告 0 错误；`Site19.PluginContent` / `Site19.ExtendedLog` / `TextChatMeow` / `MengXinSheQu.FirearmPack` 四个依赖方全部编译通过。

---

## 附录：命名空间速查

| 命名空间 | 主要内容 |
|---|---|
| `Astra` | `AstraPlugin`、`MainManager`、`AutoRegister` |
| `Astra.Apis` | `HintApi`、`CountdownApi`、`CancellationTokenApi`、`CustomAbilityApi`、`AudioApi` |
| `Astra.Abstractions` | `CustomAbility`、`StandardAbility`、`EnhancedEventsManager`、`ServerSpecificSettingBase` |
| `Astra.Interfaces` | `IIdentifiable`、`ICallOnRegister`、`ICallOnUnregister`、`IManualPatch`、`ICooldown`、`IKeybind`、`IPlayerCondition`、`IOriginalRole` |
| `Astra.Attributes` | `ManagedEventAttribute`、`ManagedPatchAttribute`、`EventMethodAttribute`、`DisableAutoRegisterAttribute` |
| `Astra.Enums` | `HintPosition`、`CancellationTokenCategory`、`AbilityFailReason`、`KeybindAbilityType` |
| `Astra.Core` | `MonoSingleton<T>`、`IdentifiableManager<T>` |
| `Astra.Events` | `PlayerEvents` |
| `Astra.Events.EventArgs` | `ShotEventArgs`、`ShootingEventArgs` |
| `Astra.Extensions` | `MapExtensions`、`PlayerExtensions` |
| `Astra.Systems.*` | 各系统内部实现（多为 internal） |
# 近期变更：框架组件运行时管理

`EventManager` 现在保留所有已登记的 `IIdentifiable`/`ManagedEvent` 事件处理器，并通过 `Handlers`、`TryGetHandlerStatus`、`TryEnableHandler`、`TryDisableHandler` 暴露只读状态与安全启停 API。禁用不会删除登记项，因此可以再次启用；启停会调用 `ICallOnRegister`/`ICallOnUnregister` 生命周期，并避免重复订阅。

`PatchManager` 同样通过 `Patches`、`TryGetPatchStatus`、`TryPatch`、`TryUnpatch` 暴露登记状态。补丁 Id 为类型完整名称，同时接受唯一短类名；重复应用或卸载是幂等的。`DisableAutoRegister` 类型只登记，不会在自动注册阶段执行。
