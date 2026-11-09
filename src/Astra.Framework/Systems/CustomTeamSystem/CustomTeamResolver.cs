using Astra.Systems.CustomRoleSystem;

using LabApi.Features.Wrappers;

using PlayerRoles;

using Astra.Abstractions;

namespace Astra.Systems.CustomTeamSystem;

/// <summary>把玩家映射到自定义阵营，并计算双方敌对关系。</summary>
/// <remarks>
/// <para>
/// 关系按「谁有明确声明」合并：
/// </para>
/// <list type="number">
/// <item>攻击方有明确态度 → 采用（含原版阵营目标，天然绝对）。</item>
/// <item>攻击方未声明、受击方有 → 采用受击方的明确态度（单方声明对双向生效，绝对关系）。</item>
/// <item>双方都明确且结论相反 → 各方向各用各的（相对关系）。</item>
/// <item>双方都未声明 → 默认敌对。</item>
/// <item>双方都没有自定义阵营 → 完全交回原版。</item>
/// </list>
/// </remarks>
public static class CustomTeamResolver
{
    /// <summary>取得玩家的自定义阵营；没有自定义角色或未声明阵营时为 <see langword="null"/>。</summary>
    /// <param name="hub">玩家 hub。</param>
    /// <returns>自定义阵营；不存在时为 <see langword="null"/>。</returns>
    public static CustomTeam? GetTeam(ReferenceHub? hub)
    {
        if (hub is null)
            return null;

        var player = Player.Get(hub);
        if (player is null)
            return null;

        return CustomRoleManager.TryGetCustomRole(player, out var role) ? role.CustomTeam : null;
    }

    /// <summary>取得玩家的原版阵营。</summary>
    /// <param name="hub">玩家 hub。</param>
    /// <returns>原版阵营；hub 无效时为 <see cref="Team.Dead"/>。</returns>
    public static Team GetVanillaTeam(ReferenceHub? hub) =>
        hub?.roleManager?.CurrentRole?.Team ?? Team.Dead;

    /// <summary>
    /// 判断玩家所属自定义阵营是否与指定原版阵营<b>明确友好</b>。
    /// </summary>
    /// <param name="hub">玩家 hub。</param>
    /// <param name="team">原版阵营（如 <see cref="Team.SCPs"/>）。</param>
    /// <returns>明确声明为友好时返回 <see langword="true"/>；无声明或敌对返回 <see langword="false"/>。</returns>
    /// <remarks>
    /// 只认「明确友好」，不吃默认值——避免未声明关系的阵营被误判为 SCP 友军而免疫技能。
    /// </remarks>
    public static bool IsExplicitFriendlyWith(ReferenceHub? hub, Team team) =>
        GetTeam(hub)?.TryGetExplicitRelation(null, team) == false;

    /// <summary>
    /// 判断玩家所属自定义阵营是否豁免某个 SCP 的特殊技能。
    /// </summary>
    /// <param name="hub">玩家 hub。</param>
    /// <param name="scpRole">施放技能的 SCP 角色。</param>
    /// <returns>
    /// 与 <b>SCPs 派系/阵营整体</b>明确友好（绝对友好，豁免全部 SCP 技能），
    /// 或与<b>该 SCP 角色</b>明确友好（只豁免它的技能）时返回 <see langword="true"/>。
    /// </returns>
    /// <remarks>
    /// 只对单个 SCP（Role 粒度）友好的阵营，不豁免其它 SCP 的技能。
    /// </remarks>
    public static bool IsExplicitFriendlyWithScp(ReferenceHub? hub, RoleTypeId scpRole)
    {
        var custom = GetTeam(hub);
        if (custom is null)
            return false;

        // 角色粒度优先，未命中时回退 Team/Faction 粒度——两者都算「明确友好」
        return custom.TryGetExplicitRelation(null, Team.SCPs, scpRole) == false;
    }

    /// <summary>
    /// SCP-096 挥击门禁：原版此处只有 <c>IsEnemy(Team, Team)</c>（拿不到玩家实例），
    /// 由转译器把调用点替换为本方法，用受击者 hub 做完整的自定义阵营判定。
    /// </summary>
    /// <param name="attackerTeam">原版传入的攻击方阵营（恒为 <see cref="Team.SCPs"/>，忽略）。</param>
    /// <param name="hitbox">受击命中盒。</param>
    /// <returns>敌对返回 <see langword="true"/>；无法判定（无自定义阵营/无效目标）交回原版结果。</returns>
    public static bool IsEnemy096HitGate(Team attackerTeam, HitboxIdentity hitbox)
    {
        var hub = hitbox is null ? null : hitbox.TargetHub;

        if (hub is null)
            return true;

        // 096 本身是原版 SCP（无自定义阵营），判定完全取决于受击者的声明
        if (!TryResolveHostility(null, hub, out var result))
            return true;

        return result;
    }

    /// <summary>
    /// 计算两名玩家是否敌对。
    /// </summary>
    /// <param name="attacker">攻击方。</param>
    /// <param name="victim">受击方。</param>
    /// <param name="result">计算结果。</param>
    /// <returns>需要接管原版判定时返回 <see langword="true"/>。</returns>
    public static bool TryResolveHostility(ReferenceHub? attacker, ReferenceHub? victim, out bool result)
    {
        result = false;

        if (attacker is null || victim is null || ReferenceEquals(attacker, victim))
            return false;

        var attackerTeam = GetTeam(attacker);
        var victimTeam = GetTeam(victim);

        if (attackerTeam is null && victimTeam is null)
            return false;   // 双方都是原版阵营，完全交回原版

        var victimVanilla = GetVanillaTeam(victim);
        var attackerVanilla = GetVanillaTeam(attacker);

        // 单向明确即可覆盖双向（绝对）；双方都明确且相反时才各自生效（相对）。
        var attackerView = attackerTeam?.TryGetExplicitRelation(
            victimTeam, victimVanilla, victim.GetRoleId());
        if (attackerView.HasValue)
        {
            result = attackerView.Value;
            return true;
        }

        var victimView = victimTeam?.TryGetExplicitRelation(
            attackerTeam, attackerVanilla, attacker.GetRoleId());
        if (victimView.HasValue)
        {
            result = victimView.Value;
            return true;
        }

        // 双方都没有明确声明 → 默认敌对
        result = true;
        return true;
    }
}
