using System;
using System.Reflection;

using HarmonyLib;

using JetBrains.Annotations;

namespace Astra.Attributes;
    /// <summary>
    /// 将标注的方法（或委托）自动订阅到指定的 .NET 事件。
    /// 框架在自动注册阶段通过 <see cref="AccessTools.DeclaredEvent"/> 解析目标事件并完成订阅，
    /// 框架禁用时自动取消订阅。
    /// </summary>
    /// <remarks>
    /// 同一方法可以标注多次（<c>AllowMultiple = true</c>），每个标注独立订阅一个事件，
    /// 目标事件的委托签名必须与该方法一致。同一事件被重复声明时（派生类重写后再次标注，
    /// 或运行时把继承来的特性复制多份）只会订阅一次，不会重复触发。
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Delegate, AllowMultiple = true, Inherited = true)]
    public class EventMethodAttribute : Attribute
    {
        /// <summary>
        /// 初始化事件订阅特性（静态事件）。
        /// </summary>
        /// <param name="eventType">声明事件的类型。</param>
        /// <param name="eventName">事件名称。</param>
        public EventMethodAttribute(Type eventType, string eventName)
        {
            EventType = eventType;
            EventName = eventName;
            EventInstance = null;
            TargetEvent = AccessTools.DeclaredEvent(eventType, eventName);
        }

        /// <summary>声明事件的类型。</summary>
        internal Type EventType { get; }

        /// <summary>事件名称。</summary>
        internal string EventName { get; }

        /// <summary>事件宿主实例（静态事件为 null）。</summary>
        [CanBeNull]
        internal object EventInstance { get; }

        /// <summary>解析出的目标事件信息。</summary>
        internal EventInfo TargetEvent { get; }

        /// <summary>运行时生成的订阅者委托。</summary>
        internal Delegate Subscriber { get; set; }

        /// <summary>被订阅的方法信息。</summary>
        internal MethodInfo SubscriberMethodInfo { get; set; }
    }

