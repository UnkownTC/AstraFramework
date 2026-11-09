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
using ShootingEventArgs = Astra.Events.ShootingEventArgs;
using EnhancedEventsHandler = Astra.Events.EnhancedEventsHandler;

namespace Astra.Items;

/// <summary>
/// 自定义物品的事件转发：把玩家行为交给对应的 <see cref="CustomItem"/> 决定是否拦截。
/// </summary>
/// <remarks>
/// 继承 <see cref="EnhancedEventsHandler"/> 复用其 <c>OnExiledShooting</c> 的事件订阅，
/// 只覆写需要接管的回调。派生类可用 <see cref="CustomItem.OnShooting"/> 阻止原版开火。
/// </remarks>
[ManagedEvent("Astra - CustomItem")]
public class CustomItemEventHandler : EnhancedEventsHandler
{
    /// <summary>
    /// 射击：玩家手持自定义物品且其 OnShooting 返回 false 时阻止原版开火。
    /// </summary>
    /// <param name="ev">射击事件参数。</param>
    public override void OnExiledShooting(ShootingEventArgs ev)
    {
        base.OnExiledShooting(ev);

        var item = ev.Item;
        if (item is null)
            return;

        var custom = CustomItemManager.FindByItem(item) ?? CustomItemManager.FindByItemType(item.Type);
        if (custom is not null && !custom.OnShooting(ev))
            ev.IsAllowed = false;
    }
}
