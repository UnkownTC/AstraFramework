using System;
using System.Collections.Generic;

using Astra;

using LabApi.Features.Wrappers;
using Astra.Attributes;
using Astra.Enums;
using Astra.Extensions;
using Astra.Roles;
using Astra.Teams;
using Astra.Items;
using Astra.Effects;
using Astra.Abilities;
using Astra.HumeShield;
using Astra.Patching;
using Astra.Hints;
using Astra.Audio;
using Astra.Countdown;
using Astra.Cancellation;
using Astra.ServerSpecificSettings;
using Astra.Loader;

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
    /// <summary>当前生效的生成器；由宿主在启动时设置。</summary>
    public static ICustomWaveRoleSpawner? Current { get; set; }

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
