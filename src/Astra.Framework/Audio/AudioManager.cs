using Astra;
using Astra.Attributes;

using LabApi.Events.CustomHandlers;

using SecretLabNAudio.Core.Pools;
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
using Astra.Countdown;
using Astra.Cancellation;
using Astra.ServerSpecificSettings;
using Astra.Loader;

namespace Astra.Audio;
    /// <summary>
    /// 音频管理器：回合重启时自动归还所有音频播放器到对象池。
    /// </summary>
    [ManagedEvent("Astra - AudioManager")]
    internal class AudioManager : CustomEventsHandler
    {
        /// <inheritdoc />
        public override void OnServerRoundRestarted()
        {
            base.OnServerRoundRestarted();
            foreach (var player in AudioApi.AudioPlayers.Values)
            {
                player.ClearBuffer();
                if (!AudioPlayerPool.IsPooled(player))
                    AudioPlayerPool.Return(player);
            }

            AudioApi.AudioPlayers.Clear();
            AudioApi.Stop();
        }
    }

