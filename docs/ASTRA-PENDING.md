# Astra 待补清单（截至 2026-11-14，基线 3.0.1）

> 本文件只列**当前仍然缺的**。已修项在 §3 留档，避免重复劳动。
> 与 PluginContent 迁移相关的其余能力见 `MIGRATION-PLUGINCONTENT.md`。

---

## 1. 待修（缺陷类）

（无）

---

## 2. 已确认安全（无需改动）

| 位置 | 说明 |
|---|---|
| `Events/EventMethodManager.cs` | 已用复数 API + 按「事件类型::事件名」去重 + `IsAlreadySubscribed` 防重订阅 |
| `Loader/VersionGuard.cs:123` | 单数查询用于 `PatchApiVersion` / `PatchGameVersion`，`AllowMultiple` 为默认 `false`，安全 |
| `Items/CustomItemEventHandler.cs` | 走 `EnhancedEventsHandler` 是 Astra 自有事件总线（`ShotBacktrackDataPatch` → `PlayerEvents.OnShooting`），非缺陷 |
| `Loader/AutoRegister.cs`（`ManagedPatchAttribute` / `ManagedEventAttribute` / `DisableAutoRegisterAttribute`） | 均为单次标注（`AllowMultiple` 默认 `false`），单数查询安全 |

---

## 3. 已修留档（勿重复实现）

| 缺陷 | 修复内容 | 位置 |
|---|---|---|
| Mono 特性继承导致 `AmbiguousMatchException` | 复数取值 + 去重键去重 | `Events/EventMethodManager.GetEffectiveAttributes` |
| 同实例同事件重复订阅 | `IsAlreadySubscribed` 守卫 | `Events/EventMethodManager` |
| 注册异常被 `TargetInvocationException` 吞掉内层原因 | 新增 `DescribeException` 展开 `InnerException` 链，三处注册日志统一替换 | `Loader/AutoRegister.cs` |
| 类上多个 `[HarmonyPatch]` 时单数查询歧义 | 改用 `type.IsDefined(typeof(HarmonyPatch), false)` | `Loader/AutoRegister.cs` |
| 同一方法多个 `[PatchMethod]` 时抛 `AmbiguousMatchException` / 只登记一个目标 | `AutoRegister` 改用 `IsDefined`；`PatchManager.PatchMethod` 遍历全部特性、逐个目标应用与登记（登记键含目标） | `Loader/AutoRegister.cs`、`Patching/PatchManager.cs` |

---

## 4. 与 PluginContent 迁移的衔接

以下两项不在本清单（不是缺陷，是新增能力），进度见 `MIGRATION-PLUGINCONTENT.md`：

| 能力 | 状态 |
|---|---|
| 加权波次（`IWeightedWave` / `WeightedWaveAdapter` / `WaveScheduler`） | 待建 |
| 效果叠加策略（`CanStack` / `StackCount`） | 待建 |

---

## 5. 完成标准

| # | 标准 | 状态 |
|---|---|---|
| 1 | §1 各项完成，Astra 独立编译 0 error | 已完成（3.0.1） |
| 2 | 异常日志能打出完整链路 | 已完成 |
| 3 | 一个方法可标注多个 `[PatchMethod]` 并全部生效 | 已完成 |
| 4 | 版本号升 PATCH | 已完成（3.0.0 → 3.0.1；原计划 2.2.2 因基线已到 3.0.0 顺延） |
