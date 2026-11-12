using LabApi.Features.Wrappers;

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
/// <item>事件：<see cref="OnShooting"/> 只在派生类覆写时才会被框架转发——覆写即表示接管开火，
/// 返回 <see langword="false"/> 阻止原版；不覆写时框架不介入射击，原版行为保持不变。</item>
/// <item>生命周期与交互（获得 / 装备 / 收起 / 拾取 / 丢弃 / 使用 / 投掷）均有对应的虚方法，
/// 默认空实现，覆写即接管。</item>
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
    /// <remarks>
    /// <b>覆写本方法即表示由本物品接管开火</b>：框架只对覆写了该方法的自定义物品转发射击事件；
    /// 不覆写时框架完全不介入，原版射击照常进行。是否拦截玩家射击因此由开发者自行选择，
    /// 而不是框架的默认行为。
    /// </remarks>
    public virtual bool OnShooting(ShootingEventArgs ev) => true;

    /// <summary>物品被玩家获得时调用。</summary>
    /// <param name="player">获得物品的玩家。</param>
    /// <param name="item">物品实例。</param>
    public virtual void OnAcquired(Player player, Item item)
    {
    }

    /// <summary>物品被装备（切换到手持）时调用。</summary>
    /// <param name="player">玩家。</param>
    /// <param name="item">被装备的物品实例。</param>
    public virtual void OnEquipped(Player player, Item item)
    {
    }

    /// <summary>物品被收起（从手持切走）时调用。</summary>
    /// <param name="player">玩家。</param>
    /// <param name="item">被收起的物品实例。</param>
    public virtual void OnHolstered(Player player, Item item)
    {
    }

    /// <summary>物品被拾取时调用。</summary>
    /// <param name="player">玩家。</param>
    /// <param name="item">被拾取的物品实例。</param>
    public virtual void OnPickedUp(Player player, Item item)
    {
    }

    /// <summary>物品被丢弃时调用。</summary>
    /// <param name="player">丢弃物品的玩家。</param>
    /// <param name="pickup">地面物品。</param>
    public virtual void OnDropped(Player player, Pickup pickup)
    {
    }

    /// <summary>物品被使用前调用；返回 <see langword="false"/> 阻止本次使用。</summary>
    /// <param name="player">玩家。</param>
    /// <param name="item">被使用的物品实例。</param>
    /// <returns>允许使用返回 <see langword="true"/>。</returns>
    public virtual bool OnUsing(Player player, UsableItem item) => true;

    /// <summary>物品使用完成后调用。</summary>
    /// <param name="player">玩家。</param>
    /// <param name="item">被使用的物品实例。</param>
    public virtual void OnUsed(Player player, UsableItem item)
    {
    }

    /// <summary>物品被投掷前调用；返回 <see langword="false"/> 阻止本次投掷。</summary>
    /// <param name="player">玩家。</param>
    /// <param name="pickup">将被投掷的地面物品。</param>
    /// <returns>允许投掷返回 <see langword="true"/>。</returns>
    public virtual bool OnThrowing(Player player, Pickup pickup) => true;

    /// <summary>给玩家发放本物品。</summary>
    /// <param name="player">目标玩家。</param>
    /// <returns>生成的物品实例；失败为 <see langword="null"/>。</returns>
    public virtual Item? Give(Player player)
    {
        if (player is null)
            return null;

        var item = CreateItem(player);
        if (item is null)
            return null;

        CustomItemManager.Track(item, this);
        OnAcquired(player, item);
        return item;
    }

    /// <summary>创建待发放的物品实例；默认发放 <see cref="BaseItem"/>。</summary>
    /// <param name="player">目标玩家。</param>
    /// <returns>物品实例；失败为 <see langword="null"/>。</returns>
    /// <remarks>覆写后可自定义初始状态（弹药、挂件、序列号等），<see cref="Give"/> 会继续完成登记与回调。</remarks>
    protected virtual Item? CreateItem(Player player) => player.AddItem(BaseItem);
}
