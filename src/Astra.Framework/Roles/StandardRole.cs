using System.Collections.Generic;

using Astra.Abilities;
using Astra.Effects;
using Astra.Enums;
using Astra.Extensions;
using Astra.HumeShield;
using Astra.Positions;

using LabApi.Features.Wrappers;

using PlayerRoles;

using UnityEngine;

namespace Astra.Roles;

public abstract class StandardRole : CustomRole, IAbilityRole, ICustomHumeShieldStat, IEffects, ISpawnDefinition
{
    /// <inheritdoc />
    public virtual IReadOnlyDictionary<SpawnPointEntry, float> CustomSpawnPoints { get; } =
        new Dictionary<SpawnPointEntry, float>();
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
        if (player is null)
            return;

        base.AddRole(player);
        if (IsDestroyed)
            return;

        // 效果在角色装配完成后施加：此时血量、物品、缩放都已就位。
        EffectApplier.ApplyAll(player, Effects);

        if (player.TryGetAbilityManager(out var abilityManager))
        {
            foreach (var ability in Abilities)
            {
                abilityManager.AddAbility(ability.Id);
            }
        }

        // 原版 SetRole 已按 SpawnFlags 处理过一次出生点；这里用角色自定义点覆盖。
        if ((SpawnFlags & RoleSpawnFlags.UseSpawnpoint) == 0)
            return;

        if (CustomSpawnPoints is not { Count: > 0 })
            return;

        if (TryPickSpawnPoint(CustomSpawnPoints, out var spawnPoint))
            spawnPoint.Teleport(player);
    }

    private static bool TryPickSpawnPoint(
        IReadOnlyDictionary<SpawnPointEntry, float> spawnPoints,
        out SpawnPointEntry point)
    {
        var candidates = new List<SpawnPointEntry>();
        foreach (var pair in spawnPoints)
        {
            var probability = pair.Value;
            if (float.IsNaN(probability) || probability <= 0f)
                continue;

            if (probability >= 1f || UnityEngine.Random.value <= probability)
                candidates.Add(pair.Key);
        }

        if (candidates.Count == 0)
        {
            point = default;
            return false;
        }

        point = candidates[UnityEngine.Random.Range(0, candidates.Count)];
        return true;
    }
}
