using System;
using System.Collections.Generic;
using System.Linq;

using Astra.Items;
using Astra.Positions;
using Astra.Roles;
using Astra.Waves;

using LabApi.Features.Wrappers;

using PlayerRoles;
using PlayerRoles.RoleAssign;

using UnityEngine;

using Logger = LabApi.Features.Console.Logger;

namespace Astra.Spawning;

/// <summary>
/// 回合开始生成管理器：在原版角色分配完成后，读取所有 <see cref="ISpawnable"/>，
/// 按 <see cref="ISpawnable.SpawnOnRoundStart"/>、刷怪点概率与生成数量上下限生成波次角色或物品。
/// </summary>
/// <remarks>
/// <para>
/// 本管理器只处理原版角色分配完成之后的事情，因此不会改变本回合的 SCP 数量：
/// 自定义角色只会替换与自身 <see cref="CustomRole.RoleType"/> 相同的原版角色玩家。
/// </para>
/// <para>
/// 只有 <see cref="ISpawnable.SpawnOnRoundStart"/> 为 <see langword="true"/> 的提供者才会执行。
/// <see cref="ISpawnable.SpawnPoints"/> 为空时，角色/波次仍会生成但不传送；物品仍需要至少一个刷怪点。
/// </para>
/// <para>
/// 除自动收集已注册的 <see cref="CustomWave"/>、<see cref="CustomRole"/>
/// 与 <see cref="CustomItem"/> 外，也可以通过 <see cref="Register"/> 登记额外提供者。
/// </para>
/// </remarks>
public static class RoundStartSpawnManager
{
    private static readonly List<ISpawnable> ExtraProviders = new();
    private static readonly HashSet<Type> WarnedProviderTypes = new();
    private static readonly HashSet<Type> WarnedItemWithoutSpawnPointTypes = new();
    private static bool _initialized;

    /// <summary>管理器是否已订阅回合开始事件。</summary>
    public static bool IsInitialized => _initialized;

    /// <summary>
    /// 订阅原版角色分配完成事件；由框架在自动注册完成后调用，重复调用无副作用。
    /// </summary>
    public static void Initialize()
    {
        if (_initialized)
            return;

        _initialized = true;
        RoleAssigner.OnPlayersSpawned += OnPlayersSpawned;
    }

    /// <summary>
    /// 取消订阅并清空手动登记的提供者；由框架在禁用时调用。
    /// </summary>
    public static void Shutdown()
    {
        if (!_initialized)
            return;

        _initialized = false;
        RoleAssigner.OnPlayersSpawned -= OnPlayersSpawned;
        ExtraProviders.Clear();
        WarnedProviderTypes.Clear();
        WarnedItemWithoutSpawnPointTypes.Clear();
    }

    /// <summary>登记一个额外的生成提供者。</summary>
    /// <param name="provider">生成提供者。</param>
    /// <remarks>重复登记同一实例无副作用；自动注册的内容不需要手动登记。</remarks>
    public static void Register(ISpawnable provider)
    {
        if (provider is null)
            return;

        if (!ExtraProviders.Any(existing => ReferenceEquals(existing, provider)))
            ExtraProviders.Add(provider);
    }

    /// <summary>注销一个手动登记的生成提供者。</summary>
    /// <param name="provider">生成提供者。</param>
    /// <returns>确实移除时返回 <see langword="true"/>。</returns>
    public static bool Unregister(ISpawnable provider) =>
        provider is not null && ExtraProviders.Remove(provider);

    /// <summary>
    /// 立即执行一次回合开始生成；主要用于手动触发或测试。
    /// </summary>
    public static void Run()
    {
        foreach (var provider in CollectProviders())
        {
            if (!provider.SpawnOnRoundStart)
                continue;

            try
            {
                Spawn(provider);
            }
            catch (Exception exception)
            {
                Logger.Error($"[Astra] 回合开始生成 {provider.GetType().FullName} 失败：{exception}");
            }
        }
    }

    private static void OnPlayersSpawned() => Run();

    private static List<ISpawnable> CollectProviders()
    {
        var providers = new List<ISpawnable>();

        foreach (var provider in ExtraProviders)
            AddProvider(providers, provider);

        foreach (var wave in CustomWaveApi.Waves.Values)
            AddProvider(providers, wave);

        foreach (var role in CustomRoleApi.RoleTemplates)
            AddProvider(providers, role);

        foreach (var item in CustomItemApi.Items.Values)
            AddProvider(providers, item);

        return providers;
    }

    private static void AddProvider(ICollection<ISpawnable> providers, object candidate)
    {
        if (candidate is not ISpawnable provider)
            return;

        if (providers.Any(existing => ReferenceEquals(existing, provider)))
            return;

        providers.Add(provider);
    }

    private static void Spawn(ISpawnable provider)
    {
        var maxSpawnCount = provider.MaxSpawnCount;
        if (maxSpawnCount <= 0)
            return;

        var minAmount = Math.Max(0, provider.MinAmount);
        if (minAmount > maxSpawnCount)
            minAmount = maxSpawnCount;

        var spawnPoints = provider.SpawnPoints;
        var hasSpawnPoints = spawnPoints is { Count: > 0 };
        var points = SelectPoints(spawnPoints, maxSpawnCount, minAmount);

        // 非空刷怪点全部未通过概率判定时，本次不生成；空字典表示允许无传送生成。
        if (hasSpawnPoints && points.Count == 0)
            return;

        switch (provider)
        {
            case CustomWave wave:
                SpawnWave(wave, points, maxSpawnCount);
                break;

            case CustomItem item:
                if (points.Count == 0)
                {
                    WarnItemWithoutSpawnPoints(item.GetType());
                    break;
                }

                SpawnItems(item, points);
                break;

            case CustomRole role:
                SpawnRole(role, points, maxSpawnCount);
                break;

            default:
                WarnUnsupportedProvider(provider.GetType());
                break;
        }
    }

