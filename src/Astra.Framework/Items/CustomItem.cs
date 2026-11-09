using System;

using Astra;
using Astra.Items;

using InventorySystem.Items;

using LabApi.Features.Wrappers;
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
using ShootingEventArgs = Astra.Events.ShootingEventArgs;

namespace Astra.Items;

/// <summary>
/// 自定义物品基类：以一个原版 <see cref="ItemType"/> 为承载体，附加自定义行为与数值。
/// </summary>
/// <remarks>
/// <para>
/// 与 Exiled 的 <c>CustomItem</c> 思路一致：**不自建物品 ID**，而是复用原版物品类型并接管其行为。
/// 因此同一个 <see cref="BaseItem"/> 只能注册一个自定义物品（重复注册会告警并跳过）。
/// </para>
/// <para>
/// 行为分两类：
/// </para>
/// <list type="bullet">
/// <item>数值/属性：派生类以属性声明，由使用方自行读取。</item>
/// <item>事件：<see cref="OnShooting"/> 等虚方法在框架转发事件时调用，可拦截原版行为（返回 <see langword="false"/>）。</item>
/// </list>
/// </remarks>
public abstract class CustomItem : IIdentifiable
{
    /// <inheritdoc />
    public abstract string Id { get; }

    /// <inheritdoc />
    public abstract string Name { get; }

    /// <summary>承载本自定义物品的原版物品类型。</summary>
    public abstract ItemType BaseItem { get; }

    /// <summary>物品描述，默认空。</summary>
    public virtual string Description => string.Empty;

    /// <summary>物品重量；<c>-1</c> 表示使用原版默认值。</summary>
    public virtual float Weight => -1f;

    /// <summary>
    /// 射击事件（仅对枪械类物品有意义）：返回 <see langword="false"/> 阻止原版射击。
    /// </summary>
    /// <param name="ev">射击事件参数。</param>
    /// <returns>允许原版射击返回 <see langword="true"/>。</returns>
    public virtual bool OnShooting(ShootingEventArgs ev) => true;

    /// <summary>物品被玩家获得时调用。</summary>
    /// <param name="player">获得物品的玩家。</param>
    /// <param name="item">物品实例。</param>
    public virtual void OnAcquired(Player player, Item item)
    {
    }

    /// <summary>物品被丢弃时调用。</summary>
    /// <param name="player">丢弃物品的玩家。</param>
    /// <param name="pickup">地面物品。</param>
    public virtual void OnDropped(Player player, Pickup pickup)
    {
    }

    /// <summary>给玩家发放本物品。</summary>
    /// <param name="player">目标玩家。</param>
    /// <returns>生成的物品实例；失败为 <see langword="null"/>。</returns>
    public virtual Item? Give(Player player)
    {
        if (player is null)
            return null;

        var item = player.AddItem(BaseItem);
        if (item is null)
            return null;

        CustomItemManager.Track(item, this);
        OnAcquired(player, item);
        return item;
    }
}
