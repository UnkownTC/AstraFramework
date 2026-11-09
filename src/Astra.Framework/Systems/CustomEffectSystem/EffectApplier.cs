using System.Collections.Generic;

using LabApi.Features.Wrappers;

using Astra.Abstractions;
using Astra.Effects;

namespace Astra.Systems.CustomEffectSystem;

/// <summary>
/// 批量施加 / 解除 <see cref="EffectEntry"/> 的入口。
/// </summary>
/// <remarks>
/// 角色套用与收口统一走这里，保证原版效果与自定义效果的处理顺序一致：
/// 施加时按声明顺序，解除时按逆序（后加的先撤）。
/// </remarks>
internal static class EffectApplier
{
    /// <summary>按声明顺序对玩家施加全部效果。</summary>
    /// <param name="player">目标玩家。</param>
    /// <param name="effects">效果集合，可为 <see langword="null"/>。</param>
    public static void ApplyAll(Player player, IReadOnlyList<EffectEntry>? effects)
    {
        if (player is null || player.IsDestroyed || effects is null || effects.Count == 0)
            return;

        for (var i = 0; i < effects.Count; i++)
            effects[i].Apply(player);
    }

    /// <summary>按逆序解除玩家的全部效果。</summary>
    /// <param name="player">目标玩家。</param>
    /// <param name="effects">效果集合，可为 <see langword="null"/>。</param>
    public static void RemoveAll(Player player, IReadOnlyList<EffectEntry>? effects)
    {
        if (player is null || player.IsDestroyed || effects is null || effects.Count == 0)
            return;

        for (var i = effects.Count - 1; i >= 0; i--)
            effects[i].Remove(player);
    }
}
