using System;

using LabApi.Events.CustomHandlers;
using LabApi.Features;
using LabApi.Loader.Features.Plugins;
using LabApi.Loader.Features.Plugins.Enums;

namespace Astra;
    /// <summary>
    /// Azure Framework（晴空框架）的 LabAPI 插件入口。
    /// 加载优先级为 <see cref="LoadPriority.Highest"/>，确保框架基础设施先于依赖它的插件就绪。
    /// </summary>
    public class AstraPlugin : Plugin<Config>
    {
        private static MainManager MainManager { get; } = new();

        /// <summary>
        /// 当前生效的框架配置。
        /// </summary>
        /// <remarks>
        /// 在 <see cref="Enable"/> 中由 LabApi 加载的 <see cref="Plugin{TConfig}.Config"/> 同步过来，
        /// 供各静态管理器（如补丁版本门禁）读取。未启用前为默认值。
        /// </remarks>
        public static Config Configuration { get; private set; } = new();

        /// <inheritdoc />
        public override string Author => "晴空Azure";

        /// <inheritdoc />
        public override string Name => "Azure Framework - 晴空框架";

        /// <inheritdoc />
        public override string Description => "本框架提供了一套标准化、低耦合的架构，用于自动管理自定义事件处理器、Harmony 补丁、倒计时与提示系统，所有组件均支持通过字符串 Id 进行精确控制。";

        /// <inheritdoc />
        public override Version Version { get; } = new(3, 0, 0, 0);

        /// <inheritdoc />
        public override Version RequiredApiVersion => LabApiProperties.CurrentVersion;

        /// <inheritdoc />
        public override LoadPriority Priority => LoadPriority.Highest;

        /// <summary>加载配置后同步到静态访问点，供各管理器读取。</summary>
        public override void LoadConfigs()
        {
            base.LoadConfigs();
            Configuration = Config ?? new Config();
        }

        public override void Enable()
        {
            Configuration = Config ?? new Config();

            // 首次运行或新增字段时把默认值写回文件，确保配置项对使用者可见。
            SaveConfig();

            CustomHandlersManager.RegisterEventsHandler(MainManager);
        }

        /// <summary>禁用时注销主管理器并销毁所有自动注册的资源。</summary>
        public override void Disable()
        {
            CustomHandlersManager.UnregisterEventsHandler(MainManager);
            AutoRegister.UnregisterAll();
        }
    }

