# Astra 待补清单（截至 2026-11-14，基线 2.2.1）

> 本文件只列**当前仍然缺的**。已修项在 §3 留档，避免重复劳动。
> 与 PluginContent 迁移相关的其余能力见 `MIGRATION-PLUGINCONTENT.md`。

---

## 1. 待修（缺陷类，2 项）

### 1.1 `Loader/AutoRegister.cs` — 异常链不可见

| 项 | 内容 |
|---|---|
| 位置 | `Loader/AutoRegister.cs` 第 **57 / 76 / 105** 行 |
| 现状 | `Logger.Error($"... Exception: {ex.Message}")` |
| 问题 | `Activator.CreateInstance` 抛的是 `TargetInvocationException`，`Message` 恒为「Exception has been thrown by the target of an invocation.」，真实原因在 `InnerException` 里，被丢弃 |
| 影响 | 类型注册失败时日志无信息量，排障只能靠猜 |

**改法**：新增展开辅函数，三处日志统一替换。

```csharp
/// <summary>展开异常链，避免 TargetInvocationException 把真实原因藏在内层。</summary>
private static string DescribeException(Exception ex)
{
    var builder = new StringBuilder();
    for (var current = ex; current is not null; current = current.InnerException)
    {
        if (builder.Length > 0)
            builder.Append(" → ");

        builder.Append(current.GetType().Name).Append(": ").Append(current.Message);
    }

    return builder.ToString();
}
```

```csharp
// 替换前
Logger.Error($"[AutoRegister] 注册类型 {type.FullName} 时发生异常，已跳过。Exception: {ex.Message}");

// 替换后
Logger.Error($"[AutoRegister] 注册类型 {type.FullName} 时发生异常，已跳过。{DescribeException(ex)}");
```

涉及三处：程序集枚举失败（57）、类型注册失败（76）、角色注册失败（105）。

**验收**：构造一个构造函数据抛异常的类型，日志包含「外层类型 → 内层类型」两段。

---

### 1.2 `Loader/AutoRegister.cs` 第 184 行 — 单数特性查询

| 项 | 内容 |
|---|---|
| 位置 | `Loader/AutoRegister.cs:184` |
| 现状 | `if (patchMethod.GetCustomAttribute<PatchMethodAttribute>() is not null)` |
| 问题 | `PatchMethodAttribute` 声明为 `AllowMultiple = true`（`Attributes/PatchMethodAttribute.cs:28`），同一方法标注两个 `[PatchMethod]` 时单数 API 抛 `AmbiguousMatchException` |
| 影响 | 与 Site19 侧已修的坑同源；目前只是没人标两个，属**潜在缺陷** |

**改法**：

```csharp
if (patchMethod.GetCustomAttributes<PatchMethodAttribute>().Any())
```

**验收**：同一方法标注两个 `[PatchMethod]`（不同目标）时，注册流程不抛异常，两个补丁都被登记。

---

## 2. 已确认安全（无需改动）

| 位置 | 说明 |
|---|---|
| `Events/EventMethodManager.cs` | 已用复数 API + 按「事件类型::事件名」去重 + `IsAlreadySubscribed` 防重订阅 |
| `Loader/VersionGuard.cs:123` | 单数查询用于 `PatchApiVersion` / `PatchGameVersion`，`AllowMultiple` 为默认 `false`，安全 |
| `Items/CustomItemEventHandler.cs` | 走 `EnhancedEventsHandler` 是 Astra 自有事件总线（`ShotBacktrackDataPatch` → `PlayerEvents.OnShooting`），非缺陷 |

---

## 3. 已修留档（勿重复实现）

| 缺陷 | 修复内容 | 位置 |
|---|---|---|
| Mono 特性继承导致 `AmbiguousMatchException` | 复数取值 + 去重键去重 | `Events/EventMethodManager.GetEffectiveAttributes` |
| 同实例同事件重复订阅 | `IsAlreadySubscribed` 守卫 | `Events/EventMethodManager` |

---

## 4. 与 PluginContent 迁移的衔接

以下三项不在本清单（不是缺陷，是新增能力），进度见 `MIGRATION-PLUGINCONTENT.md`：

| 能力 | 状态 |
|---|---|
| `Roles/RoleBase.cs`（承接 FirearmPack 的 CustomRoleBase 使用面） | 待建 |
| 加权波次（`IWeightedWave` / `WeightedWaveAdapter` / `WaveScheduler`） | 待建 |
| 效果叠加策略（`CanStack` / `StackCount`） | 待建 |

---

## 5. 完成标准

| # | 标准 |
|---|---|
| 1 | §1 两项完成，Astra 独立编译 0 error |
| 2 | 异常日志能打出完整链路 |
| 3 | 一个方法可标注多个 `[PatchMethod]` 并全部生效 |
| 4 | 版本号升 PATCH（2.2.2），提交信息注明两项修复 |
