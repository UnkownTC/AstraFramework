namespace Astra.Loader;
    /// <summary>
    /// 表示组件在被框架登记时执行一次初始化回调。
    /// </summary>
    /// <remarks>
    /// 事件处理器在订阅到事件系统之后调用；其它自定义内容（角色 / 技能 / 物品 / 阵营 / 波次）
    /// 在模板登记到对应管理器之后调用。基类的默认实现为空，按需覆写即可。
    /// </remarks>
    public interface ICallOnRegister
    {
        /// <summary>
        /// 在组件被注册到事件系统时调用。
        /// </summary>
        void OnRegisterEvents();
    }

