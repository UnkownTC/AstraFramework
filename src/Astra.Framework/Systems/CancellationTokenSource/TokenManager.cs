using System.Threading;

using Astra.Attributes;
using Astra.Enums;

using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.CustomHandlers;
using LabApi.Features.Wrappers;

namespace Astra.Systems.CancellationTokenSource
{
    // 命名空间与 System.Threading.CancellationTokenSource 同名冲突，别名需放在命名空间声明内部。
    using CancellationTokenSource = System.Threading.CancellationTokenSource;

    /// <summary>
    /// 取消令牌管理器：监听玩家加入 / 离开与回合重启，维护各类 <see cref="CancellationTokenSource"/>。
    /// 通过 <see cref="ManagedEventAttribute"/> 自动注册为事件处理器。
    /// </summary>
    [ManagedEvent("Astra - CancellationTokenManager")]
    internal class TokenManager : CustomEventsHandler
    {
        /// <summary>取消所有令牌并重建令牌源（框架禁用时调用）。</summary>
        internal static void CancelAll()
        {
            TokenSources.CancelWhenDisabled.Cancel();
            TokenSources.CancelWhenRestarted.Cancel();
            foreach (var kvp in TokenSources.PlayerCancellationTokenSources)
            {
                kvp.Value.Cancel();
                TokenSources.PlayerCancellationTokenSources[kvp.Key] = new CancellationTokenSource();
            }

            TokenSources.CancelWhenDisabled = new CancellationTokenSource();
            TokenSources.CancelWhenRestarted = new CancellationTokenSource();
        }

        /// <summary>回合重启时取消所有玩家令牌与回合令牌。</summary>
        internal static void RoundRestartedCancel()
        {
            TokenSources.CancelWhenRestarted.Cancel();
            foreach (var kvp in TokenSources.PlayerCancellationTokenSources)
            {
                kvp.Value.Cancel();
            }
            TokenSources.PlayerCancellationTokenSources.Clear();
        }

        /// <summary>玩家离开时取消并移除其令牌源。</summary>
        internal static void PlayerCancel(Player player)
        {
            if (!TokenSources.PlayerCancellationTokenSources.TryGetValue(player.NetworkId, out var tokenSource))
                return;
            tokenSource.Cancel();
            TokenSources.PlayerCancellationTokenSources.Remove(player.NetworkId);
        }

        /// <summary>按类别获取取消令牌。</summary>
        internal static CancellationToken GetToken(CancellationTokenCategory tokenCategory)
        {
            return tokenCategory switch
            {
                CancellationTokenCategory.FrameworkDisabled => TokenSources.CancelWhenDisabled.Token,
                CancellationTokenCategory.RoundRestarted => TokenSources.CancelWhenRestarted.Token,
                _ => CancellationToken.None
            };
        }

        /// <summary>获取（或创建）指定玩家的取消令牌。</summary>
        internal static CancellationToken GetPlayersToken(Player player)
        {
            if (TokenSources.PlayerCancellationTokenSources.TryGetValue(player.NetworkId, out var tokenSource) &&
                !tokenSource.IsCancellationRequested)
                return tokenSource.Token;
            tokenSource = new CancellationTokenSource();
            TokenSources.PlayerCancellationTokenSources[player.NetworkId] = tokenSource;

            return tokenSource.Token;
        }

        /// <inheritdoc />
        public override void OnPlayerJoined(PlayerJoinedEventArgs ev)
        {
            base.OnPlayerJoined(ev);
            GetPlayersToken(ev.Player);
        }

        /// <inheritdoc />
        public override void OnPlayerLeft(PlayerLeftEventArgs ev)
        {
            base.OnPlayerLeft(ev);
            PlayerCancel(ev.Player);
        }

        /// <inheritdoc />
        public override void OnServerRoundRestarted()
        {
            base.OnServerRoundRestarted();
            RoundRestartedCancel();
        }
    }
}
