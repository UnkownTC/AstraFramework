using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using Astra.Attributes;

using JetBrains.Annotations;

using LabApi.Features.Console;

namespace Astra.Events;
    /// <summary>
    /// 事件方法管理器：负责将带 <see cref="EventMethodAttribute"/> 的方法订阅到对应 .NET 事件，
    /// 并在框架禁用时统一取消订阅。
    /// </summary>
    public static class EventMethodManager
    {
        private static List<EventMethodAttribute> HandlerAttributes { get; } = new();

        public static void Register(object? instance)
        {
            if (instance is null)
                return;
            var type = instance.GetType();
            var methods = type.GetMethods().Where(m => m.GetCustomAttribute<EventMethodAttribute>() is not null);
            foreach (var method in methods)
            {
                Register(method, instance);
            }
        }

        /// <summary>
        /// 将指定方法订阅到其 <see cref="EventMethodAttribute"/> 声明的所有事件。
        /// </summary>
        /// <param name="methodInfo">被订阅的方法。</param>
        /// <param name="instance">方法所属实例（静态方法传 null）。</param>
        public static void Register(MethodInfo methodInfo, object? instance = null)
        {
            if (methodInfo?.DeclaringType is null)
                return;

            // 实例方法必须携带实例；否则会生成 Target 为 null 的委托（事件触发时崩在处理器里）。
            if (!methodInfo.IsStatic && instance is null)
            {
                Logger.Error($"[EventMethodManager] {methodInfo.DeclaringType.Name}.{methodInfo.Name} " +
                             "是实例方法但未提供实例，已跳过。静态方法的 [EventMethod] 才会走类型扫描注册。");
                return;
            }
            var attrs = methodInfo.GetCustomAttributes<EventMethodAttribute>().ToArray();
            if (!attrs.Any())
                return;
            try
            {
                foreach (var attr in attrs)
                {
                    var subscriber = Delegate.CreateDelegate(attr.TargetEvent.EventHandlerType, instance, methodInfo);
                    attr.SubscriberMethodInfo = methodInfo;
                    attr.Subscriber = subscriber;
                    attr.TargetEvent.AddEventHandler(attr.EventInstance, subscriber);
                    HandlerAttributes.Add(attr);
                    Logger.Info($"[EventMethodManager] 事件方法 {methodInfo.Name} 已订阅至 {attr.EventType.FullName}.{attr.EventName}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"An exception has been thrown when subscribe event. Exception: {ex.Message}");
                Logger.Error(ex.StackTrace);
            }
        }

        /// <summary>取消订阅指定方法（可选按事件过滤）。</summary>
        /// <param name="methodInfo">被订阅的方法。</param>
        /// <param name="eventInfo">可选，指定要取消订阅的具体事件。</param>
        public static void Unregister(MethodInfo methodInfo, [CanBeNull] EventInfo eventInfo = null)
        {
            if (methodInfo is null)
                return;
            try
            {
                // 先物化快照再遍历，避免在遍历过程中修改 HandlerAttributes 导致集合被修改异常。
                var attrs = HandlerAttributes
                    .Where(h => h.SubscriberMethodInfo == methodInfo && (eventInfo is null || h.TargetEvent == eventInfo))
                    .ToArray();
                foreach (var attr in attrs)
                {
                    attr.TargetEvent.RemoveEventHandler(attr.EventInstance, attr.Subscriber);
                    HandlerAttributes.Remove(attr);
                    Logger.Info(
                        $"[EventMethodManager] 事件方法 {methodInfo.Name} 已从 {attr.EventType.FullName}.{attr.EventName} 取消订阅");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"An exception has been thrown when unsubscribe event. Exception: {ex.Message}");
                Logger.Error($"StackTrace: {ex.StackTrace}");
            }
        }

        /// <summary>取消订阅所有已登记的事件方法。</summary>
        public static void UnregisterAll()
        {
            foreach (var attr in HandlerAttributes)
            {
                try
                {
                    attr.TargetEvent.RemoveEventHandler(attr.EventInstance, attr.Subscriber);
                }
                catch (Exception ex)
                {
                    Logger.Error($"An exception has been thrown when unsubscribe event. Exception: {ex.Message}");
                    Logger.Error($"StackTrace: {ex.StackTrace}");
                }
            }
            HandlerAttributes.Clear();
        }
    }

