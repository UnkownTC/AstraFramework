using System;
using System.Collections.Generic;

using LabApi.Features.Wrappers;

namespace Astra.Waves;

/// <summary>
/// 波次刷新角色的抽象出口：把「自定义角色 Id + 玩家」交给宿主实现。
/// </summary>
/// <remarks>
/// 框架本身不认识角色注册表（角色系统可能来自 Astra 的 <c>CustomRoleManager</c>，
/// 也可能来自外部框架），因此以委托形式注入，保持 <see cref="CustomWave"/> 零耦合。
/// </remarks>
public interface ICustomWaveRoleSpawner
{
    /// <summary>把玩家套用为指定自定义角色。</summary>
    /// <param name="roleId">自定义角色 Id。</param>
    /// <param name="player">目标玩家。</param>
    /// <returns>成功套用返回 <see langword="true"/>。</returns>
    bool TrySpawn(string roleId, Player player);
}

/// <summary>波次角色生成器的静态注册点。</summary>
internal static class CustomWaveRoleSpawner
{
    private static readonly List<ICustomWaveRoleSpawner> Spawners = [];

    /// <summary>首个生效的生成器（兼容旧 API）；没有生成器时为 <see langword="null"/>。</summary>
    public static ICustomWaveRoleSpawner? Current => Spawners.Count > 0 ? Spawners[0] : null;

    /// <summary>是否至少注册了一个生成器。</summary>
    public static bool HasAny => Spawners.Count > 0;

    /// <summary>替换全部生成器（兼容旧 <c>SetRoleSpawner</c> 语义）。</summary>
    /// <param name="spawner">生成器；<see langword="null"/> 表示清空。</param>
    public static void Set(ICustomWaveRoleSpawner? spawner)
    {
        Spawners.Clear();
        if (spawner is not null)
            Spawners.Add(spawner);
    }

    /// <summary>追加生成器；按注册顺序依次尝试。</summary>
    /// <param name="spawner">生成器。</param>
    public static void Add(ICustomWaveRoleSpawner spawner)
    {
        if (spawner is not null && !Spawners.Contains(spawner))
            Spawners.Add(spawner);
    }

    /// <summary>按注册顺序尝试生成角色，首个成功即结束。</summary>
    /// <param name="roleId">角色 Id。</param>
    /// <param name="player">目标玩家。</param>
    /// <returns>任一生成器成功时返回 <see langword="true"/>。</returns>
    public static bool TrySpawn(string roleId, Player player)
    {
        foreach (var spawner in Spawners)
        {
            if (spawner.TrySpawn(roleId, player))
                return true;
        }

        return false;
    }

    /// <summary>用委托快速构造生成器。</summary>
    /// <param name="trySpawn">套用实现。</param>
    /// <returns>生成器实例。</returns>
    public static ICustomWaveRoleSpawner FromDelegate(Func<string, Player, bool> trySpawn) =>
        new DelegateSpawner(trySpawn);

    /// <summary>委托包装实现。</summary>
    private sealed class DelegateSpawner(Func<string, Player, bool> trySpawn) : ICustomWaveRoleSpawner
    {
        /// <inheritdoc />
        public bool TrySpawn(string roleId, Player player) => trySpawn(roleId, player);
    }
}

/// <summary>波次角色统计：某个波次刷新了哪些角色、各多少。</summary>
/// <remarks>
/// 与 <see cref="CustomWave.Table"/> 同构，用于「角色反向声明自己属于哪个波次」时聚合。
/// </remarks>
public sealed class WaveComposition
{
    private readonly Dictionary<string, int> _counts = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>累加一个角色的刷新数量。</summary>
    /// <param name="roleId">角色 Id。</param>
    /// <param name="count">数量；<c>-1</c> 表示填充剩余。</param>
    public void Add(string roleId, int count)
    {
        if (string.IsNullOrWhiteSpace(roleId))
            return;

        _counts[roleId] = _counts.TryGetValue(roleId, out var existing) ? existing + count : count;
    }

    /// <summary>导出为只读字典。</summary>
    /// <returns>角色 Id → 数量。</returns>
    public IReadOnlyDictionary<string, int> ToTable() => _counts;
}
