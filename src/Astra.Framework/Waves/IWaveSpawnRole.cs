using System.Collections.Generic;

using Astra;
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
