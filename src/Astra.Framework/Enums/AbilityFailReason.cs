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
    /// 能力触发失败的原因。
    /// </summary>
    public enum AbilityFailReason
    {
        /// <summary>无失败。</summary>
        None,

        /// <summary>玩家条件不满足。</summary>
        ConditionFail,

        /// <summary>冷却未结束。</summary>
        NotReady
    }

