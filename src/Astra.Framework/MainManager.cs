using LabApi.Events.CustomHandlers;
using LabApi.Features.Console;
using LabApi.Loader;

namespace Astra
{
    /// <summary>
    /// 框架主管理器：在服务器插件全部启用后触发一次自动注册流程。
    /// </summary>
    public class MainManager : CustomEventsHandler
    {
        /// <inheritdoc />
        public override void OnServerPluginsEnabled()
        {
            base.OnServerPluginsEnabled();
            var assemblies = PluginLoader.Plugins.Values;
            if (!AutoRegister.RegisterAll(assemblies))
                return;

            LabApi.Features.Console.Logger.Info("[MainManager] 所有管理器完成加载");
        }

    }
}
