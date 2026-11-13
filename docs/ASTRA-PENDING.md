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

## 4. 待建能力（PluginContent 迁移前置）

以下两项不是缺陷，是 Astra 目前缺失的能力；补齐后 PluginContent 才能脱离 FirearmPack。
**（已于 2026-11-14 在 3.1.0 实现并提交；验收状态见 §5。）**

---

### 4.1 加权波次与调度器（已实现，3.1.0）

**新增文件**：`Waves/IWeightedWave.cs`、`Waves/WeightedWaveAdapter.cs`、`Waves/WaveScheduler.cs`
**复用既有**：`IWaveSorter<in T>`、`DelegatedWaveSorter<T>.Sort()`（已存在，不重复造排序器）

#### 4.1.1 `IWeightedWave`

```csharp
public interface IWeightedWave
{
    int Weight { get; }       // 越大越优先
    int MinPlayers { get; }   // 触发所需最少存活人数
    int MaxPlayers { get; }   // 上限，-1 表示不限
}
```

#### 4.1.2 `WeightedWaveAdapter : IWaveSorter<CustomWave>`

| 规则 | 说明 |
|---|---|
| 权重降序 | `rightWeight.CompareTo(leftWeight)`（负数表示 left 优先） |
| 未实现接口 | 按权重 0 处理 |
| 权重相同 | 按 `CustomWave.Id` 序号稳定排序，避免每次刷新顺序抖动 |
| 单例 | `public static readonly WeightedWaveAdapter Instance`（无状态） |

#### 4.1.3 `WaveScheduler`

| 成员 | 语义 |
|---|---|
| `WaveScheduler(Func<int>? playerCountProvider = null)` | 默认取 `Player.ReadyList.Count()` |
| `int Load(IEnumerable<CustomWave>)` | 覆盖式装载并按权重排序，返回队列长度 |
| `CustomWave? Advance(int? playerCount = null)` | 推进到下一个满足门槛的波次；跳过的不消耗队列语义（游标前进） |
| `CustomWave? Current` | 当前波次 |
| `int Remaining` | 剩余波次 |
| `void Reset()` | 清空 |

**门槛判定**：`MinPlayers <= count`（`MinPlayers<=0` 视为不限）且 `MaxPlayers < 0 || count <= MaxPlayers`。
**职责边界**：只决定「顺序 + 是否需要跳过」，实际生成仍由 `CustomWave.Spawn` / `CustomWaveManager` 执行；
**不接管原版增援刷新**——原版 `RespawnWave` 流程不受本调度器影响，二者互不干扰。

**验收**：3 个权重不同的波次按 Weight 顺序推进；人数不足的波次被跳过且不阻断后续波次。

---

### 4.2 效果叠加（已实现，3.1.0）

**修改文件**：`Effects/CustomEffect.cs`、`Effects/CustomEffectController.cs`

#### 4.2.1 `CustomEffect` 增量

```csharp
public virtual bool CanStack => false;          // 默认不叠加，保持现有行为
public int StackCount { get; private set; } = 1;

public void DisableEffect()
{
    Duration = 0f;
    StackCount = 1;        // ← 新增：禁用时重置层数
    ForceIntensity(0);
}

internal void Stack(float duration)             // ← 新增
{
    StackCount++;
    var scaled = (byte)Mathf.Min(Intensity * StackCount, MaxIntensity);
    SetState(scaled, duration, addDuration: true);
}
```

#### 4.2.2 `CustomEffectController.AddEffect` 增量

```csharp
public void AddEffect(CustomEffect effect)
{
    if (effect is null || _effects.Contains(effect))
        return;

    // 同类效果且支持叠加：不追加实例，改为累加层数 + 抬强度 + 刷新时长
    if (effect.CanStack && GetEffect(effect.GetType()) is { } existing)
    {
        existing.Stack(effect.Duration);
        return;
    }

    _effects.Add(effect);
}
```

| 规则 | 说明 |
|---|---|
| 默认行为不变 | `CanStack = false` 时与现状完全一致（同类效果仍可并存，按实例区分） |
| 叠加强度 | `Intensity * StackCount`，`MaxIntensity` 截断 |
| 叠加时长 | `addDuration: true`，累加而非覆盖 |
| 关闭即清层 | `DisableEffect()` 重置 `StackCount = 1` |

**验收**：`CanStack = true` 的效果连续施加两次后 `StackCount == 2`、强度翻倍（不超上限）、时长累加；`DisableEffect` 后归 1。

---

### 4.4 支援波次池

**依据**：业务侧 5 处调用 `SupportPool` / `SetSupportWaveWeight` / `PickSupportWave` / `GetSupportWavesWithFallback` / `WaveSpawned`。
`WaveScheduler` 只解决「队列推进」，不解决「支援池轮换 + 池空回退」。

**新增文件**：`Waves/SupportWavePool.cs`

| 成员 | 语义 |
|---|---|
| `void Register(CustomWave wave, int weight)` | 登记支援波次；权重默认取 `IWeightedWave.Weight`，未实现则取传入值 |
| `bool SetWeight(string waveId, int weight)` | 调整权重；Id 不存在返回 `false` |
| `IReadOnlyCollection<CustomWave> Waves` | 池内全部波次 |
| `CustomWave? Pick(int playerCount)` | 按权重随机（权重和为分母），跳过不满足 `MinPlayers/MaxPlayers` 的项 |
| `IReadOnlyList<CustomWave> GetWithFallback(int playerCount)` | 池空或全部不满足时返回兜底序列（按权重降序的全部候选），供调用方降级 |
| `event Action<CustomWave>? WaveSpawned` | 波次完成生成后触发，供业务挂钩 |

