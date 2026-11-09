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
    /// 提示（Hint）在屏幕上的显示位置。
    /// </summary>
    public enum HintPosition : byte
    {
        /// <summary>屏幕中央。</summary>
        Center,

        /// <summary>屏幕左侧。</summary>
        Left,

        /// <summary>屏幕右侧。</summary>
        Right,

        /// <summary>中上部。</summary>
        CenterTop,

        /// <summary>中上偏左。</summary>
        CenterTopLeft,

        /// <summary>中上偏右。</summary>
        CenterTopRight,

        /// <summary>中下部。</summary>
        CenterBottom,

        /// <summary>中下偏左。</summary>
        CenterBottomLeft,

        /// <summary>中下偏右。</summary>
        CenterBottomRight,

        /// <summary>顶部。</summary>
        Top,

        /// <summary>顶部偏左。</summary>
        TopLeft,

        /// <summary>顶部偏右。</summary>
        TopRight,

        /// <summary>底部。</summary>
        Bottom,

        /// <summary>底部偏左。</summary>
        BottomLeft,

        /// <summary>底部偏右。</summary>
        BottomRight,

        /// <summary>自定义位置（需自行处理坐标）。</summary>
        Custom
    }

