using System.Collections.Generic;
using System.Linq;

using LabApi.Features.Wrappers;

using SecretLabNAudio.Core;
using SecretLabNAudio.Core.Extensions;
using SecretLabNAudio.Core.Extensions.Processors;
using SecretLabNAudio.Core.Extensions.Providers;
using SecretLabNAudio.Core.Pools;
using SecretLabNAudio.Core.SendEngines;

using UnityEngine;

namespace Astra.Apis
{
    /// <summary>
    /// 音频播放对外静态 API，基于 SecretLabNAudio。
    /// </summary>
    public static class AudioApi
    {
        private static HashSet<AudioPlayer> GlobalAudioPlayer { get; } = new();

        /// <summary>按玩家 NetworkId 索引的正在播放的音频播放器。</summary>
        internal static Dictionary<uint, AudioPlayer> AudioPlayers { get; } = new();

        /// <summary>
        /// 向指定玩家播放音频。
        /// </summary>
        /// <param name="player">目标玩家。</param>
        /// <param name="file">音频文件路径。</param>
        /// <param name="volume">音量（0~1）。</param>
        /// <returns>音频播放器 Id；创建失败返回 0。</returns>
        public static byte PlayAudio(Player player, string file, float volume = 1f)
        {
            if (!StreamProcessorExtensions.TryCreateFromFile(file, false, out var processor))
                return 0;
            processor.Volume(volume);
            var audioPlayer = AudioPlayerPool.RentGloballyAudible(player.Position, player.GameObject?.transform)
                .PoolOnEnd()
                .Use(processor);
            audioPlayer.SendEngine = new SpecificPlayerSendEngine(player);
            AudioPlayers[player.NetworkId] = audioPlayer;
            return audioPlayer.Id;
        }

        /// <summary>
        /// 向所有玩家全局播放音频。
        /// </summary>
        /// <param name="file">音频文件路径。</param>
        /// <param name="volume">音量（0~1）。</param>
        /// <returns>音频播放器 Id；创建失败返回 0。</returns>
        public static byte PlayAudio(string file, float volume = 1f)
        {
            if (!StreamProcessorExtensions.TryCreateFromFile(file, false, out var processor))
                return 0;
            processor.Volume(volume);
            var audioPlayer = AudioPlayerPool.RentGloballyAudible(Vector3.zero)
                .PoolOnEnd()
                .Use(processor);
            GlobalAudioPlayer.Add(audioPlayer);
            return audioPlayer.Id;
        }

        /// <summary>
        /// 停止指定玩家正在播放的音频。
        /// </summary>
        /// <param name="player">目标玩家。</param>
        public static void Stop(Player player)
        {
            if (AudioPlayers.TryGetValue(player.NetworkId, out var audioPlayer) && !audioPlayer.HasEnded)
            {
                Stop(audioPlayer);
            }
            AudioPlayers.Remove(player.NetworkId);
        }

        /// <summary>
        /// 按 Id 停止全局音频。
        /// </summary>
        /// <param name="id">音频播放器 Id。</param>
        public static void Stop(byte id)
        {
            var player = GlobalAudioPlayer.FirstOrDefault(p => p.Id == id);
            if (player is null)
                return;
            Stop(player);
        }

        /// <summary>
        /// 停止并归还指定的音频播放器到对象池。
        /// </summary>
        /// <param name="player">音频播放器。</param>
        public static void Stop(AudioPlayer player)
        {
            player.ClearBuffer();
            if (!AudioPlayerPool.IsPooled(player))
                AudioPlayerPool.Return(player);
        }

        /// <summary>
        /// 停止所有全局音频。
        /// </summary>
        public static void Stop()
        {
            foreach (var player in GlobalAudioPlayer)
            {
                Stop(player);
            }
            GlobalAudioPlayer.Clear();
        }
    }
}
