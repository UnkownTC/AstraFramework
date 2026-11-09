using InventorySystem.Items;
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
/// 自定义 Micro-HID：在 <see cref="CustomItem"/> 之上补充蓄能/放电参数。
/// </summary>
public abstract class CustomMicroHid : CustomItem
{
    /// <inheritdoc />
    public override ItemType BaseItem { get; }

    /// <summary>构造：指定承载的原版物品类型。</summary>
    /// <param name="baseItem">原版物品类型。</param>
    protected CustomMicroHid(ItemType baseItem) => BaseItem = baseItem;

    /// <summary>单次放电伤害。</summary>
    public virtual float Damage => 500f;

    /// <summary>有效射程（米）。</summary>
    public virtual float Range => 12f;

    /// <summary>每秒蓄能百分比。</summary>
    public virtual float ChargeRate => 20f;

    /// <summary>蓄满所需秒数。</summary>
    public float ChargeSeconds => ChargeRate <= 0f ? float.PositiveInfinity : 100f / ChargeRate;
}
