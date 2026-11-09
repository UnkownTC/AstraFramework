# AstraFramework 框架规范 v2.0

| 项 | 值 |
|---|---|
| 状态 | 定稿方向（v2.0：域内聚命名空间，取消 `Systems` / `Abstractions` 分层） |
| 日期 | 2026-11-09 |
| 适用范围 | 独立仓库 `AstraFramework` |
| 兼容性 | **不考虑 Site19 各 Plugin 兼容**，一次性洗牌 |

---

## 1. 设计原则

| # | 原则 | 含义 |
|---|---|---|
| 1 | **按域内聚** | 一个业务域的全部类型（基类 + 管理器 + 值对象）放在同一命名空间，按「我要做一个角色」找代码，而不是按「抽象/实现」找 |
| 2 | **默认公开** | 应被业务调用的类型一律 `public`；只有「框架自身调度」才 `internal` |
| 3 | **调用链一跳** | 业务 → 管理器，无门面层 |
| 4 | **无分层命名空间** | 不存在 `Systems` / `Abstractions` / `Features` / `Core` 这类「按技术角色」的中间层 |

---

## 2. 命名空间总表

| 命名空间 | 职责 | 典型类型 |
|---|---|---|
| `Astra` | 插件入口与配置 | `AstraPlugin`、`Configuration` |
| `Astra.Loader` | 启动引导、自动注册、版本守卫 | `AutoRegister`(i)、`VersionGuard`(i) |
| `Astra.Roles` | 自定义角色 | `CustomRole`、`StandardRole`、`CustomRoleManager`、`CustomRoleTemplateManager`(i) |
| `Astra.Teams` | 自定义阵营 | `CustomTeam`、`CustomTeamManager`、`TeamRelation`、`RelationEntry` |
| `Astra.Waves` | 自定义波次 | `CustomWave`、`CustomWaveManager`、`IWaveRoleSpawner` |
| `Astra.Items` | 自定义物品 | `CustomItem`、`CustomFirearm`、`CustomMicroHid`、`CustomJailbird`、`CustomParticleDisruptor`、`CustomItemManager` |
| `Astra.Effects` | 效果系统 | `CustomEffect`、`EffectEntry`、`EffectType`、`EffectApplier`、`EffectCatalog`(i) |
| `Astra.Abilities` | 技能系统 | `CustomAbility`、`StandardAbility`、`CustomAbilityManager` |
| `Astra.HumeShield` | 护盾驱动 | `ICustomHumeShieldStat`、`CustomHumeShieldManager` |
| `Astra.Patching` | 托管补丁 | `PatchManager`、`PatchStatus`、`PatchMethodAttribute`、`ManagedPatchAttribute` |
| `Astra.Events` | 事件目录与参数 | `PlayerEvents`、`ShootingEventArgs`、`EventManager` |
| `Astra.Hints` / `Astra.Audio` / `Astra.Countdown` | 表现能力 | `HintManager`、`AudioManager`、`CountdownManager` |
| `Astra.Enums` | 跨域共享枚举 | `KeybindType`、`HintPosition`、`AbilityFailReason` |
| `Astra.Attributes` | 跨域共享特性 | `EventMethodAttribute`、`ManagedEventAttribute`、`PatchApiVersionAttribute` |
| `Astra.Extensions` | 公共扩展方法 | `PlayerEffectExtensions` |

**已删除的命名空间**：`Systems`、`Systems.*`、`Abstractions`、`Apis`、`Interfaces`、`Core`、`Waves`(旧)。

---

## 3. 目录结构与命名空间同构

