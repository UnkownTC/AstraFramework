using Astra.Abstractions;
using Astra.Systems.CustomAbilitySystem;

using LabApi.Features.Wrappers;
// ReSharper disable MemberCanBePrivate.Global

namespace Astra.Extensions
{
    /// <summary>
    /// 针对 <see cref="Player"/> 的技能相关扩展方法。
    /// </summary>
    public static class PlayerExtensions
    {
        /// <summary>获取玩家的技能管理器。</summary>
        public static bool TryGetAbilityManager(this Player player, out CustomAbilityManager manager)
        {
            return CustomAbilityManager.TryGetAbilityManager(player, out manager);
        }

        /// <summary>按泛型类型获取玩家的技能实例。</summary>
        public static bool TryGetPlayerAbility<T>(this Player player, out T ability) where T : CustomAbility, new()
        {
            ability = null;
            return player.TryGetAbilityManager(out var manager) && manager.TryGetAbility(out ability);
        }

        /// <summary>按 Id 获取玩家的技能实例。</summary>
        public static bool TryGetPlayerAbility(this Player player, string id, out CustomAbility ability)
        {
            ability = null;
            return player.TryGetAbilityManager(out var manager) && manager.TryGetAbility(id, out ability);
        }

        /// <summary>按 SettingId 获取玩家的技能实例。</summary>
        public static bool TryGetPlayerAbility(this Player player, int settingId, out CustomAbility ability)
        {
            ability = null;
            return player.TryGetAbilityManager(out var manager) && manager.TryGetAbility(settingId, out ability);
        }
    }
}
