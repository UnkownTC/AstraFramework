using System;
using System.Collections.Generic;

using Astra.Abstractions;
using Astra.Effects;
using Astra.Enums;
using Astra.Systems.CustomEffectSystem;

using LabApi.Features.Wrappers;

namespace Astra.Apis;

/// <summary>自定义效果对外 API：效果施加/移除与控制器访问。</summary>
public static class CustomEffectApi
{
    /// <summary>施加一组效果条目。</summary>
    /// <param name="player">目标玩家。</param>
    /// <param name="effects">效果条目。</param>
    public static void Apply(Player player, IReadOnlyList<EffectEntry>? effects) =>
        EffectApplier.ApplyAll(player, effects);

    /// <summary>移除一组效果条目。</summary>
    /// <param name="player">目标玩家。</param>
    /// <param name="effects">效果条目。</param>
    public static void Remove(Player player, IReadOnlyList<EffectEntry>? effects) =>
        EffectApplier.RemoveAll(player, effects);

    /// <summary>取玩家的自定义效果控制器。</summary>
    /// <param name="player">目标玩家。</param>
    /// <param name="controller">控制器实例。</param>
    /// <returns>存在时返回 <see langword="true"/>。</returns>
    public static bool TryGetController(Player player, out CustomEffectController controller) =>
        player.GameObject.TryGetComponent(out controller);

    /// <summary>取或添加一个自定义效果实例。</summary>
    /// <typeparam name="T">效果类型。</typeparam>
    /// <param name="player">目标玩家。</param>
    /// <param name="factory">实例工厂。</param>
    /// <returns>效果实例；控制器不存在时为 <see langword="null"/>。</returns>
    public static T? GetOrAddEffect<T>(Player player, Func<Player, T> factory) where T : CustomEffect =>
        TryGetController(player, out var controller) ? controller.GetOrAddEffect(factory) : null;

    /// <summary>禁用一个自定义效果。</summary>
    /// <param name="player">目标玩家。</param>
    /// <param name="effectType">效果类型。</param>
    public static void Disable(Player player, Type effectType)
    {
        if (TryGetController(player, out var controller))
            controller.DisableEffect(effectType);
    }

    /// <summary>查询原版效果是否可用（版本兼容）。</summary>
    /// <param name="effect">效果类型。</param>
    /// <returns>可用时返回 <see langword="true"/>。</returns>
    public static bool IsAvailable(EffectType effect) => EffectCatalog.IsAvailable(effect);
}