```
src/Astra.Framework/
├── AstraPlugin.cs                    → Astra
├── Configuration.cs                  → Astra
├── Loader/                           → Astra.Loader
│   ├── AutoRegister.cs               (internal)
│   └── VersionGuard.cs               (internal)
├── Roles/                            → Astra.Roles
│   ├── CustomRole.cs
│   ├── StandardRole.cs
│   ├── CustomRoleManager.cs
│   └── CustomRoleTemplateManager.cs  (internal)
├── Teams/                            → Astra.Teams
│   ├── CustomTeam.cs
│   ├── CustomTeamManager.cs
│   ├── CustomTeamRelation.cs
│   └── RelationEntry.cs
├── Waves/                            → Astra.Waves
│   ├── CustomWave.cs
│   ├── CustomWaveManager.cs
│   └── IWaveRoleSpawner.cs
├── Items/                            → Astra.Items
│   ├── CustomItem.cs
│   ├── CustomFirearm.cs
│   ├── CustomMicroHid.cs
│   ├── CustomJailbird.cs
│   ├── CustomParticleDisruptor.cs
│   └── CustomItemManager.cs
├── Effects/                          → Astra.Effects
│   ├── CustomEffect.cs
│   ├── EffectEntry.cs
│   ├── EffectApplier.cs
│   └── EffectCatalog.cs              (internal)
├── Abilities/                        → Astra.Abilities
├── HumeShield/                       → Astra.HumeShield
├── Patching/                         → Astra.Patching
│   ├── PatchManager.cs
│   ├── PatchStatus.cs
│   └── PatchLoader.cs                (internal)
├── Events/                           → Astra.Events
│   ├── PlayerEvents.cs
│   ├── Arguments/ShootingEventArgs.cs
│   └── EventManager.cs
├── Enums/ Attributes/ Extensions/    → 同名
├── Hints/ Audio/ Countdown/          → Astra.Hints / Audio / Countdown
└── Patches/                          → Astra（框架自带补丁，internal 类）
```

规则：**一目录一命名空间，目录名 = 命名空间末段**，不再有二级技术分层目录。

---

## 4. 公开 / 收敛决策

### 4.1 公开（业务需要调用或继承）

| 类别 | 类型 |
|---|---|
| 基类与契约 | `CustomRole`、`StandardRole`、`CustomTeam`、`CustomWave`、`CustomItem` 及四个派生基类、`CustomEffect`、`CustomAbility`、`StandardAbility` |
| 接口 | `IWaveRoleSpawner`、`ICustomHumeShieldStat`、`ICustomHumeShieldBlocker`、`IIdentifiable`、`IAbilityRole`、`IEffects`、`IKeybind`、`ICooldown`、`ICallOnRegister`、`ICallOnUnregister` |
| 管理器 | `CustomRoleManager`、`CustomTeamManager`、`CustomWaveManager`、`CustomItemManager`、`CustomAbilityManager`、`CustomHumeShieldManager`、`PatchManager`、`EventManager`、`EffectApplier`、`HintManager`、`AudioManager`、`CountdownManager` |
| 值对象 / DTO | `EffectEntry`、`RelationEntry`、`CustomTeamRelation`、`PatchStatus`、`EventHandlerStatus`、`WaveComposition` |
| 事件 | `PlayerEvents`、全部 `*EventArgs` |
| 标注与枚举 | `Astra.Attributes.*`、`Astra.Enums.*` |

### 4.2 收敛（框架内部）

| 类型 | 原因 |
|---|---|
| `AutoRegister`、`VersionGuard`、`PatchLoader` | 启动期调度，业务不应调用 |
| `CustomRoleTemplateManager`、`AbilityTemplateManager` | 模板注册由扫描驱动，手动注册没有意义 |
| `EffectCatalog`、`CustomEffectController`、`CustomItemEventHandler`、`CustomHumeShieldEventHandler` | 内部映射/驱动器，业务通过 `EffectApplier` / 基类钩子使用 |
| 框架自带补丁类（`CustomTeamHostilityPatch` 等） | 实现细节，通过 `PatchManager` 启停 |
| 任何缓存、索引表、反射工具 | 无外部价值 |

### 4.3 判定准则

```
业务是否需要「调用 / 继承 / 实现 / 读取」？
├─ 是 → public
└─ 否 → 它是否只服务于框架调度？
        ├─ 是 → internal
        └─ 否（业务可能将来需要） → public，但 XML 注明「非稳定 API」
```

---

## 5. 依赖规则

| 允许 | 禁止 |
|---|---|
| 域内自由引用（`Astra.Roles` 内部任意） | 域间静态方法互调（`Astra.Roles` → `Astra.Items`） |
| 任意域 → `Astra.Enums` / `Astra.Attributes` / `Astra.Extensions` | 任意域 → `Astra.Loader`（除 Loader 自身与入口） |
| 任意域 → `Astra.Events.Arguments`（只读事件数据） | 事件参数 → 任意域（只能是纯数据 + 基类引用） |
| `Astra.Abstractions`（已取消）| `CustomRole` 等基类反向调用业务域管理器 |

