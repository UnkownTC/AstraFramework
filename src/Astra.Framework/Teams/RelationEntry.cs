using System;
using System.Linq;

using Astra;

using PlayerRoles;
using Astra.Attributes;
using Astra.Enums;
using Astra.Extensions;
using Astra.Roles;
using Astra.Waves;
using Astra.Items;
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

namespace Astra.Teams;

/// <summary>名单条目的类别。</summary>
public enum RelationEntryKind
{
    /// <summary>原版角色（最细粒度，如 <see cref="RoleTypeId.Scp049"/>）。</summary>
    Role,

    /// <summary>原版阵营（如 <see cref="Team.SCPs"/>）。</summary>
    Team,

    /// <summary>原版派系（如 <see cref="Faction.SCP"/>）。</summary>
    Faction,

    /// <summary>自定义阵营 Id。</summary>
    CustomTeam,
}

/// <summary>
/// 名单条目：把 <see cref="RoleTypeId"/> / <see cref="Team"/> / <see cref="Faction"/> /
/// 自定义阵营 Id 四种键**包成强类型**，杜绝非法字符串静默失配。
/// </summary>
/// <remarks>
/// <para>
/// 通过 <see cref="Parse(string)"/> 或隐式转换创建时即完成校验：
/// 字符串会按 Role → Team → Faction 顺序尝试解析，全部失败时视为自定义阵营 Id
/// （仍要求非空白）。非法值在名单构造期就会抛出，而不是运行期静默不命中。
/// </para>
/// <para>同名键的解析顺序：Role → Team → Faction（例如 <c>Flamingos</c> 同时是 Team 与 Faction，解析为 Team）。</para>
/// </remarks>
public readonly struct RelationEntry : IEquatable<RelationEntry>
{
    private RelationEntry(RelationEntryKind kind, RoleTypeId role, Team team, Faction faction, string? customTeamId)
    {
        Kind = kind;
        Role = role;
        Team = team;
        Faction = faction;
        CustomTeamId = customTeamId;
    }

    /// <summary>条目类别。</summary>
    public RelationEntryKind Kind { get; }

    /// <summary>Role 类别的值；其它类别为 <see langword="null"/>。</summary>
    public RoleTypeId? Role { get; }

    /// <summary>Team 类别的值；其它类别为 <see langword="null"/>。</summary>
    public Team? Team { get; }

    /// <summary>Faction 类别的值；其它类别为 <see langword="null"/>。</summary>
    public Faction? Faction { get; }

    /// <summary>自定义阵营类别的 Id；其它类别为 <see langword="null"/>。</summary>
    public string? CustomTeamId { get; }

    /// <summary>创建 Role 条目。</summary>
    /// <param name="role">原版角色。</param>
    /// <returns>条目。</returns>
    public static RelationEntry For(RoleTypeId role) => new(RelationEntryKind.Role, role, default, default, null);

    /// <summary>创建 Team 条目。</summary>
    /// <param name="team">原版阵营。</param>
    /// <returns>条目。</returns>
    public static RelationEntry For(Team team) => new(RelationEntryKind.Team, default, team, default, null);

    /// <summary>创建 Faction 条目。</summary>
    /// <param name="faction">原版派系。</param>
    /// <returns>条目。</returns>
    public static RelationEntry For(Faction faction) => new(RelationEntryKind.Faction, default, default, faction, null);

    /// <summary>创建自定义阵营条目。</summary>
    /// <param name="customTeamId">自定义阵营 Id。</param>
    /// <returns>条目。</returns>
    /// <exception cref="ArgumentException">Id 为空白。</exception>
    public static RelationEntry For(string customTeamId)
    {
        if (string.IsNullOrWhiteSpace(customTeamId))
            throw new ArgumentException("自定义阵营 Id 不能为空白。", nameof(customTeamId));

        return new RelationEntry(RelationEntryKind.CustomTeam, default, default, default, customTeamId);
    }

    /// <summary>
    /// 解析字符串为条目（隐式转换的底层实现）。
    /// </summary>
    /// <param name="raw">原始键。</param>
    /// <returns>条目。</returns>
    /// <exception cref="FormatException">空白或无法解析。</exception>
    public static RelationEntry Parse(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            throw new FormatException("[RelationEntry] 名单键不能为空白。");

        if (Enum.TryParse(raw, ignoreCase: true, out RoleTypeId role) && Enum.IsDefined(typeof(RoleTypeId), role))
            return For(role);

        if (Enum.TryParse(raw, ignoreCase: true, out Team team) && Enum.IsDefined(typeof(Team), team))
            return For(team);

        if (Enum.TryParse(raw, ignoreCase: true, out Faction faction) && Enum.IsDefined(typeof(Faction), faction))
            return For(faction);

        return For(raw);
    }

    /// <summary>字符串到条目的隐式转换（会校验）。</summary>
    /// <param name="raw">原始键。</param>
    public static implicit operator RelationEntry(string raw) => Parse(raw);

    /// <summary>RoleTypeId 到条目的隐式转换。</summary>
    /// <param name="role">原版角色。</param>
    public static implicit operator RelationEntry(RoleTypeId role) => For(role);

    /// <summary>Team 到条目的隐式转换。</summary>
    /// <param name="team">原版阵营。</param>
    public static implicit operator RelationEntry(Team team) => For(team);

    /// <summary>Faction 到条目的隐式转换。</summary>
    /// <param name="faction">原版派系。</param>
    public static implicit operator RelationEntry(Faction faction) => For(faction);

    /// <summary>
    /// 判断本条目是否指向指定目标。
    /// </summary>
    /// <param name="other">目标自定义阵营；<see langword="null"/> 表示原版阵营。</param>
    /// <param name="otherVanillaTeam">目标的原版阵营。</param>
    /// <param name="otherRole">目标的原版角色。</param>
    /// <returns>指向该目标时为 <see langword="true"/>。</returns>
    internal bool Matches(CustomTeam? other, Team otherVanillaTeam, RoleTypeId? otherRole) => Kind switch
    {
        RelationEntryKind.CustomTeam => other is not null &&
                                        string.Equals(other.Id, CustomTeamId, StringComparison.OrdinalIgnoreCase),
        RelationEntryKind.Role => otherRole.HasValue && otherRole.Value == Role!.Value,
        RelationEntryKind.Team => otherVanillaTeam == Team!.Value &&
                                  (other is null || other.VanillaTeam == Team!.Value),
        RelationEntryKind.Faction => otherVanillaTeam.GetFaction() == Faction!.Value &&
                                     (other is null || other.VanillaFaction == Faction!.Value),
        _ => false,
    };

    /// <inheritdoc />
    public bool Equals(RelationEntry other) => Kind == other.Kind && Kind switch
    {
        RelationEntryKind.Role => Role == other.Role,
        RelationEntryKind.Team => Team == other.Team,
        RelationEntryKind.Faction => Faction == other.Faction,
        RelationEntryKind.CustomTeam => string.Equals(CustomTeamId, other.CustomTeamId, StringComparison.OrdinalIgnoreCase),
        _ => false,
    };

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is RelationEntry other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        unchecked
        {
            return Kind switch
            {
                RelationEntryKind.Role => ((int)Role!.Value * 397) ^ (int)Kind,
                RelationEntryKind.Team => ((int)Team!.Value * 397) ^ (int)Kind,
                RelationEntryKind.Faction => ((int)Faction!.Value * 397) ^ (int)Kind,
                RelationEntryKind.CustomTeam => (CustomTeamId?.ToLowerInvariant().GetHashCode() ?? 0) ^ (int)Kind,
                _ => (int)Kind,
            };
        }
    }

    /// <inheritdoc />
    public override string ToString() => Kind switch
    {
        RelationEntryKind.Role => $"Role:{Role}",
        RelationEntryKind.Team => $"Team:{Team}",
        RelationEntryKind.Faction => $"Faction:{Faction}",
        RelationEntryKind.CustomTeam => $"CustomTeam:{CustomTeamId}",
        _ => Kind.ToString(),
    };
}
