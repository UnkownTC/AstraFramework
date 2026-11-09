using System;
// ReSharper disable MemberCanBePrivate.Global

namespace Astra.Extensions
{
    /// <summary>
    /// 地图环境音相关的扩展方法。
    /// </summary>
    public static class MapExtensions
    {
        private static AmbientSoundPlayer ambientSoundPlayer;

        /// <summary>获取主机（Host）的 <see cref="AmbientSoundPlayer"/> 组件。</summary>
        public static AmbientSoundPlayer AmbientSoundPlayer =>
            ambientSoundPlayer ??= ReferenceHub._hostHub.GetComponent<AmbientSoundPlayer>();

        /// <summary>
        /// Plays a random ambient sound.
        /// </summary>
        public static void PlayAmbientSound() => AmbientSoundPlayer.GenerateRandom();

        /// <summary>
        /// Plays an ambient sound.
        /// </summary>
        /// <param name="id">The id of the sound to play.</param>
        public static void PlayAmbientSound(int id)
        {
            if (id >= AmbientSoundPlayer.clips.Length)
                throw new IndexOutOfRangeException(
                    $"There are only {AmbientSoundPlayer.clips.Length} sounds available.");

            AmbientSoundPlayer.RpcPlaySound(AmbientSoundPlayer.clips[id].index);
        }
    }
}
