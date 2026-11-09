using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using Astra.Abstractions;
using Astra.Attributes;
using Astra.Interfaces;

using LabApi.Events.CustomHandlers;
using LabApi.Features.Console;

namespace Astra.Systems.EventSystem
{
    /// <summary>自定义事件处理器登记、启停与统一卸载管理器。</summary>
    internal static class EventManager
    {
        private static readonly Dictionary<string, HandlerEntry> _handlers = new Dictionary<string, HandlerEntry>();

        /// <summary>仅按实例登记（角色 / 技能模板）的处理器，用于统一卸载。</summary>
        private static readonly List<CustomEventsHandler> _instanceHandlers = new List<CustomEventsHandler>();

        /// <summary>所有已登记事件处理器的只读状态快照。</summary>
        public static IReadOnlyList<EventHandlerStatus> Handlers => _handlers
            .Select(pair => new EventHandlerStatus(pair.Key, pair.Value.Handler.GetType(), pair.Value.IsEnabled))
            .OrderBy(status => status.Id, StringComparer.OrdinalIgnoreCase).ToArray();

        internal static void Register(Type type)
        {

            // 只实例化一次：原实现连续两次 Activator.CreateInstance，首个实例被丢弃（构造函数副作用执行两次）。
            if (Activator.CreateInstance(type) is not CustomEventsHandler handler)
                return;
            RegisterInstance(handler, type);
        }

        /// <summary>
        /// 用一个已存在的实例登记事件。
        /// </summary>
        /// <remarks>
        /// 角色与技能的事件监听由各自的模板管理器调用本方法，用「唯一模板实例」注册，
        /// 避免为同一类型额外构造第二个实例。
        /// </remarks>
        internal static void RegisterInstance(CustomEventsHandler handler, Type type)
        {
            if (handler is null || type is null)
                return;

            if (handler is CustomAbility or CustomRole)
            {
                CustomHandlersManager.RegisterEvents(handler, type);
                _instanceHandlers.Add(handler);
                return;
            }

            var id = handler is IIdentifiable identifiable ? identifiable.Id : string.Empty;
            if (type.GetCustomAttribute<ManagedEventAttribute>() is { } attr && string.IsNullOrWhiteSpace(id))
                id = attr.Id;
            if (string.IsNullOrWhiteSpace(id))
            { Logger.Error($"[EventManager] {type.FullName} 的事件 Id 为空。"); return; }
            if (_handlers.Keys.Any(key => string.Equals(key, id, StringComparison.OrdinalIgnoreCase)))
            { Logger.Error($"[EventManager] 事件 Id {id} 已存在。"); return; }
            var entry = new HandlerEntry(handler);
            _handlers.Add(id, entry);
            Logger.Info($"[EventManager] {id} 已登记");
            if (type.GetCustomAttribute<DisableAutoRegisterAttribute>() == null)
                TryEnableEntry(id, entry);
        }

        internal static void UnregisterAll()
        {
            foreach (var pair in _handlers.ToArray())
                TryDisableEntry(pair.Key, pair.Value);
            _handlers.Clear();

            // 角色 / 技能模板实例不经 _handlers 管理，需单独注销，避免插件重载后残留订阅。
            foreach (var handler in _instanceHandlers.ToArray())
            {
                try
                { CustomHandlersManager.UnregisterEventsHandler(handler); }
                catch (Exception ex) { Logger.Error($"[EventManager] 模板实例 {handler.GetType().FullName} 注销失败：{ex.Message}"); }
            }
            _instanceHandlers.Clear();
        }

        public static bool TryGetHandlerStatus(string id, out EventHandlerStatus status)
        {
            if (!TryResolveEntry(id, out var resolvedId, out var entry))
            { status = null; return false; }
            status = new EventHandlerStatus(resolvedId, entry.Handler.GetType(), entry.IsEnabled);
            return true;
        }

        public static bool TryEnableHandler(string id) => TryResolveEntry(id, out var resolvedId, out var entry) && TryEnableEntry(resolvedId, entry);
        public static bool TryDisableHandler(string id) => TryResolveEntry(id, out var resolvedId, out var entry) && TryDisableEntry(resolvedId, entry);
        public static void RegisterHandler(string id) => TryEnableHandler(id);
        public static void UnregisterHandler(string id) => TryDisableHandler(id);

        private static bool TryResolveEntry(string id, out string resolvedId, out HandlerEntry entry)
        {
            resolvedId = null;
            entry = null;
            if (string.IsNullOrWhiteSpace(id))
                return false;
            foreach (var pair in _handlers)
                if (string.Equals(pair.Key, id, StringComparison.OrdinalIgnoreCase))
                { resolvedId = pair.Key; entry = pair.Value; return true; }
            return false;
        }

        private static bool TryEnableEntry(string id, HandlerEntry entry)
        {
            if (entry.IsEnabled)
                return false;
            var lifecycleStarted = false;
            try
            {
                if (entry.Handler is ICallOnRegister registerTrigger)
                { registerTrigger.OnRegisterEvents(); lifecycleStarted = true; }
                CustomHandlersManager.RegisterEventsHandler(entry.Handler);
                entry.IsEnabled = true;
                Logger.Info($"[EventManager] {id} 已启用");
                return true;
            }
            catch (Exception ex)
            {
                if (lifecycleStarted && entry.Handler is ICallOnUnregister unregisterTrigger)
                    try
                    { unregisterTrigger.OnUnregisterEvents(); }
                    catch (Exception cleanup) { Logger.Error($"[EventManager] {id} 清理失败：{cleanup}"); }
                Logger.Error($"[EventManager] {id} 启用失败：{ex}");
                return false;
            }
        }

        private static bool TryDisableEntry(string id, HandlerEntry entry)
        {
            if (!entry.IsEnabled)
                return false;
            var success = true;
            var unregistered = true;
            try
            { CustomHandlersManager.UnregisterEventsHandler(entry.Handler); }
            catch (Exception ex) { success = false; unregistered = false; Logger.Error($"[EventManager] {id} 注销失败：{ex}"); }
            try
            { if (entry.Handler is ICallOnUnregister trigger) trigger.OnUnregisterEvents(); }
            catch (Exception ex) { success = false; Logger.Error($"[EventManager] {id} 注销回调失败：{ex}"); }
            // LabAPI 注销失败时保留“启用”状态，避免管理面板误报并允许稍后重试。
            entry.IsEnabled = !unregistered ? true : false;
            Logger.Info($"[EventManager] {id} 已禁用");
            return success;
        }

        private sealed class HandlerEntry
        {
            public CustomEventsHandler Handler { get; }
            public bool IsEnabled { get; set; }
            public HandlerEntry(CustomEventsHandler handler) { Handler = handler; }
        }
    }

    public sealed class EventHandlerStatus
    {
        public string Id { get; }
        public Type HandlerType { get; }
        public string TypeFullName => HandlerType.FullName ?? HandlerType.Name;
        public bool IsEnabled { get; }
        internal EventHandlerStatus(string id, Type handlerType, bool isEnabled) { Id = id; HandlerType = handlerType; IsEnabled = isEnabled; }
    }
}