**新增文件**：`Waves/WaveScheduler` 扩展（同文件内加成员）

| 成员 | 语义 |
|---|---|
| `SupportWavePool Support { get; }` | 内置支援池实例 |
| `CustomWave? PickSupportWave(int playerCount)` | 转发 `Support.Pick`，并记录为 `Current` |

**验收**：两个权重 3:1 的支援波次，100 次取样比例接近 3:1；全部不满足人数门槛时 `GetWithFallback` 返回非空序列。

---

### 4.5 原版波次分发标记

**依据**：业务侧用 `VanillaWaveAdapter.IsDispatchingRespawnEvent` 区分「本次刷新来自原版波次」还是「来自自定义波次」（2 个文件）。

**新增文件**：`Waves/VanillaWaveDispatch.cs`

```csharp
public static class VanillaWaveDispatch
{
    // 当前线程/栈上是否处于原版波次分发过程中
    public static bool IsDispatching { get; }

    // 作用域包装：进入时置位，Dispose 时复位（用 using 包裹原版 RespawnWave 调用）
    public static IDisposable Begin();
}
```

**新增文件**：`Waves/VanillaWaveAdapter.cs`

| 成员 | 语义 |
|---|---|
| `static bool IsDispatchingRespawnEvent` | 转发 `VanillaWaveDispatch.IsDispatching` |
| `static bool TryGetVanillaWave(object source, out int waveIndex)` | 从原版事件源解析波次序号，解析失败返回 `false` |

**实现要求**：`Begin()` 必须是可嵌套的计数器（而不是布尔），避免原版波次内部再触发时提前复位。

**边界约束（重要）**：

| 约束 | 说明 |
|---|---|
| 不拦截 | 本节只提供「读标记」能力；Astra 当前 4 个补丁（`ComponentApply` / `CustomTeamHostility` / `HitscanHitregModuleBase` / `ShotBacktrackData`）**均不触碰增援刷新**，本次也不新增 |
| 谁调用 `Begin()` | 由**业务侧**在其自身的 Harmony 补丁里包裹原版调用；Astra 不注册 respawn 相关补丁 |
| 若需框架代劳 | 只能以「纯观察」形式：postfix / 无副作用 prefix，**不得**修改参数、返回值或取消执行 |

**验收**：`using (VanillaWaveDispatch.Begin()) { ... }` 期间 `IsDispatching` 为 `true`，退出后恢复 `false`；嵌套两层时内层结束后仍为 `true`。

---

### 4.6 `CustomWaveApi` 补执行入口

**依据**：`CustomWaveApi` 目前只有 `Waves` / `Find` / `Register` / `Initialize`，业务侧 2 处直接调 `WaveManager.Spawn`，迁移后无公开方法可用。

**修改文件**：`Waves/CustomWaveApi.cs`

```csharp
/// <summary>按 Id 立即执行波次。</summary>
public static IReadOnlyList<Player> Spawn(string waveId, IEnumerable<Player> players)
    => CustomWaveManager.Find(waveId)?.Spawn(players) ?? [];

/// <summary>立即执行指定波次实例。</summary>
public static IReadOnlyList<Player> Spawn(CustomWave wave, IEnumerable<Player> players)
    => wave?.Spawn(players) ?? [];
```

**职责边界**：只做「查表 + 转发 `CustomWave.Spawn`」，不在 Api 里写生成逻辑。

**验收**：`CustomWaveApi.Spawn("测试波次", players)` 返回实际套用的玩家列表；Id 不存在时返回空集合且不抛异常。

---

### 4.3 影响面与验证

| 项 | 内容 |
|---|---|
| 新增公开类型 | `IWeightedWave`、`WeightedWaveAdapter`、`WaveScheduler`、`SupportWavePool`、`VanillaWaveDispatch`、`VanillaWaveAdapter` |
| 修改公开类型 | `CustomEffect`（+2 成员）、`CustomEffectController`（行为分支）、`CustomWaveApi`（+2 重载） |
| 兼容性 | 非破坏性：`CanStack` 默认 false、`Weight` 接口可选实现 |
| 验证 | Astra 独立编译 0 error；`WaveScheduler` 单测覆盖「权重排序 / 人数跳过 / 队列耗尽」 |
| 版本 | MINOR（3.0.1 → 3.1.0） |

---

## 5. 完成标准

| # | 标准 | 状态 |
|---|---|---|
| 1 | §1 各项完成，Astra 独立编译 0 error | 已完成（3.0.1） |
| 2 | 异常日志能打出完整链路 | 已完成 |
| 3 | 一个方法可标注多个 `[PatchMethod]` 并全部生效 | 已完成（3.0.1） |
| 4 | §4.1 加权波次可排序并跳过人数不足的波次 | 已完成（3.1.0） |
| 5 | §4.2 可叠加效果层数与强度按规格变化 |
| 6 | §4.4 支援池按权重取样，池空可回退 |
| 7 | §4.5 原版波次标记可嵌套且能正确复位 |
| 8 | §4.6 `CustomWaveApi.Spawn` 可用 | 已完成（3.1.0） |
| 6 | 版本号升级（新增能力） | 已完成（3.0.0 → 3.0.1 缺陷修复；3.0.1 → 3.1.0 新增能力） |
