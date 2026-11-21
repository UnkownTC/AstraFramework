<div align="center">

# AstraFramework

**SCP: Secret Laboratory 服务端框架库**

基于 LabAPI · 自定义内容 · 托管补丁 · 声明式事件

![Version](https://img.shields.io/badge/version-8.0.0-2ea043)
![Framework](https://img.shields.io/badge/.NET%20Framework-4.8.1-512bd4)
![Platform](https://img.shields.io/badge/platform-SCP%3A%20SL%20server-6f42c1)
![License](https://img.shields.io/badge/license-MIT-yellow)

</div>

AstraFramework 是运行在 SCP: Secret Laboratory 服务端的**框架库**，不是独立插件。它在 LabAPI 之上提供自定义角色、阵营、波次、物品、效果与技能，并统一管理 Harmony 补丁、事件方法和注册生命周期。

> 本仓库只包含抽象与机制；具体角色、物品、波次等实现应放在业务插件中。

> 下载：[GitHub Releases](https://github.com/UnkownTC/AstraFramework/releases) · 文档：[公开 API 速查](docs/API.md) · [架构规范](docs/STANDARD.md)

## 特性

| 领域 | 提供的能力 |
|---|---|
| 角色 / 阵营 / 波次 | `CustomRole`、`TeamRef`、`CustomWave`；波次调度、权重波次、支援波池、原版波次适配 |
| 物品 | `CustomItem` / `CustomFirearm` 及装备、拾取、使用、换弹、瞄准、射击等扩展钩子 |
| 效果 / 技能 | `CustomEffect` 叠加与控制器、`CustomAbility` 及玩家技能管理、HumeShield 扩展 |
| 事件 | `[EventMethod]` 声明式订阅、增强事件处理器、事件生命周期管理 |
| 补丁 | `[PatchMethod]` / `[ManagedPatch]` 托管 Harmony 补丁，按版本与状态查询 |
| 注册生命周期 | `Astra.Lifecycle`：按程序集记录注册来源，插件禁用时协作式撤销 |
| 位置 / 刷新点 | `PositionEntry`、`SpawnPointEntry`、`PositionTeleport`；`RoundStartSpawnManager` 在原版分配完成后按概率生成波次角色与物品 |
| 分析器 | Roslyn 分析器校验事件、补丁与契约用法（AST1001–AST5005） |

## CustomContent 示例

下面是最小骨架，完整签名以 [公开 API 速查](docs/API.md) 为准，命名空间与依赖方向见 [架构规范](docs/STANDARD.md)。示例省略宿主插件的启用/禁用流程与命名空间声明。

### 自定义角色

```csharp
using System.Collections.Generic;

using Astra.Items;
using Astra.Roles;
using Astra.Teams;

using InventorySystem.Items;

using PlayerRoles;

public sealed class ScoutRole : CustomRole
{
    public override string Id => "scout";
    public override string Name => "侦察兵";
    public override RoleTypeId RoleType => RoleTypeId.ClassD;

    // 自定义阵营可写成 TeamRef.FromCustom(team)；原版阵营可直接隐式转换。
    public override TeamRef Team => PlayerRoles.Team.ChaosInsurgency;

    public override float MaxHealth { get; set; } = 120f;

    public override IReadOnlyList<ItemEntry> Items { get; set; } =
        [ItemEntry.Vanilla(ItemType.Medkit)];
}
```

> 若用同一个适配器类从配置生成多个角色，请覆写 `CreateInstance()` 复制定义数据；默认实现只按类型实例化，会丢失实例上的配置。

### 自定义物品

```csharp
using Astra.Items;

using InventorySystem.Items;
using InventorySystem.Items.Usables;

using LabApi.Features.Wrappers;

using UnityEngine;

public sealed class StimItem : CustomItem
{
    public override string Id => "stim";
    public override string Name => "兴奋剂";
    public override ItemType BaseItem => ItemType.Medkit;

    // 返回 false 阻止原版使用；如需消耗物品，请在自定义逻辑里自行处理。
    public override bool OnUsing(Player player, UsableItem item)
    {
        player.Health = Mathf.Min(player.MaxHealth, player.Health + 50f);
        return false;
    }
}
```

### 自定义波次

```csharp
using System.Collections.Generic;

using Astra.Waves;

using PlayerRoles;

public sealed class ScoutWave : CustomWave
{
    public override string Id => "scout_wave";
    public override string Name => "侦察波次";

    public override IReadOnlyList<WaveSpawnEntry> Spawns { get; } =
    [
        WaveSpawnEntry.Custom("scout", 2),
        WaveSpawnEntry.Vanilla(RoleTypeId.ClassD, -1),
    ];
}
```

### 回合开始生成

波次、物品或角色实现 `ISpawnOnRoundStart` 后，`RoundStartSpawnManager` 会在原版角色分配完成后读取刷新点与概率：

```csharp
using System.Collections.Generic;

using Astra.Positions;
using Astra.Waves;

public sealed class ScoutWave : CustomWave, ISpawnOnRoundStart
{
    public override string Id => "scout_wave";
    public override string Name => "侦察波次";

    public override IReadOnlyList<WaveSpawnEntry> Spawns { get; } =
    [
        WaveSpawnEntry.Custom("scout", 2),
    ];

    public int MinSpawnCount => 1;
    public int MaxSpawnCount => 2;

    public IReadOnlyDictionary<SpawnPointEntry, float> CustomSpawnPoints { get; } =
        new Dictionary<SpawnPointEntry, float>
        {
            [SpawnPointEntry.Random()] = 0.5f,
        };
}
```

`StandardRole` 只实现 `ISpawnDefinition`：默认 `CustomSpawnPoints` 为空；它只用这个集合在 `AddRole` 后选点传送，不参与管理器的自动生成，也没有 `MinSpawnCount` / `MaxSpawnCount`。需要参与自动生成的角色应实现 `ISpawnOnRoundStart`。下面是只实现 `ISpawnDefinition` 的 `StandardRole` 示例：

```csharp
using System.Collections.Generic;

using Astra.Positions;
using Astra.Roles;

using PlayerRoles;

public sealed class ScoutRole : StandardRole
{
    public override string Id => "scout";
    public override string Name => "侦察兵";
    public override RoleTypeId RoleType => RoleTypeId.ClassD;

    public override IReadOnlyDictionary<SpawnPointEntry, float> CustomSpawnPoints { get; } =
        new Dictionary<SpawnPointEntry, float>
        {
            [SpawnPointEntry.Random()] = 1f,
        };
}
```

`StandardRole.AddRole` 在 `SpawnFlags` 含 `UseSpawnpoint` 时，从 `CustomSpawnPoints` 按概率选一个点并传送；数组为空则不传送。它不会读取 `MinSpawnCount` / `MaxSpawnCount`。`ISpawnOnRoundStart` 的 `MinSpawnCount`/`MaxSpawnCount` 只控制管理器的自动生成；`CustomSpawnPoints` 为空时角色/波次仍会生成，但不会传送玩家，物品生成仍需要至少一个刷新点。物品实现接口时会在点位置创建原版 `BaseItem`，不会发放到玩家背包。

自动生成提供者若还需要动态条件，可同时实现 `ISpawnCondition`，例如（需 `using LabApi.Features.Wrappers;`）：`public bool CanSpawnOnRoundStart() => Player.List.Count(player => player.IsAlive) >= 5;`。管理器会在生成前检查；波次中的角色也会逐个检查。

### 注册与按来源撤销

```csharp
using Astra.Items;
using Astra.Lifecycle;
using Astra.Roles;
using Astra.Waves;

// 需在 Astra 自动注册完成后调用（如服务器就绪事件）；不要直接在插件 OnEnabled 里注册波次。
var scope = AstraLifecycle.CreateScope("MyPlugin", typeof(ScoutRole).Assembly);

CustomRoleApi.Register(new ScoutRole(), scope);
CustomItemApi.Register(new StimItem(), scope);
CustomWaveApi.Register(new ScoutWave(), scope);

// 插件禁用时：按注册逆序撤销，幂等，且不会触碰插件直接向 LabAPI 订阅的内容。
scope.Dispose();
```

## 环境要求

- Windows
- .NET Framework **4.8.1**
- SCP: Secret Laboratory 专用服务器与 LabAPI
- Python 3（构建脚本）
- Visual Studio Build Tools / 完整 .NET Framework MSBuild

`libs/` 为本地只读依赖目录（游戏与 LabAPI 公开化程序集），已由 `.gitignore` 排除，不随仓库分发；构建前请自行准备该目录。构建不读取本机 Steam 安装目录。

## 服务端部署

Astra 是**库**，不是独立插件；单独把 `Astra.Framework.dll` 放进插件目录不会产生自定义内容，还需要至少一个引用 Astra 的业务插件。

1. 安装带 LabAPI 的 SCP: Secret Laboratory 专用服务器。
2. 从 [Releases](https://github.com/UnkownTC/AstraFramework/releases) 下载 `Astra.Framework.dll`，放入 LabAPI 插件目录（与业务插件同级）。
3. 放入业务插件并启动服务器；Astra 会以最高加载优先级先于业务插件初始化。

Release 只附带 `Astra.Framework.dll`，**不附带任何依赖 DLL**。运行所需的其他程序集按下表准备；标有「服务端自带」的不要复制到插件目录，缺失时应修复服务端或 LabAPI 安装。

| 程序集 | 来源 / 部署说明 |
|---|---|
| `Astra.Framework.dll` | 本 Release 提供 |
| `LabApi.dll` | LabAPI 1.1.7 提供，随 LabAPI 安装 |
| `0Harmony.dll` | Harmony 2.4.2；LabAPI 通常已附带，缺失时补 2.4.2 |
| `Assembly-CSharp.dll`、`Assembly-CSharp-firstpass.dll`、`CommandSystem.Core.dll`、`Mirror.dll` | SCP:SL 服务端 `Managed/` 自带 |
| `UnityEngine.CoreModule.dll`、`UnityEngine.PhysicsModule.dll` | SCP:SL 服务端 `Managed/` 自带 |
| `SecretLabNAudio.Core.dll`、`NAudio.Core.dll` | SCP:SL 服务端音频运行时依赖；缺失时补 2.1.2 / 2.2.1 |
| `HintServiceMeow.dll` | HintServiceMeow 6.0.0，第三方插件；使用提示 / HUD 相关 API 时必须安装，且应使用原版插件，不要使用 Publicized 版 |

- `*-Publicized.dll` 只用于编译期引用，运行时绑定的是服务端原版程序集，不要部署。
- `Astra.Analyzers.dll` 只用于编译期分析，不需要部署。
- `libs/` 与构建输出中的其他 DLL（包括构建时复制的引用副本和 `astratest.exe` / `dispatchprobe.exe` 等探针）都不需要放到服务端。
- 未安装 `HintServiceMeow` 时，框架可以加载，但不要调用 `HintApi` / `HintManager` 等提示相关 API。

## 构建

```bash
# 框架库
python tools/vsbuild.py src/Astra.Framework/Astra.Framework.csproj -p:Configuration=Release -p:Platform=x64

# Roslyn 分析器
python tools/vsbuild.py src/Astra.Analyzers/Astra.Analyzers.csproj -p:Configuration=Release
```

`net481` 工程必须使用完整的 .NET Framework MSBuild；`dotnet build` 对 COM 引用会报 `MSB4803`，因此统一走 `tools/vsbuild.py`。

## 产物

所有编译输出与中间文件统一收进 `artifacts/`：

| 类型 | 路径 |
|---|---|
| 框架主程序集 | `artifacts/build/Astra.Framework/<Platform>/<Configuration>/Astra.Framework.dll` |
| 分析器 | `artifacts/build/Astra.Analyzers/[<Platform>/]<Configuration>/netstandard2.0/Astra.Analyzers.dll` |
| 构建日志 | `artifacts/logs/vsbuild-<时间戳>.log` |

发布时从 `artifacts/build/...` 取 DLL；`artifacts/` 不提交。GitHub Release 只上传 `Astra.Framework.dll`，不附带依赖，部署方式见[服务端部署](#服务端部署)。

## 项目结构

```text
src/Astra.Framework/     框架主程序集（按域分目录）
  Roles/ Teams/ Waves/  自定义角色、阵营、波次
  Items/ Effects/       自定义物品、效果
  Abilities/ HumeShield/技能与护盾扩展
  Events/ Patching/     事件与托管补丁
  Lifecycle/ Positions/ 注册生命周期、位置与刷新点
  Spawning/             回合开始生成调度
  Commands/ Loader/     RA 指令、启动与自动注册
src/Astra.Analyzers/     Roslyn 分析器
docs/                    架构规范与公开 API
libs/                    本地只读依赖（游戏 / LabAPI 公开化程序集，不提交）
tools/                   vsbuild.py、ApiDoc.cs
```

## 文档

- [架构规范](docs/STANDARD.md) — 命名空间、可见性、依赖方向与构建约定
- [公开 API 速查](docs/API.md) — 对外类型与成员清单
- [待补清单](docs/ASTRA-PENDING.md) — 尚未完成的能力与设计缺口
- [协作约定](AGENTS.md) — 提交、构建与交付规范

`docs/API.md` 的生成段由 `dotnet run tools/ApiDoc.cs` 维护；可用 `dotnet run tools/ApiDoc.cs -- --check` 校验文档漂移。

## 版本

当前版本：**8.0.0**

采用 `MAJOR.MINOR.PATCH`：

- 新增公开类型 / 方法：MINOR
- 修改或删除公开签名：MAJOR（需标注 `BREAKING` 并同步 `docs/API.md`）
- 内部实现修复：PATCH

## 许可证

本项目使用 [MIT License](LICENSE)。
