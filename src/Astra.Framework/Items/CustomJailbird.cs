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
/// 自定义 Jailbird：在 <see cref="CustomItem"/> 之上补充充能/放电参数。
/// </summary>
public abstract class CustomJailbird : CustomItem
{
    /// <inheritdoc />
    public override ItemType BaseItem { get; }

    /// <summary>构造：指定承载的原版物品类型。</summary>
    /// <param name="baseItem">原版物品类型。</param>
    protected CustomJailbird(ItemType baseItem) => BaseItem = baseItem;

    /// <summary>单次攻击伤害。</summary>
    public virtual float Damage => 60f;

    /// <summary>每次使用消耗的电量百分比。</summary>
    public virtual float ChargeCost => 20f;

    /// <summary>每秒回充百分比。</summary>
    public virtual float RechargeRate => 10f;
}
