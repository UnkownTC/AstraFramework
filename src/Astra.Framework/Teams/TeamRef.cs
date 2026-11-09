using System;

using PlayerRoles;

namespace Astra.Teams;

/// <summary>
/// 阵营引用：把「自定义阵营」与「原版阵营」收敛到同一种值类型，供角色等组件统一表达阵营归属。
/// </summary>
/// <remarks>
/// <para>
/// 自定义阵营优先：<see cref="CustomTeam"/> 非空时以它为准，<see cref="VanillaTeam"/> 取其
/// <see cref="Astra.Teams.CustomTeam.VanillaTeam"/> 锚点；否则 <see cref="VanillaTeam"/> 就是原版阵营本身。
/// </para>
/// <para>
/// <see cref="PlayerRoles.Team"/> 与 <see cref="Astra.Teams.CustomTeam"/> 都可隐式转换为本类型，
/// 因此声明阵营时直接写原版枚举或自定义阵营实例即可。
/// </para>
/// <para>
/// 本类型也可隐式转换为 <see cref="PlayerRoles.Team"/>（返回 <see cref="VanillaTeam"/>），便于把
/// <c>TeamRef</c> 当作原版阵营使用；但 <c>switch</c> 的常量模式要求常量本身可隐式转换为被匹配类型，
/// 这个方向不成立，需要按原版阵营分支时请显式写 <c>team.VanillaTeam</c>。
/// </para>
/// </remarks>
public readonly struct TeamRef : IEquatable<TeamRef>
{
    /// <summary>用自定义阵营（可选）与原版阵营构造阵营引用。</summary>
    /// <param name="customTeam">自定义阵营；<see langword="null"/> 表示只使用原版阵营。</param>
    /// <param name="vanillaTeam">原版阵营。</param>
    public TeamRef(CustomTeam? customTeam, Team vanillaTeam)
    {
        CustomTeam = customTeam;
        VanillaTeam = vanillaTeam;
    }

    /// <summary>自定义阵营；为 <see langword="null"/> 表示只使用原版阵营。</summary>
    public CustomTeam? CustomTeam { get; }

    /// <summary>
    /// 原版阵营：自定义阵营存在时为其声明的 <see cref="Astra.Teams.CustomTeam.VanillaTeam"/> 锚点。
    /// </summary>
    public Team VanillaTeam { get; }

    /// <summary>是否由自定义阵营接管。</summary>
    public bool IsCustom => CustomTeam is not null;

    /// <summary>用原版阵营构造阵营引用。</summary>
    /// <param name="team">原版阵营。</param>
    /// <returns>只含原版阵营的引用。</returns>
    public static TeamRef FromVanilla(Team team) => new(null, team);

    /// <summary>用自定义阵营构造阵营引用，原版阵营取其锚点。</summary>
    /// <param name="team">自定义阵营。</param>
    /// <returns>含自定义阵营的引用。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="team"/> 为 <see langword="null"/>。</exception>
    public static TeamRef FromCustom(CustomTeam team)
    {
        if (team is null)
            throw new ArgumentNullException(nameof(team));

        return new TeamRef(team, team.VanillaTeam);
    }

    /// <summary>原版阵营隐式转换为阵营引用。</summary>
    /// <param name="team">原版阵营。</param>
    /// <returns>只含原版阵营的引用。</returns>
    public static implicit operator TeamRef(Team team) => FromVanilla(team);

    /// <summary>自定义阵营隐式转换为阵营引用。</summary>
    /// <param name="team">自定义阵营。</param>
    /// <returns>含自定义阵营的引用。</returns>
    public static implicit operator TeamRef(CustomTeam team) => FromCustom(team);

    /// <summary>阵营引用隐式转换为原版阵营（自定义阵营时取其 <see cref="Astra.Teams.CustomTeam.VanillaTeam"/> 锚点）。</summary>
    /// <param name="team">阵营引用。</param>
    /// <returns>原版阵营。</returns>
    public static implicit operator Team(TeamRef team) => team.VanillaTeam;

    /// <inheritdoc />
    public bool Equals(TeamRef other) =>
        VanillaTeam == other.VanillaTeam && ReferenceEquals(CustomTeam, other.CustomTeam);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is TeamRef other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() =>
        ((CustomTeam?.GetHashCode() ?? 0) * 397) ^ (int)VanillaTeam;

    /// <inheritdoc />
    public override string ToString() => CustomTeam?.ToString() ?? VanillaTeam.ToString();

    /// <summary>比较两个阵营引用是否相同（自定义阵营按实例、原版阵营按枚举值）。</summary>
    /// <param name="left">左值。</param>
    /// <param name="right">右值。</param>
    /// <returns>相同时返回 <see langword="true"/>。</returns>
    public static bool operator ==(TeamRef left, TeamRef right) => left.Equals(right);

    /// <summary>比较两个阵营引用是否不同。</summary>
    /// <param name="left">左值。</param>
    /// <param name="right">右值。</param>
    /// <returns>不同时返回 <see langword="true"/>。</returns>
    public static bool operator !=(TeamRef left, TeamRef right) => !left.Equals(right);
}
