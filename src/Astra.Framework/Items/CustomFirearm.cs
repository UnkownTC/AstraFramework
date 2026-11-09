using InventorySystem.Items;

using LabApi.Events.Arguments.PlayerEvents;
using Astra;
using Astra.Attributes;
using Astra.Enums;
using Astra.Extensions;
using Astra.Roles;
using Astra.Teams;
using Astra.Waves;
using Astra.Effects;
using Astra.Abilities;
using Astra.HumeShield;
using Astra.Patching;
using Astra.Hints;
using Astra.Audio;
using Astra.Countdown;
using Astra.Cancellation;
using Astra.ServerSpecificSettings;
using Astra.Loader;

namespace Astra.Items;

/// <summary>
/// 自定义枪械：在 <see cref="CustomItem"/> 之上补充射击数值与开火行为。
/// </summary>
/// <remarks>
/// 数值本身不自动生效——由派生类在 <see cref="OnShooting"/> 中以「阻止原版 + 自建弹道」的方式使用，
/// 这保证自定义枪械不被原版后坐力/弹匣逻辑干扰。
/// </remarks>
public abstract class CustomFirearm : CustomItem
{
    /// <inheritdoc />
    public override ItemType BaseItem { get; }

    /// <summary>构造：指定承载的原版枪械类型。</summary>
    /// <param name="baseItem">原版物品类型。</param>
    protected CustomFirearm(ItemType baseItem) => BaseItem = baseItem;

    /// <summary>单发伤害。</summary>
    public virtual float Damage => 25f;

    /// <summary>射速（发/分钟）。</summary>
    public virtual float FireRate => 600f;

    /// <summary>弹匣容量；<c>-1</c> 表示沿用原版。</summary>
    public virtual int MagazineSize => -1;

    /// <summary>换弹时间（秒）；<c>-1</c> 表示沿用原版。</summary>
    public virtual float ReloadTime => -1f;

    /// <summary>穿透力（可穿透的目标数）。</summary>
    public virtual int Penetration => 1;

    /// <summary>扩散角（度）。</summary>
    public virtual float Spread => 0.5f;

    /// <summary>射击间隔（秒），由 <see cref="FireRate"/> 推导。</summary>
    public float FireInterval => FireRate <= 0f ? 0f : 60f / FireRate;
}
