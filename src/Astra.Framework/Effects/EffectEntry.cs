using System;
using System.Collections.Concurrent;
using System.Reflection;

using Astra;
using Astra.Enums;
using Astra.Extensions;
using Astra.Effects;

using LabApi.Features.Wrappers;
using Astra.Attributes;
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
/// 统一的「效果条目」：原版状态效果与自定义效果共用同一种声明方式。
/// </summary>
/// <remarks>
/// <para>
/// 原版效果按 <see cref="EffectType"/> 施加到玩家身上，走 LabApi 的效果接口；
/// 自定义效果按 <c>CustomEffectBase</c> 派生类型挂到 <see cref="CustomEffectController"/> 上。
/// 角色只需声明一组 <see cref="EffectEntry"/>，无需关心两者的实现差异。
/// </para>
/// <para>
/// 用法：
/// <code>
/// public override IReadOnlyList&lt;EffectEntry&gt; Effects { get; } =
/// [
///     EffectEntry.Vanilla(EffectType.Scp207, 2, 15f),
///     EffectEntry.Custom&lt;CustomBleedingEffect&gt;(1, 20f),
/// ];
/// </code>
/// </para>
/// </remarks>
public readonly struct EffectEntry
{
    private static readonly ConcurrentDictionary<Type, ConstructorInfo> ConstructorCache = new();

    private readonly EffectType _vanilla;
    private readonly Type? _customType;
    private readonly Func<Player, CustomEffect>? _factory;

    private EffectEntry(EffectType vanilla, Type? customType, Func<Player, CustomEffect>? factory,
        byte intensity, float duration, bool addDuration)
    {
        _vanilla = vanilla;
        _customType = customType;
        _factory = factory;
        Intensity = intensity;
        Duration = duration;
        AddDuration = addDuration;
    }

    /// <summary>是否为自定义效果。</summary>
    public bool IsCustom => _customType is not null;

    /// <summary>原版效果枚举；自定义效果时为 <see cref="EffectType.None"/>。</summary>
    public EffectType VanillaType => _vanilla;

    /// <summary>自定义效果类型；原版效果时为 <see langword="null"/>。</summary>
    public Type? CustomType => _customType;

    /// <summary>强度。</summary>
    public byte Intensity { get; }

    /// <summary>持续时间（秒），0 表示沿用效果默认值。</summary>
    public float Duration { get; }

    /// <summary>是否在剩余时长上累加。</summary>
    public bool AddDuration { get; }

    /// <summary>声明一个原版状态效果。</summary>
    /// <param name="type">效果枚举。</param>
    /// <param name="intensity">强度。</param>
    /// <param name="duration">持续时间（秒）。</param>
    /// <param name="addDuration">是否累加时长。</param>
    /// <returns>效果条目。</returns>
    public static EffectEntry Vanilla(EffectType type, byte intensity = 1, float duration = 0f,
        bool addDuration = false) =>
        new(type, null, null, intensity, duration, addDuration);

    /// <summary>
    /// 声明一个自定义效果；要求目标类型具备 <c>(Player)</c> 公开构造函数。
    /// </summary>
    /// <typeparam name="T">自定义效果类型。</typeparam>
    /// <param name="intensity">强度。</param>
    /// <param name="duration">持续时间（秒）。</param>
    /// <param name="addDuration">是否累加时长。</param>
    /// <returns>效果条目。</returns>
    public static EffectEntry Custom<T>(byte intensity = 1, float duration = 0f, bool addDuration = false)
        where T : CustomEffect =>
        Custom(CreateFactory(typeof(T)), intensity, duration, addDuration);

    /// <summary>用显式工厂声明一个自定义效果，适合构造函数带额外参数的场景。</summary>
    /// <typeparam name="T">自定义效果类型。</typeparam>
    /// <param name="factory">创建工厂。</param>
    /// <param name="intensity">强度。</param>
    /// <param name="duration">持续时间（秒）。</param>
    /// <param name="addDuration">是否累加时长。</param>
    /// <returns>效果条目。</returns>
    public static EffectEntry Custom<T>(Func<Player, T> factory, byte intensity = 1, float duration = 0f,
        bool addDuration = false) where T : CustomEffect
    {
        if (factory is null)
            throw new ArgumentNullException(nameof(factory));

        return new EffectEntry(EffectType.None, typeof(T), player => factory(player), intensity, duration,
            addDuration);
    }

    /// <summary>对指定玩家施加该效果。</summary>
    /// <param name="player">目标玩家。</param>
    public void Apply(Player player)
    {
        if (player is null || player.IsDestroyed)
            return;

        if (!IsCustom)
        {
            player.EnableEffect(_vanilla, Intensity, Duration, AddDuration);
            return;
        }

        var controller = GetController(player);
        var effect = controller?.GetOrAddEffect(_factory!);
        effect?.SetState(Intensity, Duration, AddDuration);
    }

    /// <summary>从指定玩家身上解除该效果。</summary>
    /// <param name="player">目标玩家。</param>
    /// <remarks>自定义效果只做停用，实例保留在控制器里以便下次复用。</remarks>
    public void Remove(Player player)
    {
        if (player is null || player.IsDestroyed)
            return;

        if (!IsCustom)
        {
            player.DisableEffect(_vanilla);
            return;
        }

        GetController(player)?.DisableEffect(_customType!);
    }

    /// <inheritdoc />
    public override string ToString() =>
        IsCustom ? $"Custom({_customType!.Name}, {Intensity}, {Duration})"
                 : $"Vanilla({_vanilla}, {Intensity}, {Duration})";

    /// <summary>取玩家身上的自定义效果控制器。</summary>
    private static CustomEffectController? GetController(Player player) =>
        player.GameObject.TryGetComponent<CustomEffectController>(out var controller) ? controller : null;

    /// <summary>为具备 <c>(Player)</c> 构造函数的类型生成缓存的创建工厂。</summary>
    private static Func<Player, CustomEffect> CreateFactory(Type type)
    {
        var ctor = ConstructorCache.GetOrAdd(type, static target =>
            target.GetConstructor(new[] { typeof(Player) })
            ?? throw new InvalidOperationException(
                $"自定义效果 {target.FullName} 缺少 public (Player) 构造函数，请改用 EffectEntry.Custom(factory, ...)。"));

        return player => (CustomEffect)ctor.Invoke(new object[] { player });
    }
}
