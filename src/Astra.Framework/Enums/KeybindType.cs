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
    /// 按键能力的触发类型。
    /// </summary>
    public enum KeybindType
    {
        /// <summary>按下时触发。</summary>
        Press,

        /// <summary>按住式（松开时触发）。</summary>
        Hold
    }

