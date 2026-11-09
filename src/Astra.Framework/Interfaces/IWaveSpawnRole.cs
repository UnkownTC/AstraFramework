using System.Collections.Generic;

using Astra.Abstractions;

namespace Astra.Interfaces;

/// <summary>
/// 参与波次刷新的角色契约：声明归属的波次与刷新数量。
/// </summary>
/// <remarks>
/// 波次的 <see cref="CustomWave.Table"/> 由已登记的角色聚合而成，角色是刷新配置的唯一来源。
/// 波次实例跨回合复用（<c>CustomWaveManager.Initialize</c> 一次性创建），因此可直接持有实例引用。
/// </remarks>
public interface IWaveSpawnRole
{
    /// <summary>归属的波次实例列表。</summary>
    IReadOnlyList<CustomWave> Waves { get; }

    /// <summary>在每个归属波次中的刷新数量；<c>-1</c> 表示填充剩余玩家。</summary>
    int WaveSpawnCount { get; }
}
