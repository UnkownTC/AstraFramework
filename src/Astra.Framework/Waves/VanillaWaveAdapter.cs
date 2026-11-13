using System;
using System.Reflection;

namespace Astra.Waves;

/// <summary>
/// 原版波次适配辅助：查询分发状态，或从原版事件源尽量解析波次序号。
/// </summary>
public static class VanillaWaveAdapter
{
    /// <summary>本次刷新是否来自原版波次（转发 <see cref="VanillaWaveDispatch.IsDispatching"/>）。</summary>
    public static bool IsDispatchingRespawnEvent => VanillaWaveDispatch.IsDispatching;

    /// <summary>尝试从原版事件源解析波次序号。</summary>
    /// <param name="source">原版事件源（事件参数、波次对象或索引）。</param>
    /// <param name="waveIndex">解析出的序号；失败为 <c>-1</c>。</param>
    /// <returns>解析成功时返回 <see langword="true"/>。</returns>
    /// <remarks>按「int → WaveIndex/Index 成员 → 嵌套 Wave/RespawnWave/WaveType/Source」的顺序尽力解析，不抛异常。</remarks>
    public static bool TryGetVanillaWave(object? source, out int waveIndex)
    {
        waveIndex = -1;
        if (source is null)
            return false;

        if (source is int index)
        {
            waveIndex = index;
            return true;
        }

        if (TryReadIndex(source, out waveIndex))
            return true;

        foreach (var name in new[] { "Wave", "RespawnWave", "WaveType", "Source" })
        {
            var nested = TryReadMember(source, name);
            if (nested is null || ReferenceEquals(nested, source))
                continue;

            if (TryGetVanillaWave(nested, out waveIndex))
                return true;
        }

        waveIndex = -1;
        return false;
    }

    private static bool TryReadIndex(object source, out int index)
    {
        index = -1;

        foreach (var name in new[] { "WaveIndex", "waveIndex", "Index", "index" })
        {
            if (TryReadMember(source, name) is int value)
            {
                index = value;
                return true;
            }
        }

        return false;
    }

    private static object? TryReadMember(object source, string name)
    {
        try
        {
            var type = source.GetType();
            var property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (property is not null && property.GetIndexParameters().Length == 0)
                return property.GetValue(source);

            var field = type.GetField(name, BindingFlags.Public | BindingFlags.Instance);
            return field?.GetValue(source);
        }
        catch
        {
            return null;
        }
    }
}
