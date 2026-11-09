using System.Collections.Generic;

namespace Astra.Events;

/// <summary>事件处理器对外 API：状态查询与启停。</summary>
public static class EventApi
{
    /// <summary>全部已登记事件处理器的状态快照。</summary>
    public static IReadOnlyList<EventHandlerStatus> Handlers => EventManager.Handlers;

    /// <summary>按 Id 查询处理器状态。</summary>
    /// <param name="id">处理器 Id。</param>
    /// <param name="status">处理器状态。</param>
    /// <returns>存在时返回 <see langword="true"/>。</returns>
    public static bool TryGetStatus(string id, out EventHandlerStatus status) =>
        EventManager.TryGetHandlerStatus(id, out status);

    /// <summary>启用处理器。</summary>
    /// <param name="id">处理器 Id。</param>
    /// <returns>状态发生变化时返回 <see langword="true"/>。</returns>
    public static bool Enable(string id) => EventManager.TryEnableHandler(id);

    /// <summary>禁用处理器。</summary>
    /// <param name="id">处理器 Id。</param>
    /// <returns>状态发生变化时返回 <see langword="true"/>。</returns>
    public static bool Disable(string id) => EventManager.TryDisableHandler(id);
}
