using System;
using System.Collections.Generic;

using Astra.Effects;
using Astra.Enums;
using Astra.Systems.CustomEffectSystem;

using CustomPlayerEffects;

using LabApi.Features.Wrappers;

namespace Astra.Extensions;

/// <summary>
/// 以 <see cref="EffectType"/> 为入口的玩家状态效果操作。
/// </summary>
/// <remarks>
/// <para>
/// 与 <c>LabApi.Helpers</c> 的同类扩展相比，这里不再为每个效果写一个 <c>case</c>：
/// 先由 <see cref="EffectCatalog"/> 把枚举解析成类型，再在玩家的效果表中定位实例，
/// 最后统一走 LabApi 的非泛型开关接口。
/// </para>
/// <para>
/// 因而不受泛型约束影响，也无需为游戏新增效果改代码；解析不到的枚举会被安全忽略。
/// </para>
/// </remarks>
public static class PlayerEffectExtensions
{
    /// <summary>
    /// 效果类型 → 在 <c>AllEffects</c> 中的下标缓存。
    /// </summary>
    /// <remarks>
    /// 所有玩家的效果表由同一 prefab 构建，下标一致，故可全局缓存；
    /// 命中后仍会校验该下标的实际类型，一旦对不上就退回线性扫描并回填，保证正确性。
    /// 只在主线程访问，无需加锁。
    /// </remarks>
    private static readonly Dictionary<Type, int> IndexByType = new();

    /// <summary>在玩家身上定位指定效果实例。</summary>
    /// <param name="player">目标玩家。</param>
    /// <param name="effect">效果枚举。</param>
    /// <returns>效果实例；玩家无效、枚举无实现或该效果不存在时为 <see langword="null"/>。</returns>
    public static StatusEffectBase? FindEffect(this Player player, EffectType effect)
    {
        if (player is null || player.IsDestroyed || effect == EffectType.None)
            return null;

        var type = EffectCatalog.Resolve(effect);
        if (type is null)
            return null;

        var effects = player.ReferenceHub.playerEffectsController.AllEffects;

        // 快路径：命中缓存且下标处类型对得上。
        if (IndexByType.TryGetValue(type, out var known) && known < effects.Length)
        {
            var cached = effects[known];
            if (cached is not null && cached.GetType() == type)
                return cached;
        }

        // 慢路径：线性扫描并回填下标。
        for (var i = 0; i < effects.Length; i++)
        {
            var candidate = effects[i];
            if (candidate is not null && candidate.GetType() == type)
            {
                IndexByType[type] = i;
                return candidate;
            }
        }

        IndexByType.Remove(type);
        return null;
    }

    /// <summary>启用（或刷新）一个效果。</summary>
    /// <param name="player">目标玩家。</param>
    /// <param name="effect">效果枚举。</param>
    /// <param name="intensity">强度。</param>
    /// <param name="duration">持续时间（秒），0 表示沿用效果自身默认值。</param>
    /// <param name="addDuration">是否在剩余时长上累加。</param>
    public static void EnableEffect(this Player player, EffectType effect, byte intensity = 1,
        float duration = 0f, bool addDuration = false)
    {
        var instance = player.FindEffect(effect);
        if (instance is null)
            return;

        player.EnableEffect(instance, intensity, duration, addDuration);
    }

    /// <summary>关闭一个效果。</summary>
    /// <param name="player">目标玩家。</param>
    /// <param name="effect">效果枚举。</param>
    public static void DisableEffect(this Player player, EffectType effect)
    {
        var instance = player.FindEffect(effect);
        if (instance is null)
            return;

        player.DisableEffect(instance);
    }

    /// <summary>尝试取得玩家的指定效果实例。</summary>
    /// <param name="player">目标玩家。</param>
    /// <param name="effect">效果枚举。</param>
    /// <param name="instance">效果实例。</param>
    /// <returns>存在则为 <see langword="true"/>。</returns>
    public static bool TryGetEffect(this Player player, EffectType effect, out StatusEffectBase? instance)
    {
        instance = player.FindEffect(effect);
        return instance is not null;
    }

    /// <summary>取得玩家的指定效果视图。</summary>
    /// <param name="player">目标玩家。</param>
    /// <param name="effect">效果枚举。</param>
    /// <returns>效果视图；不存在时为 <see langword="null"/>。</returns>
    public static Effect? GetEffect(this Player player, EffectType effect)
    {
        var instance = player.FindEffect(effect);
        return instance is null ? null : new Effect(instance);
    }

    /// <summary>取得玩家当前所有生效中的效果视图。</summary>
    /// <param name="player">目标玩家。</param>
    /// <returns>效果视图列表；玩家无效时为空。</returns>
    public static IReadOnlyList<Effect> GetEffects(this Player player)
    {
        if (player is null || player.IsDestroyed)
            return [];

        var result = new List<Effect>();
        foreach (var active in player.ActiveEffects)
        {
            if (active is not null)
                result.Add(new Effect(active));
        }

        return result;
    }

    /// <summary>读取效果强度。</summary>
    /// <param name="player">目标玩家。</param>
    /// <param name="effect">效果枚举。</param>
    /// <returns>强度；效果不存在时为 0。</returns>
    public static byte GetEffectIntensity(this Player player, EffectType effect) =>
        player.FindEffect(effect)?.Intensity ?? 0;

    /// <summary>修改效果强度；<paramref name="duration"/> 大于 0 时同时续期。</summary>
    /// <param name="player">目标玩家。</param>
    /// <param name="effect">效果枚举。</param>
    /// <param name="intensity">新强度。</param>
    /// <param name="duration">持续时间（秒），0 表示不改时长。</param>
    public static void ChangeEffectIntensity(this Player player, EffectType effect, byte intensity,
        float duration = 0f)
    {
        var instance = player.FindEffect(effect);
        if (instance is null)
            return;

        instance.Intensity = intensity;
        if (duration > 0f)
            instance.Duration = duration;
    }
}
