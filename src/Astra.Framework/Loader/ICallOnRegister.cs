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

namespace Astra.Loader;
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

