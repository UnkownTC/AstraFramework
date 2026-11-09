using System;
using System.Collections.Generic;
using System.Linq;

using Astra.Enums;

using CustomPlayerEffects;

using Logger = LabApi.Features.Console.Logger;
using Astra;
using Astra.Attributes;
using Astra.Extensions;
using Astra.Roles;
using Astra.Teams;
using Astra.Waves;
using Astra.Items;
using Astra.Abilities;
using Astra.HumeShield;
using Astra.Patching;
using Astra.Hints;
using Astra.Audio;
using Astra.Countdown;
using Astra.Cancellation;
using Astra.ServerSpecificSettings;
using Astra.Loader;

namespace Astra.Effects;

/// <summary>
/// <see cref="EffectType"/> 与游戏效果类型之间的映射表。
/// </summary>
/// <remarks>
/// <para>
/// 不维护全量手写字典：启动时扫描 <c>StatusEffectBase</c> 的全部非抽象子类，
/// 按「枚举名 == 类型名」自动配对；只有命名不一致的少数项才需要在这里登记例外。
/// </para>
/// <para>
/// 好处是游戏更新新增效果时，只要类型名与枚举成员同名就自动生效，
/// 不再出现「枚举有、映射表没有、开关静默失效」的情况。
/// </para>
/// </remarks>
internal static class EffectCatalog
{
    /// <summary>枚举名与类型名不一致的例外项。</summary>
    private static readonly Dictionary<EffectType, string> NameOverrides = new()
    {
        [EffectType.Blinded] = "Blindness",
        [EffectType.SinkHole] = "Sinkhole",
        [EffectType.Marshmallow] = "MarshmallowEffect",
        [EffectType.Scp559] = "Scp559Effect",
        [EffectType.TraumatizedEvil] = "TraumatizedByEvil",
    };

    private static readonly Dictionary<EffectType, Type> TypeByEffect;
    private static readonly Dictionary<Type, EffectType> EffectByType;

    static EffectCatalog()
    {
        var typesByName = new Dictionary<string, Type>(StringComparer.Ordinal);
        foreach (var type in typeof(StatusEffectBase).Assembly.GetTypes())
        {
            if (type.IsAbstract || !typeof(StatusEffectBase).IsAssignableFrom(type))
                continue;

            // 同名类型理论上不存在；真出现时保留第一个并告警，避免抛异常打断启动。
            if (typesByName.TryGetValue(type.Name, out var existing))
            {
                if (existing != type)
                    Logger.Warn($"[EffectCatalog] 存在同名效果类型，已保留 {existing.FullName}，忽略 {type.FullName}。");
                continue;
            }

            typesByName[type.Name] = type;
        }

        TypeByEffect = new Dictionary<EffectType, Type>();
        var unresolved = new List<EffectType>();

        foreach (EffectType effect in Enum.GetValues(typeof(EffectType)))
        {
            if (effect == EffectType.None)
                continue;

            var typeName = NameOverrides.TryGetValue(effect, out var overridden)
                ? overridden
                : effect.ToString();

            if (typesByName.TryGetValue(typeName, out var resolved))
                TypeByEffect[effect] = resolved;
            else
                unresolved.Add(effect);
        }

        EffectByType = new Dictionary<Type, EffectType>(TypeByEffect.Count);
        foreach (var pair in TypeByEffect)
            EffectByType[pair.Value] = pair.Key;

        if (unresolved.Count > 0)
            Logger.Warn($"[EffectCatalog] 以下效果在当前游戏版本中找不到对应类型，相关开关将静默忽略：{string.Join(", ", unresolved)}");
    }

    /// <summary>已成功解析的效果数量。</summary>
    public static int Count => TypeByEffect.Count;

    /// <summary>把枚举解析为游戏效果类型。</summary>
    /// <param name="effect">效果枚举。</param>
    /// <returns>对应的 <see cref="StatusEffectBase"/> 子类；无法解析时为 <see langword="null"/>。</returns>
    public static Type? Resolve(EffectType effect) =>
        TypeByEffect.TryGetValue(effect, out var type) ? type : null;

    /// <summary>把游戏效果实例解析为枚举。</summary>
    /// <param name="effect">效果实例，可为 <see langword="null"/>。</param>
    /// <returns>对应的枚举；无法解析时为 <see cref="EffectType.None"/>。</returns>
    public static EffectType Resolve(StatusEffectBase? effect)
    {
        if (effect is null)
            return EffectType.None;

        return EffectByType.TryGetValue(effect.GetType(), out var type) ? type : EffectType.None;
    }

    /// <summary>尝试把游戏效果实例解析为枚举。</summary>
    /// <param name="effect">效果实例。</param>
    /// <param name="type">解析结果。</param>
    /// <returns>是否解析成功。</returns>
    public static bool TryResolve(StatusEffectBase? effect, out EffectType type)
    {
        type = Resolve(effect);
        return type != EffectType.None;
    }

    /// <summary>枚举是否在当前游戏版本中有对应实现。</summary>
    /// <param name="effect">效果枚举。</param>
    /// <returns>可解析则为 <see langword="true"/>。</returns>
    public static bool IsAvailable(EffectType effect) => TypeByEffect.ContainsKey(effect);
}
