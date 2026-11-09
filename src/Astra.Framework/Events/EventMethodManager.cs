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

        /// <summary>
        /// 扫描实例的公开方法，把带 <see cref="EventMethodAttribute"/> 的实例方法订阅到其声明的事件。
        /// 静态方法不在此处理，统一由 <see cref="AutoRegister"/> 按类型扫描注册。
        /// </summary>
        /// <param name="instance">事件处理器实例。</param>
        public static void Register(object? instance)
        {
            if (instance is null)
                return;

            var type = instance.GetType();
            foreach (var method in type.GetMethods())
            {
                // 静态方法没有实例可绑定，且已在自动注册阶段按类型扫描过，这里跳过。
                if (method.IsStatic)
                    continue;

                Register(method, instance);
            }
        }

        /// <summary>
        /// 将指定方法订阅到其 <see cref="EventMethodAttribute"/> 声明的所有事件。
        /// </summary>
        /// <param name="methodInfo">被订阅的方法。</param>
        /// <param name="instance">方法所属实例（静态方法传 null）。</param>
        /// <remarks>
        /// 一个方法可以标注多个 <see cref="EventMethodAttribute"/>，每个标注订阅一个事件（目标事件
        /// 委托的签名需与该方法一致）。同一事件被重复声明（派生类覆盖时又写了一遍特性、或 Mono
        /// 运行时把继承来的特性复制成两份）只订阅一次——否则处理器会被调用两次。
        /// </remarks>
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

            var attributes = GetEffectiveAttributes(methodInfo);
            foreach (var attribute in attributes)
            {
                if (attribute.TargetEvent is null)
                {
                    Logger.Error($"[EventMethodManager] 无法解析事件 {attribute.EventType?.FullName}.{attribute.EventName}" +
                                 $"（{methodInfo.DeclaringType.Name}.{methodInfo.Name}），已跳过。");
                    continue;
                }

                // 同一实例对同一事件的重复登记直接跳过，保证 Register 幂等。
                if (IsAlreadySubscribed(methodInfo, instance, attribute))
                    continue;

                try
                {
                    var subscriber = Delegate.CreateDelegate(attribute.TargetEvent.EventHandlerType, instance, methodInfo);
                    attribute.SubscriberMethodInfo = methodInfo;
                    attribute.Subscriber = subscriber;
                    attribute.TargetEvent.AddEventHandler(attribute.EventInstance, subscriber);
                    HandlerAttributes.Add(attribute);
                    Logger.Info($"[EventMethodManager] 事件方法 {methodInfo.Name} 已订阅至 {TargetDisplay(attribute)}");
                }
                catch (Exception ex)
                {
                    // 单个事件绑定失败不应影响同一方法上的其余事件。
                    Logger.Error($"[EventMethodManager] 事件方法 {methodInfo.DeclaringType.Name}.{methodInfo.Name} " +
                                 $"订阅 {attribute.EventType?.FullName}.{attribute.EventName} 失败：{ex.Message}");
                    Logger.Error(ex.StackTrace);
                }
            }
        }

        /// <summary>
        /// 读取方法实际生效的 <see cref="EventMethodAttribute"/>，按目标事件去重。
        /// </summary>
        /// <remarks>
        /// <para>
        /// 去重是必需的，原因有两类：
        /// 一是 <see cref="EventMethodAttribute"/> 声明了 <c>Inherited = true</c>，派生类覆盖并重新标注同一事件时，
        /// 反射会同时返回派生类与基类两份声明；
        /// 二是 Mono 运行时在解析继承特性时会把同一声明复制成两份。
        /// </para>
        /// <para>
        /// 去重键是「事件声明类型 + 事件名」，所以同一方法标注的不同事件（即使委托签名一致）都会被保留，
        /// 单个实例方法因此可以同时注册到多个事件。
        /// </para>
        /// </remarks>
        private static EventMethodAttribute[] GetEffectiveAttributes(MethodInfo methodInfo)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var attributes = new List<EventMethodAttribute>();
            foreach (var attribute in methodInfo.GetCustomAttributes<EventMethodAttribute>(inherit: true))
            {
                if (seen.Add(TargetKey(attribute)))
                    attributes.Add(attribute);
            }

            return attributes.ToArray();
        }

        /// <summary>事件目标的去重键：声明类型全名 + 事件名。</summary>
        private static string TargetKey(EventMethodAttribute attribute) =>
            (attribute.EventType?.FullName ?? string.Empty) + "::" + attribute.EventName;

        /// <summary>日志用的「类型.事件」描述。</summary>
        private static string TargetDisplay(EventMethodAttribute attribute) =>
            (attribute.EventType?.FullName ?? "?") + "." + attribute.EventName;

        /// <summary>判断该实例对指定事件是否已经订阅过（同名方法的不同实例各自独立）。</summary>
        private static bool IsAlreadySubscribed(MethodInfo methodInfo, object? instance, EventMethodAttribute attribute)
        {
            var key = TargetKey(attribute);
            foreach (var registered in HandlerAttributes)
            {
                if (registered.Subscriber is null || !object.Equals(registered.SubscriberMethodInfo, methodInfo))
                    continue;
                if (!ReferenceEquals(registered.Subscriber.Target, instance))
                    continue;
                if (string.Equals(TargetKey(registered), key, StringComparison.Ordinal))
                    return true;
            }

            return false;
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

