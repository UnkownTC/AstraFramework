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
    /// 表示组件在被注销事件处理器时需要执行一次清理回调。
    /// </summary>
    public interface ICallOnUnregister
    {
        /// <summary>
        /// 在组件从事件系统注销时调用。
        /// </summary>
        void OnUnregisterEvents();
    }

