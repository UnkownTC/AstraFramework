using Astra.Interfaces;

namespace Astra.Abstractions
{
    /// <summary>
    /// 服务器专属设置（ServerSpecificSettings）的抽象基类。
    /// 继承该类并按 Id 返回一组设置项，框架会自动统一下发给客户端。
    /// </summary>
    public abstract class ServerSpecificSettingBase : IIdentifiable
    {
        /// <summary>获取该设置组的全局唯一标识。</summary>
        public abstract string Id { get; }

        /// <summary>获取该设置组包含的服务器专属设置项。</summary>
        public abstract UserSettings.ServerSpecific.ServerSpecificSettingBase[] ActiveSettings { get; }

        /// <summary>获取设置组的排序优先级（数值越小越靠前，-1 表示排最后）。</summary>
        public virtual int Priority { get; } = -1;
    }
}
