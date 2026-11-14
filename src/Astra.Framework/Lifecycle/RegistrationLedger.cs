using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using LabApi.Features.Console;

namespace Astra.Lifecycle;

/// <summary>
/// 注册台账：记录「哪个来源注册了什么」以及对应的撤销动作。
/// </summary>
/// <remarks>
/// 撤销动作只在台账里保存，不反向要求各管理器认识来源；这样能覆盖现有注册 API，
/// 同时让各管理器保持无来源字段。撤销时按注册逆序执行，先清依赖方再清被依赖方。
/// </remarks>
internal static class RegistrationLedger
{
    private static readonly object Gate = new();
    private static readonly List<Entry> Entries = [];

    /// <summary>登记一条注册及其撤销动作。</summary>
    internal static void Track(RegistrationSource source, string description, Action cleanup)
    {
        if (!source.IsValid || cleanup is null)
            return;

        lock (Gate)
            Entries.Add(new Entry(source, description, cleanup));
    }

    /// <summary>按程序集撤销全部登记项。</summary>
    internal static int Unregister(Assembly assembly) =>
        assembly is null ? 0 : Unregister(RegistrationSource.FromAssembly(assembly));

    /// <summary>按来源撤销登记项；来源带程序集时按程序集匹配。</summary>
    internal static int Unregister(RegistrationSource source)
    {
        if (!source.IsValid)
            return 0;

        Entry[] selected;
        lock (Gate)
        {
            selected = Entries.Where(entry => Matches(entry.Source, source)).ToArray();
            foreach (var entry in selected)
                Entries.Remove(entry);
        }

        // 逆序：后注册的先撤销。调用 cleanup 时不持锁，避免撤销动作再次注册时死锁。
        var removed = 0;
        for (var i = selected.Length - 1; i >= 0; i--)
        {
            var entry = selected[i];
            try
            {
                entry.Cleanup();
                removed++;
            }
            catch (Exception ex)
            {
                Logger.Error($"[AstraLifecycle] 撤销 {entry.Description} 失败：{ex.Message}");
            }
        }

        if (removed > 0)
            Logger.Info($"[AstraLifecycle] 已按来源 {source} 撤销 {removed} 项注册");

        return removed;
    }

    /// <summary>清空台账（框架整体卸载时调用，撤销动作由各管理器统一执行）。</summary>
    internal static void Clear()
    {
        lock (Gate)
            Entries.Clear();
    }

    private static bool Matches(RegistrationSource entrySource, RegistrationSource query)
    {
        // 以装配体为隔离键：RegistrationScope 的 PluginId 只用于展示，
        // 自动注册的条目没有 PluginId，仍应被同一程序集的 scope 撤销。
        return query.Assembly is not null
            ? ReferenceEquals(entrySource.Assembly, query.Assembly)
            : entrySource.Equals(query);
    }

    private sealed class Entry
    {
        public Entry(RegistrationSource source, string description, Action cleanup)
        {
            Source = source;
            Description = description;
            Cleanup = cleanup;
        }

        public RegistrationSource Source { get; }

        public string Description { get; }

        public Action Cleanup { get; }
    }
}
