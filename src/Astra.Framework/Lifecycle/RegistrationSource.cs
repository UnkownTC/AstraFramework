using System;
using System.Reflection;

namespace Astra.Lifecycle;

/// <summary>
/// 一次注册的来源标识。框架用它把注册内容与插件程序集关联起来，供按来源撤销使用。
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Assembly"/> 是实际撤销时的匹配键；<see cref="PluginId"/> 只用于日志与诊断。
/// 一个程序集里可能有多个插件，或一个插件跨多个程序集，因此来源只保证「程序集级」隔离，
/// 不保证严格的插件级隔离。
/// </para>
/// </remarks>
public readonly struct RegistrationSource : IEquatable<RegistrationSource>
{
    /// <summary>来源插件 Id；未显式提供时为空字符串。</summary>
    public string PluginId { get; }

    /// <summary>来源程序集；未能解析时为 <see langword="null"/>。</summary>
    public Assembly? Assembly { get; }

    /// <summary>是否具备可撤销的来源（必须能解析到程序集）。</summary>
    public bool IsValid => Assembly is not null;

    /// <summary>创建一个来源标识。</summary>
    /// <param name="pluginId">插件 Id，可为空。</param>
    /// <param name="assembly">来源程序集。</param>
    public RegistrationSource(string? pluginId, Assembly? assembly)
    {
        PluginId = pluginId?.Trim() ?? string.Empty;
        Assembly = assembly;
    }

    /// <summary>按程序集创建来源。</summary>
    /// <param name="assembly">来源程序集。</param>
    /// <param name="pluginId">可选插件 Id。</param>
    /// <returns>来源标识。</returns>
    public static RegistrationSource FromAssembly(Assembly assembly, string? pluginId = null) =>
        new(pluginId, assembly);

    /// <summary>按类型所在程序集创建来源。</summary>
    /// <param name="type">用于定位程序集的类型。</param>
    /// <param name="pluginId">可选插件 Id。</param>
    /// <returns>来源标识。</returns>
    public static RegistrationSource FromType(Type type, string? pluginId = null) =>
        new(pluginId, type?.Assembly);

    /// <summary>按插件 Id 与可选程序集创建来源。</summary>
    /// <param name="pluginId">插件 Id。</param>
    /// <param name="assembly">来源程序集。</param>
    /// <returns>来源标识。</returns>
    public static RegistrationSource For(string? pluginId, Assembly? assembly = null) =>
        new(pluginId, assembly);

    /// <summary>判断来源是否指向指定程序集。</summary>
    /// <param name="assembly">目标程序集。</param>
    /// <returns>同一程序集时返回 <see langword="true"/>。</returns>
    public bool Matches(Assembly? assembly) =>
        assembly is not null && ReferenceEquals(Assembly, assembly);

    /// <inheritdoc />
    public bool Equals(RegistrationSource other) =>
        ReferenceEquals(Assembly, other.Assembly) &&
        string.Equals(PluginId, other.PluginId, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is RegistrationSource other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var assemblyHash = Assembly is null ? 0 : Assembly.GetHashCode();
        return (assemblyHash * 397) ^ StringComparer.Ordinal.GetHashCode(PluginId);
    }

    /// <inheritdoc />
    public override string ToString()
    {
        var assemblyName = Assembly?.GetName().Name ?? "<unknown>";
        return string.IsNullOrWhiteSpace(PluginId) ? assemblyName : $"{PluginId} ({assemblyName})";
    }
}
