namespace Astra.Interfaces
{
    /// <summary>
    /// 表示组件在被注册为事件处理器时需要执行一次初始化回调。
    /// </summary>
    public interface ICallOnRegister
    {
        /// <summary>
        /// 在组件被注册到事件系统时调用。
        /// </summary>
        void OnRegisterEvents();
    }
}
