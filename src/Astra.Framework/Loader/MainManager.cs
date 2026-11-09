using LabApi.Events.CustomHandlers;
using LabApi.Features.Console;
using LabApi.Loader;
using Astra;
using Astra.Attributes;
using Astra.Enums;
using Astra.Extensions;
using Astra.Roles;
using Astra.Teams;
using Astra.Waves;
using Astra.Items;
using Astra.Effects;
using Astra.Abilities;
using Astra.HumeShield;
using Astra.Patching;
using Astra.Hints;
using Astra.Audio;
using Astra.Countdown;
using Astra.Cancellation;
using Astra.ServerSpecificSettings;
using Astra.Loader;

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