    private static List<SpawnPointEntry> SelectPoints(
        IReadOnlyDictionary<SpawnPointEntry, float>? spawnPoints, int maxSpawnCount, int minAmount)
    {
        var selected = new List<SpawnPointEntry>();
        var unselected = new List<SpawnPointEntry>();
        if (spawnPoints is null)
            return selected;

        foreach (var pair in spawnPoints)
        {
            var probability = pair.Value;
            if (float.IsNaN(probability) || probability <= 0f)
                continue;

            var accepted = probability >= 1f || UnityEngine.Random.value <= probability;
            if (accepted)
                selected.Add(pair.Key);
            else
                unselected.Add(pair.Key);
        }

        Shuffle(selected);

        // MinAmount 是下限：概率未选够时，从有效但未命中的点中补足，最多到 MaxSpawnCount。
        var targetMin = Math.Min(minAmount, maxSpawnCount);
        if (selected.Count < targetMin)
        {
            Shuffle(unselected);
            var needed = targetMin - selected.Count;
            for (var index = 0; index < needed && index < unselected.Count; index++)
                selected.Add(unselected[index]);
        }

        Shuffle(selected);

        if (selected.Count > maxSpawnCount)
            selected.RemoveRange(maxSpawnCount, selected.Count - maxSpawnCount);

        return selected;
    }

    private static void SpawnWave(CustomWave wave, IReadOnlyList<SpawnPointEntry> points, int maxSpawnCount)
    {
        var assignments = new List<(CustomRole Role, int Count)>();
        foreach (var pair in wave.Table)
        {
            var role = pair.Key.ResolveCustom();
            if (role is null || pair.Value == 0)
                continue;

            assignments.Add((role, pair.Value));
        }

        if (assignments.Count == 0)
            return;

        Shuffle(assignments);

        var usedPlayers = new HashSet<Player>();
        var hasSpawnPoints = points.Count > 0;
        var spawned = 0;

        foreach (var (role, count) in assignments)
        {
            if (spawned >= maxSpawnCount || (hasSpawnPoints && spawned >= points.Count))
                break;

            var remaining = maxSpawnCount - spawned;
            var wanted = count < 0 ? remaining : Math.Min(count, remaining);
            if (wanted <= 0)
                continue;

            foreach (var player in GetCandidates(role.RoleType, usedPlayers))
            {
                if (wanted <= 0 || spawned >= maxSpawnCount || (hasSpawnPoints && spawned >= points.Count))
                    break;

                if (CustomRoleApi.AddRole(player, role.Id) is null)
                    continue;

                if (hasSpawnPoints)
                    points[spawned].Teleport(player);

                usedPlayers.Add(player);
                spawned++;
                wanted--;
            }
        }
    }

    private static void SpawnRole(CustomRole role, IReadOnlyList<SpawnPointEntry> points, int maxSpawnCount)
    {
        var hasSpawnPoints = points.Count > 0;
        var spawned = 0;
        foreach (var player in GetCandidates(role.RoleType, null))
        {
            if (spawned >= maxSpawnCount || (hasSpawnPoints && spawned >= points.Count))
                break;

            if (CustomRoleApi.AddRole(player, role.Id) is null)
                continue;

            if (hasSpawnPoints)
                points[spawned].Teleport(player);

            spawned++;
        }
    }

    private static void SpawnItems(CustomItem item, IReadOnlyList<SpawnPointEntry> points)
    {
        if (item.BaseItem == ItemType.None)
            return;

        foreach (var point in points)
        {
            if (!point.TryResolve(out var position, out var rotation))
                continue;

            Pickup.Create(item.BaseItem, position, rotation);
        }
    }

    private static List<Player> GetCandidates(RoleTypeId roleType, ISet<Player>? excluded)
    {
        return Player.List
            .Where(player => excluded is null || !excluded.Contains(player))
            .Where(player => player.ReferenceHub.IsAlive())
            .Where(player => CustomRoleApi.GetRole(player) is null)
            .Where(player => roleType == RoleTypeId.None || player.Role == roleType)
            .ToList();
    }

    private static void WarnUnsupportedProvider(Type type)
    {
        if (WarnedProviderTypes.Add(type))
            Logger.Warn($"[Astra] {type.FullName} 实现了 ISpawnable，但管理器不支持该内容类型，已跳过。");
    }

    private static void WarnItemWithoutSpawnPoints(Type type)
    {
        if (WarnedItemWithoutSpawnPointTypes.Add(type))
            Logger.Warn($"[Astra] {type.FullName} 是物品生成提供者，但 SpawnPoints 为空；物品生成需要至少一个刷怪点，已跳过。");
    }

    private static void Shuffle<T>(IList<T> items)
    {
        for (var index = items.Count - 1; index > 0; index--)
        {
            var swapIndex = UnityEngine.Random.Range(0, index + 1);
            (items[index], items[swapIndex]) = (items[swapIndex], items[index]);
        }
    }
}
