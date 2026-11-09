using Astra;
using Astra.Attributes;
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

namespace Astra.Enums;
    /// <summary>
    /// 取消令牌（CancellationToken）的类别。
    /// </summary>
    public enum CancellationTokenCategory
    {
        /// <summary>框架禁用时取消。</summary>
        FrameworkDisabled,

        /// <summary>回合重启时取消。</summary>
        RoundRestarted
    }

