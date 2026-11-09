// ReSharper disable IdentifierTypo

using Astra.Effects;

namespace Astra.Enums;

/// <summary>
/// 玩家状态效果的类型标识。
/// </summary>
/// <remarks>
/// <para>
/// 成员名与游戏内 <c>CustomPlayerEffects.StatusEffectBase</c> 的子类名一一对应，
/// 只有少数命名不一致的项由 <see cref="EffectCatalog"/> 的例外表处理。
/// </para>
/// <para>
/// 移植自 <c>LabApi.Helpers.Enums.EffectType</c>，用于在原框架之外独立完成
/// 「按枚举开关效果」而不依赖 <c>LabApi.Helpers</c> 的手写映射表与巨型 switch。
/// </para>
/// </remarks>
public enum EffectType
{
    /// <summary>无效果。</summary>
    None,

    AmnesiaItems,
    AmnesiaVision,
    Asphyxiated,
    Bleeding,
    Blinded,
    Burned,
    Concussed,
    Corroding,
    Deafened,
    Decontaminating,
    Disabled,
    Ensnared,
    Exhausted,
    Flashed,
    Hemorrhage,
    Invigorated,
    BodyshotReduction,
    Poisoned,
    Scp207,
    Invisible,
    SinkHole,
    DamageReduction,
    MovementBoost,
    RainbowTaste,
    SeveredHands,
    Stained,
    Vitality,
    Hypothermia,
    Scp1853,
    CardiacArrest,
    InsufficientLighting,
    SoundtrackMute,
    SpawnProtected,
    Traumatized,
    AntiScp207,
    Scanned,
    PocketCorroding,
    SilentWalk,
    Marshmallow,
    Strangled,
    Ghostly,
    FogControl,
    Slowness,
    Scp1344,
    SeveredEyes,
    PitDeath,
    Blurred,
    BecomingFlamingo,
    Scp559,
    Scp956Target,
    Snowed,
    Scp1344Detected,
    Lightweight,
    HeavyFooted,
    Fade,
    NightVision,

    Scp1509Resurrected,
    TraumatizedEvil,
    SlowMetabolism,
    TemporaryBypass,
    Spicy,
    SugarCrave,
    OrangeWitness,
    Metal,
    SugarHigh,
    SugarRush,
    OrangeCandy,
    WhiteCandy,
    Prismatic,

    AnomalousTarget,
    FocusedVision,
    AnomalousRegeneration,
}
