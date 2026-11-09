using Astra.Attributes;

using EnhancedEventsHandler = Astra.Events.EnhancedEventsHandler;
using ShootingEventArgs = Astra.Events.ShootingEventArgs;

namespace Astra.Items;

/// <summary>
/// 自定义物品的事件转发：把玩家行为交给对应的 <see cref="CustomItem"/> 决定是否拦截。
/// </summary>
/// <remarks>
/// 继承 <see cref="EnhancedEventsHandler"/> 复用其 <c>OnExiledShooting</c> 的事件订阅，
/// 只覆写需要接管的回调。射击默认完全不介入：只有派生类覆写了 <see cref="CustomItem.OnShooting"/>
/// 的自定义物品才会收到回调，并以其返回值决定是否阻止原版开火。
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

        var custom = CustomItemManager.FindByItem(item) ?? CustomItemManager.FindByItemType(item.Type);

        // 未覆写 OnShooting 的物品不接管射击：框架不介入，原版行为保持不变。
        if (custom is null || !CustomItemManager.InterceptsShooting(custom))
            return;

        if (!custom.OnShooting(ev))
            ev.IsAllowed = false;
    }
}
