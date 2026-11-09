using System.Threading;

using Astra.Enums;
using Astra.Cancellation;

using LabApi.Features.Wrappers;
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
using Astra.ServerSpecificSettings;
using Astra.Loader;

namespace Astra.Cancellation;
    /// <summary>
    /// 取消令牌对外静态 API，内部委托给 <see cref="TokenManager"/>。
    /// </summary>
    public static class CancellationTokenApi
    {
        /// <summary>
        /// 获取与指定玩家绑定的取消令牌，玩家离开时自动取消。
        /// </summary>
        /// <param name="player">目标玩家。</param>
        public static CancellationToken GetPlayersToken(Player player)
        {
            return TokenManager.GetPlayersToken(player);
        }

        /// <summary>
        /// 获取回合重启时取消的令牌。
        /// </summary>
        public static CancellationToken GetRoundRestartToken()
        {
            return TokenManager.GetToken(CancellationTokenCategory.RoundRestarted);
        }

        /// <summary>
        /// 获取框架禁用时取消的令牌。
        /// </summary>
        public static CancellationToken GetFrameworkDisableToken()
        {
            return TokenManager.GetToken(CancellationTokenCategory.FrameworkDisabled);
        }
    }

