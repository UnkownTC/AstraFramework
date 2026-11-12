using Astra.Attributes;

using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Features.Wrappers;

using EnhancedEventsHandler = Astra.Events.EnhancedEventsHandler;
using ShotEventArgs = Astra.Events.ShotEventArgs;
using ShootingEventArgs = Astra.Events.ShootingEventArgs;

namespace Astra.Items;

/// <summary>
/// 自定义物品的事件转发：把玩家与物品的交互交给对应的 <see cref="CustomItem"/> 处理。
/// </summary>
/// <remarks>
/// 继承 <see cref="EnhancedEventsHandler"/> 复用其事件订阅，只覆写需要转发的回调。
/// 射击默认完全不介入：只有覆写了 <see cref="CustomItem.OnShooting"/> 的自定义物品才会收到回调；
/// 其余生命周期/交互钩子默认空实现，覆写即生效。
/// </remarks>
[ManagedEvent("Astra - CustomItem")]
public class CustomItemEventHandler : EnhancedEventsHandler
{
    /// <summary>
    /// 射击：仅当自定义物品覆写了 <see cref="CustomItem.OnShooting"/> 时转发，返回 false 则阻止原版开火。
    /// </summary>
    /// <param name="ev">射击事件参数。</param>
    public override void OnExiledShooting(ShootingEventArgs ev)
    {
        base.OnExiledShooting(ev);

        var item = ev.Item;
        if (item is null)
            return;

        var custom = Resolve(item);

        // 未覆写 OnShooting 的物品不接管射击：框架不介入，原版行为保持不变。
        if (custom is null || !CustomItemManager.InterceptsShooting(custom))
            return;

        if (!custom.OnShooting(ev))
            ev.IsAllowed = false;
    }

    /// <summary>命中结算：转发给自定义枪械。</summary>
    /// <param name="ev">命中事件参数。</param>
    public override void OnExiledShot(ShotEventArgs ev)
    {
        base.OnExiledShot(ev);

        if (ev.FirearmItem is { } item && Resolve(item) is CustomFirearm firearm)
            firearm.OnShot(ev);
    }

    /// <summary>切换手持物品：向旧物品转发收起、向新物品转发装备。</summary>
    /// <param name="ev">切换事件参数。</param>
    public override void OnPlayerChangedItem(PlayerChangedItemEventArgs ev)
    {
        base.OnPlayerChangedItem(ev);

        if (ev.Player is null)
            return;

        if (ev.OldItem is { } oldItem && Resolve(oldItem) is { } holstered)
            holstered.OnHolstered(ev.Player, oldItem);

        if (ev.NewItem is { } newItem && Resolve(newItem) is { } equipped)
            equipped.OnEquipped(ev.Player, newItem);
    }

    /// <summary>拾取物品：转发给自定义物品。</summary>
    /// <param name="ev">拾取事件参数。</param>
    public override void OnPlayerPickedUpItem(PlayerPickedUpItemEventArgs ev)
    {
        base.OnPlayerPickedUpItem(ev);

        if (ev.Player is null || ev.Item is not { } item)
            return;

        Resolve(item)?.OnPickedUp(ev.Player, item);
    }

    /// <summary>丢弃物品：转发给自定义物品。</summary>
    /// <param name="ev">丢弃事件参数。</param>
    public override void OnPlayerDroppedItem(PlayerDroppedItemEventArgs ev)
    {
        base.OnPlayerDroppedItem(ev);

        if (ev.Player is null || ev.Pickup is not { } pickup)
            return;

        Resolve(pickup)?.OnDropped(ev.Player, pickup);
    }

    /// <summary>使用物品前：返回 false 可阻止本次使用。</summary>
    /// <param name="ev">使用事件参数。</param>
    public override void OnPlayerUsingItem(PlayerUsingItemEventArgs ev)
    {
        base.OnPlayerUsingItem(ev);

        if (ev.Player is null || ev.UsableItem is not { } usable)
            return;

        if (Resolve(usable) is { } custom && !custom.OnUsing(ev.Player, usable))
            ev.IsAllowed = false;
    }

