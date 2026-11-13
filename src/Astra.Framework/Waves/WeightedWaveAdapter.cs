using System;

namespace Astra.Waves;

/// <summary>
/// 把 <see cref="IWeightedWave"/> 的权重转换为 <see cref="IWaveSorter{T}"/> 的排序器。
/// </summary>
/// <remarks>
/// 规则：权重降序；未实现 <see cref="IWeightedWave"/> 的波次按权重 0；
/// 权重相同时按 <see cref="CustomWave.Id"/> 排序，避免每次刷新顺序抖动。排序器无状态，可直接复用单例。
/// </remarks>
public sealed class WeightedWaveAdapter : IWaveSorter<CustomWave>
{
    /// <summary>无状态单例。</summary>
    public static readonly WeightedWaveAdapter Instance = new();

    private WeightedWaveAdapter()
    {
    }

    /// <inheritdoc />
    public int Compare(CustomWave left, CustomWave right)
    {
        if (ReferenceEquals(left, right))
            return 0;
        if (left is null)
            return 1;
        if (right is null)
            return -1;

        var leftWeight = left is IWeightedWave weightedLeft ? weightedLeft.Weight : 0;
        var rightWeight = right is IWeightedWave weightedRight ? weightedRight.Weight : 0;

        var byWeight = rightWeight.CompareTo(leftWeight);
        return byWeight != 0
            ? byWeight
            : string.Compare(left.Id, right.Id, StringComparison.OrdinalIgnoreCase);
    }
}
