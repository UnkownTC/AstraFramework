using System;
using System.Collections.Generic;
using System.Linq;

using LabApi.Features.Wrappers;

using UnityEngine;

using ShootingEventArgs = Astra.Events.ShootingEventArgs;

namespace Astra.Items;

/// <summary>
/// 自定义 Particle Disruptor（轨道炮类）：蓄力、穿透与射线伤害。
/// </summary>
/// <remarks>
/// 与原版一致，发射为「瞬时射线」而非抛射物；是否穿透由 <see cref="PierceCount"/> 决定。
/// 视觉轨迹属于客户端表现，服务端只能提供命中数据，派生类可在 <see cref="OnFired"/> 中自行广播效果。
/// </remarks>
public abstract class CustomParticleDisruptor : CustomItem
{
    /// <inheritdoc />
    public override ItemType BaseItem { get; }

    /// <summary>构造：指定承载的原版物品类型。</summary>
    /// <param name="baseItem">原版物品类型。</param>
    protected CustomParticleDisruptor(ItemType baseItem) => BaseItem = baseItem;

    /// <summary>蓄力时间（秒）。</summary>
    public virtual float ChargeTime => 1f;

    /// <summary>单目标伤害。</summary>
    public virtual float Damage => 200f;

    /// <summary>射程（米）。</summary>
    public virtual float Range => 100f;

    /// <summary>可穿透的目标数量；<c>0</c> 表示命中即止。</summary>
    public virtual int PierceCount => 3;

    /// <summary>穿透时每层的伤害衰减系数（0~1）。</summary>
    public virtual float DamageFalloff => 0.75f;

    /// <summary>光束颜色（供客户端/后续表现层使用）。</summary>
    public virtual Color BeamColor => Color.cyan;

    /// <summary>射击事件：阻止原版开火并立即结算自定义射线。</summary>
    /// <param name="ev">射击事件参数。</param>
    /// <returns>始终返回 <see langword="false"/>（阻止原版）。</returns>
    public override bool OnShooting(ShootingEventArgs ev)
    {
        if (ev.Player is { } player)
            Fire(player);

        return false;
    }

    /// <summary>
    /// 结算一次穿透射线：从玩家视角沿朝向发射，按 <see cref="PierceCount"/> 穿透并逐层衰减。
    /// </summary>
    /// <param name="player">开火者。</param>
    /// <returns>按命中顺序排列的目标。</returns>
    public virtual IReadOnlyList<Player> Fire(Player player)
    {
        if (player is null)
            return Array.Empty<Player>();

        var camera = player.ReferenceHub.PlayerCameraReference;
        var origin = camera.position;
        var direction = camera.forward;
        var hits = Physics.RaycastAll(origin, direction, Range, ~0, QueryTriggerInteraction.Ignore);

        var targets = new List<Player>(PierceCount + 1);
        var seen = new HashSet<Player>();

        foreach (var hit in hits.OrderBy(hit => hit.distance))
        {
            if (targets.Count > PierceCount)
                break;

            var hitbox = hit.collider.GetComponentInParent<HitboxIdentity>();
            if (hitbox?.TargetHub is not { } hub)
                continue;

            var target = Player.Get(hub);
            if (target is null || target == player || !seen.Add(target))
                continue;

            var damage = Damage * Mathf.Pow(DamageFalloff, targets.Count);
            target.Damage(damage, Name);
            targets.Add(target);
        }

        OnFired(player, targets);
        return targets;
    }

    /// <summary>射线结算完成后的回调。</summary>
    /// <param name="player">开火者。</param>
    /// <param name="hits">按命中顺序排列的目标。</param>
    public virtual void OnFired(Player player, IReadOnlyList<Player> hits)
    {
    }
}
