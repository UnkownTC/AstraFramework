using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Astra.Extensions;

/// <summary>集合随机取值扩展。</summary>
public static class CollectionExtensions
{
    private static readonly ThreadLocal<Random> _threadLocalRandom =
        new(() => new Random(Guid.NewGuid().GetHashCode()));

    private static int GetRandomNumber(int maximum, int minimum = 0) =>
        _threadLocalRandom.Value.Next(minimum, maximum);

    /// <summary>从只读列表随机取一个元素。</summary>
    /// <typeparam name="T">元素类型。</typeparam>
    /// <param name="list">源列表。</param>
    /// <returns>随机元素；空列表返回默认值。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="list"/> 为 <see langword="null"/>。</exception>
    public static T? GetRandom<T>(this IReadOnlyList<T> list)
    {
        if (list == null)
            throw new ArgumentNullException(nameof(list));

        if (list.Count == 0)
            return default;

        return list[GetRandomNumber(list.Count)];
    }

    /// <summary>从可枚举序列随机取一个元素；非集合序列会先物化为数组。</summary>
    /// <typeparam name="T">元素类型。</typeparam>
    /// <param name="enumerable">源序列。</param>
    /// <returns>随机元素；空序列返回默认值。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="enumerable"/> 为 <see langword="null"/>。</exception>
    public static T? GetRandom<T>(this IEnumerable<T> enumerable)
    {
        if (enumerable == null)
            throw new ArgumentNullException(nameof(enumerable));

        if (enumerable is IReadOnlyCollection<T> collection)
        {
            if (collection.Count == 0)
                return default;

            return collection.ElementAt(GetRandomNumber(collection.Count));
        }

        var array = enumerable.ToArray();
        if (array.Length == 0)
            return default;

        return array[GetRandomNumber(array.Length)];
    }

    /// <summary>从数组随机取一个元素。</summary>
    /// <typeparam name="T">元素类型。</typeparam>
    /// <param name="array">源数组。</param>
    /// <returns>随机元素；空数组返回默认值。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="array"/> 为 <see langword="null"/>。</exception>
    public static T? GetRandom<T>(this T[] array)
    {
        if (array == null)
            throw new ArgumentNullException(nameof(array));

        if (array.LongLength == 0)
            return default;

        return array[GetRandomNumber(array.Length)];
    }
}
