using System;
using System.Collections.Generic;

namespace Astra.Waves;

/// <summary>
/// 用委托实现的排序器，便于调用方临时指定比较逻辑而不必新建类型。
/// </summary>
/// <typeparam name="T">被排序的波次类型。</typeparam>
public sealed class DelegatedWaveSorter<T> : IWaveSorter<T>
{
    private readonly Comparison<T> _comparison;

    /// <summary>用比较委托构造排序器。</summary>
    /// <param name="comparison">比较委托，语义同 <see cref="IWaveSorter{T}.Compare"/>。</param>
    public DelegatedWaveSorter(Comparison<T> comparison)
    {
        _comparison = comparison ?? throw new ArgumentNullException(nameof(comparison));
    }

    /// <inheritdoc />
    public int Compare(T left, T right) => _comparison(left, right);
}

/// <summary>排序器的便捷扩展。</summary>
public static class WaveSorterExtensions
{
    /// <summary>按排序器的优先级返回一个新列表（靠前者优先）。</summary>
    /// <typeparam name="T">波次类型。</typeparam>
    /// <param name="sorter">排序器。</param>
    /// <param name="waves">候选波次。</param>
    /// <returns>排序后的新列表；<paramref name="waves"/> 为 <see langword="null"/> 时为空列表。</returns>
    public static List<T> Sort<T>(this IWaveSorter<T> sorter, IEnumerable<T> waves)
    {
        var list = waves is null ? new List<T>() : new List<T>(waves);
        if (sorter is not null && list.Count > 1)
            list.Sort((a, b) => sorter.Compare(a, b));
        return list;
    }
}
