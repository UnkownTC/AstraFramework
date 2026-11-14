using System;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Astra.Lifecycle;

/// <summary>
/// Astra 注册项的生命周期入口：按插件程序集创建作用域，或直接撤销某个来源的注册。
/// </summary>
/// <remarks>
/// <para>
/// 这里管理的是「经 Astra 登记、且能识别来源」的内容：自动注册的事件处理器、设置组、
/// 托管补丁、事件方法、角色/阵营/波次/物品/技能模板，以及通过这些 API 新增的实例注册。
/// 插件直接订阅 LabAPI 事件、直接打 Harmony 补丁、直接操作游戏对象的行为不在此范围。
/// </para>
/// <para>
/// 注册来源按程序集匹配。自动注册的条目会记录类型所在程序集；外部插件可用
/// <see cref="CreateScope(Type, string?)"/> 创建与自身程序集绑定的作用域。
/// </para>
/// </remarks>
public static class AstraLifecycle
{
    /// <summary>
    /// 按插件 Id 与程序集创建注册作用域。
    /// </summary>
    /// <param name="pluginId">插件 Id，用于日志与诊断。</param>
    /// <param name="assembly">插件程序集；为空时使用当前调用方程序集。</param>
    /// <returns>可释放的注册作用域。</returns>
    /// <remarks>未显式传程序集时依赖 <see cref="Assembly.GetCallingAssembly"/>；禁止内联以保证取到调用方程序集。</remarks>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static RegistrationScope CreateScope(string pluginId, Assembly? assembly = null)
    {
        if (string.IsNullOrWhiteSpace(pluginId))
            throw new ArgumentException("插件 Id 不能为空。", nameof(pluginId));

        return new RegistrationScope(RegistrationSource.For(pluginId, assembly ?? Assembly.GetCallingAssembly()));
    }

    /// <summary>
    /// 以锚点类型所在程序集创建注册作用域。
    /// </summary>
    /// <param name="anchorType">用于定位程序集的类型，通常是插件入口类型。</param>
    /// <param name="pluginId">可选插件 Id；为空时使用程序集名。</param>
    /// <returns>可释放的注册作用域。</returns>
    public static RegistrationScope CreateScope(Type anchorType, string? pluginId = null)
    {
        if (anchorType is null)
            throw new ArgumentNullException(nameof(anchorType));

        var resolvedPluginId = string.IsNullOrWhiteSpace(pluginId)
            ? anchorType.Assembly.GetName().Name ?? "plugin"
            : pluginId!;

        return CreateScope(resolvedPluginId, anchorType.Assembly);
    }

    /// <summary>撤销指定程序集在 Astra 登记的全部可撤销内容。</summary>
    /// <param name="assembly">插件程序集。</param>
    /// <returns>实际撤销的登记项数量。</returns>
    public static int UnregisterSource(Assembly assembly) =>
        RegistrationLedger.Unregister(assembly);

    /// <summary>撤销锚点类型所在程序集的全部可撤销内容。</summary>
    /// <param name="anchorType">用于定位程序集的类型。</param>
    /// <returns>实际撤销的登记项数量。</returns>
    public static int UnregisterSource(Type anchorType) =>
        anchorType is null ? 0 : UnregisterSource(anchorType.Assembly);

    /// <summary>按来源撤销登记项；来源带程序集时按程序集匹配。</summary>
    /// <param name="source">注册来源。</param>
    /// <returns>实际撤销的登记项数量。</returns>
    public static int UnregisterSource(RegistrationSource source) =>
        RegistrationLedger.Unregister(source);

    /// <summary>框架内部使用：把注册及其撤销动作登记到台账。</summary>
    internal static void Track(RegistrationSource source, string description, Action cleanup) =>
        RegistrationLedger.Track(source, description, cleanup);
}
