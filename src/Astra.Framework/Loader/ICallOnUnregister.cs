namespace Astra.Loader;
    /// <summary>
    /// 表示组件在被注销事件处理器时需要执行一次清理回调。
    /// </summary>
    public interface ICallOnUnregister
    {
        /// <summary>
        /// 在组件从事件系统注销时调用。
        /// </summary>
        void OnUnregisterEvents();
    }

