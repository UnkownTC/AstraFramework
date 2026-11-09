using System;

using Astra.Enums;
using Astra.Effects;

using CustomPlayerEffects;
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
/// 玩家身上一个状态效果的轻量视图。
/// </summary>
/// <remarks>
/// 仅包装已有的 <see cref="StatusEffectBase"/> 实例，不持有额外状态；
/// 写入 <see cref="Duration"/> / <see cref="Intensity"/> / <see cref="IsEnabled"/> 会直接作用到游戏对象。
/// </remarks>
public readonly struct Effect : IEquatable<Effect>
{
    /// <summary>包装一个游戏效果实例。</summary>
    /// <param name="effect">效果实例。</param>
    public Effect(StatusEffectBase effect)
    {
        Base = effect ?? throw new ArgumentNullException(nameof(effect));
    }

    /// <summary>底层游戏对象。</summary>
    public StatusEffectBase Base { get; }

    /// <summary>对应的效果枚举；未登记时为 <see cref="EffectType.None"/>。</summary>
    public EffectType Type => EffectCatalog.Resolve(Base);

    /// <summary>剩余持续时间（秒）。</summary>
    public float Duration
    {
        get => Base.Duration;
        set => Base.Duration = value;
    }

    /// <summary>强度。</summary>
    public byte Intensity
    {
        get => Base.Intensity;
        set => Base.Intensity = value;
    }

    /// <summary>是否处于启用状态。</summary>
    public bool IsEnabled
    {
        get => Base.IsEnabled;
        set => Base.IsEnabled = value;
    }

    /// <inheritdoc />
    public bool Equals(Effect other) => ReferenceEquals(Base, other.Base);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Effect other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => Base.GetHashCode();
}
