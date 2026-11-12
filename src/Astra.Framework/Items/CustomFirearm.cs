using Astra.Events;

using LabApi.Features.Wrappers;

namespace Astra.Items;

/// <summary>
/// 自定义枪械：在 <see cref="CustomItem"/> 之上补充射击数值，以及开火 / 换弹 / 空枪 / 瞄准 / 命中结算等扩展点。
/// </summary>
/// <remarks>
/// 数值本身不自动生效——由派生类覆写 <see cref="CustomItem.OnShooting"/>（覆写即接管开火）并返回
/// <see langword="false"/>，再以「阻止原版 + 自建弹道」的方式使用这些数值，从而避免被原版后坐力/弹匣逻辑干扰。
/// 不覆写时框架不介入射击，原版行为保持不变。
/// <para>
/// 武器事件同样以虚方法暴露，默认空实现（换弹/空枪返回 <see langword="true"/> 表示放行），
/// 派生类按需覆写即可接管，无需自行订阅 LabAPI 事件或反查自定义物品。
/// </para>
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

    /// <summary>换弹开始时调用；返回 <see langword="false"/> 阻止本次换弹。</summary>
    /// <param name="player">玩家。</param>
    /// <param name="firearm">正在换弹的枪械实例。</param>
    /// <returns>允许换弹返回 <see langword="true"/>。</returns>
    public virtual bool OnReloading(Player player, FirearmItem firearm) => true;

    /// <summary>换弹完成时调用。</summary>
    /// <param name="player">玩家。</param>
    /// <param name="firearm">换弹完成的枪械实例。</param>
    public virtual void OnReloaded(Player player, FirearmItem firearm)
    {
    }

    /// <summary>空仓扣扳机时调用；返回 <see langword="false"/> 阻止原版空枪反馈。</summary>
    /// <param name="player">玩家。</param>
    /// <param name="firearm">枪械实例。</param>
    /// <returns>允许原版空枪反馈返回 <see langword="true"/>。</returns>
    public virtual bool OnDryFiring(Player player, FirearmItem firearm) => true;

    /// <summary>玩家举枪瞄准时调用。</summary>
    /// <param name="player">玩家。</param>
    /// <param name="firearm">枪械实例。</param>
    public virtual void OnAiming(Player player, FirearmItem firearm)
    {
    }

    /// <summary>子弹命中结算时调用，可修改 <see cref="ShotEventArgs.CanHurt"/> 等字段或自行结算伤害。</summary>
    /// <param name="ev">命中事件参数。</param>
    public virtual void OnShot(ShotEventArgs ev)
    {
    }
}
