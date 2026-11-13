namespace Astra.Waves;

/// <summary>
/// 角色在「按波次」粒度声明刷新数量的可选契约：优先于 <see cref="IWaveSpawnRole.WaveSpawnCount"/>。
/// </summary>
/// <remarks>
/// 未实现本接口的角色仍按 <see cref="IWaveSpawnRole.WaveSpawnCount"/> 在所有归属波次中使用同一数量。
/// </remarks>
public interface IWaveSpawnCountProvider : IWaveSpawnRole
{
    /// <summary>获取本角色在指定波次中的刷新数量。</summary>
    /// <param name="wave">目标波次。</param>
    /// <returns>刷新数量；<c>-1</c> 表示填充剩余玩家。</returns>
    int GetWaveSpawnCount(CustomWave wave);
}
