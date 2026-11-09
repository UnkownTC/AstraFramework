using Astra.Enums;
using Astra;
using Astra.Attributes;
using Astra.Extensions;
using Astra.Roles;
using Astra.Teams;
using Astra.Waves;
using Astra.Items;
using Astra.Effects;
using Astra.HumeShield;
using Astra.Patching;
using Astra.Hints;
using Astra.Audio;
using Astra.Countdown;
using Astra.Cancellation;
using Astra.ServerSpecificSettings;
using Astra.Loader;

namespace Astra.Abilities;
    /// <summary>
    /// 表示一个通过按键触发的能力。
    /// </summary>
    public interface IKeybind
    {
        /// <summary>获取该能力绑定的服务器专属设置 SettingId（对应 SSKeybindSetting）。</summary>
        int SettingId { get; }

        /// <summary>获取按键触发类型（按下 / 按住）。</summary>
        KeybindType KeybindType { get; }

        /// <summary>获取或设置按键当前是否被按下。</summary>
        bool SyncIsPressed { get; set; }

        /// <summary>按键按下时调用。</summary>
        void KeyDown();

        /// <summary>按键松开时调用。</summary>
        void KeyUp();
    }