**跨域协作方式**（按优先级）：

1. `Astra.Loader` 的注册顺序（Team → Wave → Item → Role）
2. `Astra.Events` 事件（如角色套用时广播）
3. 显式依赖注入（管理器构造函数接收对方管理器引用，不写死静态调用）

---

## 6. 程序集与包

| 程序集 | 打包 |
|---|---|
| `Astra.Framework.dll` | NuGet `AstraFramework` |
| `Astra.Analyzers.dll` | NuGet `AstraFramework.Analyzers`（`developmentDependency`） |
| `Astra.Testing.dll` | NuGet `AstraFramework.Testing`（可选） |

版本 `MAJOR.MINOR.PATCH`；公开类型签名变更 → MAJOR。

---

## 7. Analyzer 规则

| ID | 内容 | 级别 |
|---|---|---|
| AST1001~1004 | `[EventMethod]` 签名 / override / 可访问性 / 事件存在 | Error |
| AST2001~2003 | `[PatchMethod]` 静态 / 补丁类型 / 目标存在 | Error |
| AST2101 | `[ManagedPatch]` 缺目标声明 | Error |
| AST3001 | 版本特性用在非补丁成员 | Warning |
| AST5001~5005 | 契约范围（事件基类 / 实例化契约 / 静态类） | Error |
| **AST6101** | 一个域 `using` 了另一个 `Astra.<域>`（跨域静态依赖） | Warning |
| **AST6102** | 公开类型的成员暴露了 `internal` 类型（应为编译器错误，给出更明确提示） | Error |

**废弃**：AZF4001（禁外引 Systems）、AST6001/6002（随 `Systems` 消失）。

---

## 8. 洗牌迁移步骤（Astra 仓库内，一次完成）

| 阶段 | 动作 | 校验 |
|---|---|---|
| **P0** | 新建仓库，拷贝 `AzureFramework` + `Analyzer`，程序集名改 `Astra.Framework` / `Astra.Analyzers`，根命名空间改 `Astra` | 编译 0 error |
| **P1** | 目录平铺：`Systems/*System/*` → 顶层域目录；`Abstractions/*` → 对应域目录 | 编译 0 error |
| **P2** | 命名空间改写：每个文件按所在目录设置（无 `Systems`/`Abstractions` 段） | `grep "Astra.Systems\|Astra.Abstractions"` 为空 |
| **P3** | 删除全部 `Apis/*.cs`，逻辑合回 Manager（仅转发的直接删） | 无 `*Api` 类 |
| **P4** | 可见性审计：按 §4 逐类型调整；internal 类型加 `internal` 修饰并确认无 public 泄漏 | 编译器 + AST6102 零告警 |
| **P5** | Analyzer 前缀 `AZF` → `AST`，新增 6101/6102，删除废弃规则 | 规则自测 |
| **P6** | `docs/` 三件套 + `1.0.0-alpha.1` 打包 | 干净项目试用 |

---

## 9. 验收标准

| # | 标准 |
|---|---|
| 1 | 代码中不存在 `Systems` / `Abstractions` / `Apis` 命名空间段 |
| 2 | 每个业务域一个命名空间，目录与命名空间一一对应 |
| 3 | 无 `*Api` 门面类，业务到实现一跳 |
| 4 | §4.1 清单内类型全部 `public` 且有 XML 文档 |
| 5 | §4.2 清单内类型全部 `internal`，且未出现在任何 public 签名中 |
| 6 | 跨域静态依赖告警（AST6101）为零 |
| 7 | `-warnaserror` 编译通过 |
| 8 | 干净项目引用 NuGet 包后可注册并套用一个自定义角色 |

---

## 10. 待决事项

| # | 问题 | 建议 |
|---|---|---|
| 1 | `Astra.Testing` 首发 | 首发，提供 Fake 基类 |
| 2 | 管理器是否需要实例化版本 | 保持 `static`（与现状一致，改动最小） |
| 3 | `Astra.Runtime`（运行时上下文）是否单列 | 暂不单列，入口类型留在 `Astra` |
