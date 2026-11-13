namespace Astra.Loader;
    /// <summary>
    /// 表示组件在被框架注销时执行一次清理回调。
    /// </summary>
    /// <remarks>
    /// 事件处理器在取消订阅之后调用；其它自定义内容在对应管理器清空之前调用。基类的默认实现为空，按需覆写即可。
    /// </remarks>
    public interface ICallOnUnregister
    {
        /// <summary>
        /// 在组件从事件系统注销时调用。
        /// </summary>
        void OnUnregisterEvents();
    }

