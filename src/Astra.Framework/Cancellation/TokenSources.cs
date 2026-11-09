using System.Collections.Generic;

namespace Astra.Cancellation;
    // 命名空间 Astra.CancellationTokenSource 会使同名类型在文件级 using 别名下被遮蔽，
    // 因此别名必须放在命名空间声明内部才能生效。
    using CancellationTokenSource = System.Threading.CancellationTokenSource;
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
using Astra.ServerSpecificSettings;
using Astra.Loader;

    /// <summary>
    /// 各类 <see cref="CancellationTokenSource"/> 的持有者。
    /// </summary>
    internal static class TokenSources
    {
        /// <summary>框架禁用时取消的令牌源。</summary>
        internal static CancellationTokenSource CancelWhenDisabled { get; set; } = new();

        /// <summary>回合重启时取消的令牌源。</summary>
        internal static CancellationTokenSource CancelWhenRestarted { get; set; } = new();

        /// <summary>按玩家 NetworkId 索引的令牌源。</summary>
        internal static Dictionary<uint, CancellationTokenSource> PlayerCancellationTokenSources { get; } = new();
    }

