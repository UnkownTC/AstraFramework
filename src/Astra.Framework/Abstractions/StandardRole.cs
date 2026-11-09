using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Astra.Abstractions;
using Astra.Effects;
using Astra.Enums;
using Astra.Systems.CustomEffectSystem;
using Astra.Extensions;
using Astra.Interfaces;
using Astra.Systems.CustomAbilitySystem;

using CustomPlayerEffects;

using Discord;

using LabApi.Features.Console;
using LabApi.Features.Wrappers;

using Mirror;

using UnityEngine;

namespace Astra.Abstractions;

public abstract class StandardRole : CustomRole, IAbilityRole, ICustomHumeShieldStat, IEffects
{
    public virtual CustomAbility[] Abilities { get; } = [];

    /// <summary>护盾上限；<see cref="HumeShieldCurve"/> 非空时以曲线为准。</summary>
    public virtual float MaxHumeShield { get; set; } = 0;

    /// <summary>护盾上限随血量比例的曲线（纵轴为护盾绝对值），复刻原版 <c>ShieldOverHealth</c>。</summary>
    public virtual AnimationCurve HumeShieldCurve { get; set; } = null;

    /// <summary>每秒恢复量。</summary>
    public virtual float HumeShieldRegenRate { get; set; } = 0;

    /// <summary>受伤后延迟多少秒开始恢复。</summary>
    public virtual float HumeShieldRegenDelay { get; set; } = 0;

    /// <summary>
    /// 套用角色时一并施加的效果集合，原版效果与自定义效果可混用。
    /// </summary>
    /// <remarks>
    /// 施加顺序即声明顺序，收口时逆序解除。原版效果走 <see cref="EffectType"/>，
    /// 自定义效果用 <see cref="EffectEntry.Custom{T}(byte, float, bool)"/> 声明。
    /// </remarks>
    public virtual EffectEntry[] Effects { get; } = [];

    internal override void RemoveRole(Player player)
    {
        // 先撤效果再收口，避免基类清掉 Owner/血量后取不到作用对象。
        if (!IsDestroyed)
            EffectApplier.RemoveAll(player, Effects);

        if (player.TryGetAbilityManager(out var abilityManager))
        {
            foreach (var ability in Abilities)
            {
                abilityManager.RemoveAbility(ability.Id);
            }
        }

        base.RemoveRole(player);
    }

    internal override void AddRole(Player player)
    {
        base.AddRole(player);

        // 效果在角色装配完成后施加：此时血量、物品、缩放都已就位。
        EffectApplier.ApplyAll(player, Effects);

        if (player.TryGetAbilityManager(out var abilityManager))
        {
            foreach (var ability in Abilities)
            {
                abilityManager.AddAbility(ability.Id);
            }
        }
    }
}
