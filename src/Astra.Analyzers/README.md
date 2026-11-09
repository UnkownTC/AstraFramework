# Astra.Analyzers

针对 Astra 的 Roslyn 分析器，把「运行期静默失效」提前成「编译期报错」。

## 规则一览

| ID | 严重度 | 拦截什么 |
|---|---|---|
| **AZF1001** | Error | `[EventMethod]` 的**实例**方法既不是 `override` 也不是 `virtual` 声明点（`new` 隐藏、普通新增方法） |
| **AZF1002** | Error | `[EventMethod]` 方法签名与目标事件的委托签名不兼容 |
| **AZF1003** | Warning | `[EventMethod]` 方法不是 `public` |
| **AZF1004** | Error | `[EventMethod]` 声明的事件在编译期找不到（名字拼错、类型选错） |
| **AZF2001** | Error | `[ManagedPatchMethod]` 补丁方法不是 `static` |
| **AZF2002** | Error | `[ManagedPatchMethod]` 方法缺少 `[HarmonyPrefix/Postfix/Transpiler/Finalizer]` |
| **AZF2003** | Error | `[ManagedPatchMethod]` 的目标成员在编译期找不到 |
| **AZF2101** | Error | `[ManagedPatch]` 类既没有 `[HarmonyPatch]` 也没实现 `IManualPatch` |
| **AZF3001** | Info | `[PatchApiVersion]` / `[PatchGameVersion]` 标在非补丁成员上（不会生效） |

## 为什么需要

框架的核心机制是「约定 + 反射」，违反约定时**大多在运行期静默跳过**，只留一行日志。这些分析器把常见错误前移到编译期：

```csharp
// AZF1001：new 隐藏不会注册
[EventMethod(typeof(PlayerEvents), nameof(PlayerEvents.Shooting))]
public new void OnExiledShooting(ShootingEventArgs ev) { }   // ❌

// AZF1004：事件名拼错
[EventMethod(typeof(PlayerEvents), "Shootting")]              // ❌
public override void OnExiledShooting(ShootingEventArgs ev) { }

// AZF2001/2002：补丁方法姿势不对
[ManagedPatchMethod(typeof(X), "Update")]
[HarmonyPrefix]
public bool Prefix(X __instance) { }                          // ❌ 少写 static
```

## 使用方式

分析器随 `Astra` 一起构建，目标工程只需引用：

```xml
<ProjectReference Include="..\Astra.Analyzers\Astra.Analyzers.csproj"
                  OutputItemType="Analyzer"
                  ReferenceOutputAssembly="false" />
```

已接入：`Astra`、`Site19.PluginContent`。

## 实现约束

- 目标框架 `netstandard2.0`，只依赖 `Microsoft.CodeAnalysis.CSharp`
- **不引用**任何游戏程序集或 Astra 本体，全部按**特性类型名**匹配
- Harmony 的特性类名不带 `Attribute` 后缀（`HarmonyPatch` / `HarmonyPrefix`），
  因此统一用 `AttributeNames.Is(type, "HarmonyPatch")` 同时兼容两种写法
