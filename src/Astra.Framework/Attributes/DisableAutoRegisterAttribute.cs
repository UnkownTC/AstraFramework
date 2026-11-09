using System;

namespace Astra.Attributes;
    /// <summary>
    /// 标记一个组件「仅登记、不自动注册」。
    /// <para>应用于事件处理器类时：框架会实例化并记录它，但不会自动调用
    /// <c>CustomHandlersManager.RegisterEventsHandler</c>，需之后通过 <c>EventManager.RegisterHandler</c> 手动启用。</para>
    /// <para>应用于补丁类时：补丁仅被登记，不立即应用，需之后通过 <c>PatchManager.Patch</c> 手动启用。</para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class DisableAutoRegisterAttribute : Attribute
    {
    }

