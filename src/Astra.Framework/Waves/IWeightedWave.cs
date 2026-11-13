namespace Astra.Waves;

/// <summary>
/// 波次的权重与人数门槛：供 <see cref="WeightedWaveAdapter"/> 与 <see cref="WaveScheduler"/> 使用。
/// </summary>
/// <remarks>未实现本接口的 <see cref="CustomWave"/> 按权重 0、无人数门槛处理。</remarks>
public interface IWeightedWave
{
    /// <summary>权重；越大越优先。</summary>
    int Weight { get; }

    /// <summary>触发所需最少存活人数；<c>&lt;= 0</c> 表示不限。</summary>
    int MinPlayers { get; }

    /// <summary>触发允许的最大存活人数；<c>&lt; 0</c> 表示不限。</summary>
    int MaxPlayers { get; }
}