    /// <summary>使用完成：转发给自定义物品。</summary>
    /// <param name="ev">使用完成事件参数。</param>
    public override void OnPlayerUsedItem(PlayerUsedItemEventArgs ev)
    {
        base.OnPlayerUsedItem(ev);

        if (ev.Player is null || ev.UsableItem is not { } usable)
            return;

        Resolve(usable)?.OnUsed(ev.Player, usable);
    }

    /// <summary>投掷前：返回 false 可阻止本次投掷。</summary>
    /// <param name="ev">投掷事件参数。</param>
    public override void OnPlayerThrowingItem(PlayerThrowingItemEventArgs ev)
    {
        base.OnPlayerThrowingItem(ev);

        if (ev.Player is null || ev.Pickup is not { } pickup)
            return;

        if (Resolve(pickup) is { } custom && !custom.OnThrowing(ev.Player, pickup))
            ev.IsAllowed = false;
    }

    /// <summary>开始换弹：转发给自定义枪械，返回 false 可阻止。</summary>
    /// <param name="ev">换弹事件参数。</param>
    public override void OnPlayerReloadingWeapon(PlayerReloadingWeaponEventArgs ev)
    {
        base.OnPlayerReloadingWeapon(ev);

        if (ev.Player is null || ev.FirearmItem is not { } firearm)
            return;

        if (Resolve(firearm) is CustomFirearm custom && !custom.OnReloading(ev.Player, firearm))
            ev.IsAllowed = false;
    }

    /// <summary>换弹完成：转发给自定义枪械。</summary>
    /// <param name="ev">换弹完成事件参数。</param>
    public override void OnPlayerReloadedWeapon(PlayerReloadedWeaponEventArgs ev)
    {
        base.OnPlayerReloadedWeapon(ev);

        if (ev.Player is null || ev.FirearmItem is not { } firearm)
            return;

        if (Resolve(firearm) is CustomFirearm custom)
            custom.OnReloaded(ev.Player, firearm);
    }

    /// <summary>空仓扣扳机：转发给自定义枪械，返回 false 可阻止。</summary>
    /// <param name="ev">空枪事件参数。</param>
    public override void OnPlayerDryFiringWeapon(PlayerDryFiringWeaponEventArgs ev)
    {
        base.OnPlayerDryFiringWeapon(ev);

        if (ev.Player is null || ev.FirearmItem is not { } firearm)
            return;

        if (Resolve(firearm) is CustomFirearm custom && !custom.OnDryFiring(ev.Player, firearm))
            ev.IsAllowed = false;
    }

    /// <summary>举枪瞄准：转发给自定义枪械。</summary>
    /// <param name="ev">瞄准事件参数。</param>
    public override void OnPlayerAimedWeapon(PlayerAimedWeaponEventArgs ev)
    {
        base.OnPlayerAimedWeapon(ev);

        if (ev.Player is null || ev.FirearmItem is not { } firearm)
            return;

        if (Resolve(firearm) is CustomFirearm custom)
            custom.OnAiming(ev.Player, firearm);
    }

    /// <summary>按物品实例或原版类型解析自定义物品定义。</summary>
    /// <param name="item">物品实例。</param>
    /// <returns>自定义定义；非自定义物品为 <see langword="null"/>。</returns>
    private static CustomItem? Resolve(Item? item) =>
        item is null ? null : CustomItemManager.FindByItem(item) ?? CustomItemManager.FindByItemType(item.Type);

    /// <summary>按地面物品的原版类型解析自定义物品定义。</summary>
    /// <param name="pickup">地面物品。</param>
    /// <returns>自定义定义；非自定义物品为 <see langword="null"/>。</returns>
    private static CustomItem? Resolve(Pickup? pickup) =>
        pickup is null ? null : CustomItemManager.FindByItemType(pickup.Type);
}
