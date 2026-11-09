using System;
using System.Collections.Generic;

using Astra.Interfaces;
using Astra.Systems.CustomTeamSystem;

using LabApi.Features.Console;

using PlayerRoles;

using UnityEngine;

namespace Astra.Abstractions;

/// <summary>
/// 自定义阵营：声明本阵营与「原版阵营 / 其它自定义阵营」是敌对还是友好。
/// </summary>
/// <remarks>
/// <para>
/// <para>
/// <b>关系是相对的</b>：<c>A 对 B 友好 ≠ B 对 A 友好</c>。判定只看<b>发起方</b>自己的名单，
/// 对方的名单只影响反方向（对方作为发起方时）的结果。
/// </para>
/// 关系判定优先级（<see cref="IsHostileTowards"/>）：
/// </para>
/// <list type="number">
/// <item>自身 → 永远友好。</item>
/// <item><see cref="Relation"/> 为 <see cref="CustomTeamRelation.AllHostile"/> → 敌对。</item>
/// <item><see cref="Relation"/> 为 <see cref="CustomTeamRelation.AllFriendly"/> → 友好。</item>
/// <item><see cref="Relation"/> 为 <see cref="CustomTeamRelation.Custom"/> 时：
///   <list type="bullet">
///   <item>命中 <see cref="Blacklist"/> → 敌对。</item>
///   <item>命中 <see cref="Whitelist"/> → 友好。</item>
///   <item>都没命中 → <see cref="KeepVanillaRelate"/> 为 <see langword="true"/> 时按原版阵营关系判定，
///         为 <see langword="false"/> 时按敌对处理。</item>
///   </list>
/// </item>
/// </list>
/// <para>
/// 黑白名单的条目既可以写自定义阵营的 <see cref="Id"/>，也可以写原版 <see cref="Team"/> 枚举名
/// （如 <c>"Mtf"</c> / <c>"ChaosInsurgency"</c>）。
/// </para>
/// </remarks>
public abstract class CustomTeam : IEquatable<CustomTeam>, IIdentifiable
{
    // 本回合的阵营实例（Id → 实例）
    private static readonly Dictionary<string, CustomTeam> Registry =
        new(StringComparer.OrdinalIgnoreCase);

    // 模板类型：每回合实例化一次
    private static readonly List<Type> Templates = [];

    /// <summary>阵营唯一标识。</summary>
    public abstract string Id { get; }

    /// <summary>阵营显示名。</summary>
    public abstract string Name { get; }

    /// <summary>阵营配色，默认白色。</summary>
    public virtual Color TeamColor => Color.white;

    /// <summary>关系模式，默认 <see cref="CustomTeamRelation.Custom"/>。</summary>
    public virtual CustomTeamRelation Relation => CustomTeamRelation.Custom;

    /// <summary>
    /// 是否保留原版阵营关系（仅 <see cref="CustomTeamRelation.Custom"/> 下生效）。
    /// </summary>
    /// <remarks>
    /// <see langword="true"/>：未被黑白名单命中的目标按原版 <see cref="Team"/> 关系判定；
    /// <see langword="false"/>：未被命中的目标一律视为敌对。
    /// </remarks>
    public virtual bool KeepVanillaRelate => false;

    /// <summary>
    /// 本阵营对应的原版 <see cref="Team"/>：既是「我方在原版体系中的代表」，
    /// 也是名单里引用我方时的匹配键之一。
    /// </summary>
    /// <remarks>默认 <see cref="Team.OtherAlive"/>，表示未指定具体原版阵营。</remarks>
    public virtual Team VanillaTeam => Team.OtherAlive;

    /// <summary>
    /// 本阵营对应的原版 <see cref="Faction"/>：用于派系级别的匹配与比较。
    /// </summary>
    /// <remarks>默认 <see cref="Faction.Unclassified"/>，表示未指定具体派系。</remarks>
    public virtual Faction VanillaFaction => Faction.Unclassified;

    /// <summary>敌对名单。</summary>
    /// <remarks>字符串会隐式转换为 <see cref="RelationEntry"/> 并在构造期校验（非法键直接抛出）。</remarks>
    public virtual IReadOnlyList<RelationEntry> Blacklist => Array.Empty<RelationEntry>();

    /// <summary>友好名单。</summary>
    /// <remarks>字符串会隐式转换为 <see cref="RelationEntry"/> 并在构造期校验（非法键直接抛出）。</remarks>
    public virtual IReadOnlyList<RelationEntry> Whitelist => Array.Empty<RelationEntry>();

    private readonly List<CustomWave> _waves = [];

    /// <summary>归属本阵营的自定义波次（由波次侧的 <see cref="CustomWave.Teams"/> 反向填充）。</summary>
    public IReadOnlyList<CustomWave> Waves => _waves;

    /// <summary>登记一个归属本阵营的波次（框架在波次实例化时调用）。</summary>
    /// <param name="wave">波次实例。</param>
    internal void AddWave(CustomWave wave)
    {
        if (wave is not null && !_waves.Contains(wave))
            _waves.Add(wave);
    }

    /// <summary>当前已注册的全部自定义阵营。</summary>
    internal static IReadOnlyDictionary<string, CustomTeam> All => Registry;

    /// <summary>
    /// 登记阵营模板类型：由 <see cref="Initialize"/> 一次性实例化。
    /// </summary>
    /// <param name="type">阵营类型（须派生自 <see cref="CustomTeam"/> 且非抽象）。</param>
    internal static void RegisterTemplate(Type type)
    {
        if (type is null || type.IsAbstract || !typeof(CustomTeam).IsAssignableFrom(type))
            return;

        if (!Templates.Contains(type))
            Templates.Add(type);
    }

    /// <summary>
    /// 按模板实例化全部阵营（幂等：已实例化时直接返回，不再因回合重启重建）。
    /// </summary>
    /// <remarks>
    /// 由框架在插件启用时调用一次。实例化使用 <c>nonPublic: true</c>，支持单例式的私有构造。
    /// 阵营是无状态描述符，跨回合复用不会残留局面数据。
    /// </remarks>
    internal static void Initialize()
    {
        if (Registry.Count > 0)
            return;

        Registry.Clear();

        foreach (var type in Templates)
        {
            try
            {
                if (Activator.CreateInstance(type, nonPublic: true) is CustomTeam team &&
                    !string.IsNullOrWhiteSpace(team.Id))
                {
                    if (Registry.TryGetValue(team.Id, out var existing))
                    {
                        LabApi.Features.Console.Logger.Warn(
                            $"[CustomTeam] 阵营 Id 重复：{team.Id}（{existing.GetType().Name} 已被 {type.Name} 覆盖）。");
                    }

                    Registry[team.Id] = team;
                }
                else
                {
                    LabApi.Features.Console.Logger.Warn($"[CustomTeam] 模板 {type.Name} 实例化失败或缺少 Id，已跳过。");
                }
            }
            catch (Exception ex)
            {
                LabApi.Features.Console.Logger.Error($"[CustomTeam] 模板 {type.Name} 实例化异常：{ex.Message}");
            }
        }
    }

    /// <summary>按 Id 查找阵营。</summary>
    /// <param name="id">阵营 Id。</param>
    /// <returns>找到的阵营；不存在时为 <see langword="null"/>。</returns>
    internal static CustomTeam? Find(string? id) =>
        id is not null && Registry.TryGetValue(id, out var team) ? team : null;

    /// <summary>清空模板与实例（框架卸载时调用）。</summary>
    internal static void Clear()
    {
        Templates.Clear();
        Registry.Clear();
    }

    /// <summary>
    /// 判断本阵营是否敌对指定目标。
    /// </summary>
    /// <param name="other">目标阵营；<see langword="null"/> 表示原版阵营。</param>
    /// <param name="otherVanillaTeam">目标的原版阵营（用于名单匹配与原版关系判定）。</param>
    /// <param name="otherRole">目标的原版角色（可选，用于最细粒度的名单匹配）。</param>
    /// <returns>敌对返回 <see langword="true"/>。</returns>
    /// <inheritdoc cref="TryGetExplicitRelation(CustomTeam?, Team, RoleTypeId?)" />
    /// <remarks>
    /// <para>
    /// 名单匹配在 <b>同一套判定链</b> 内完成，不脱离 <see cref="Relation"/> 与
    /// <see cref="KeepVanillaRelate"/>：
    /// </para>
    /// <list type="number">
    /// <item><see cref="CustomTeamRelation.AllHostile"/> / <see cref="CustomTeamRelation.AllFriendly"/>
    ///       定下基调，但名单仍可作为例外：AllHostile 下白名单豁免为友好、AllFriendly 下黑名单排除为敌对。</item>
    /// <item><see cref="CustomTeamRelation.Custom"/> 下先查名单（<b>具体条目优先于派系条目</b>）。</item>
    /// <item>名单未命中时才看 <see cref="KeepVanillaRelate"/>：<see langword="true"/> 走原版关系，
    ///       <see langword="false"/> 一律敌对。</item>
    /// </list>
    /// <para>
    /// 原版目标的匹配粒度：<see cref="Team"/> 是 <see cref="Faction"/> 的细化，
    /// 因此 <b>Team 条目优先</b>，可用于在「整块派系敌对/友好」中开出例外：
    /// 黑名单含 <c>"FoundationStaff"</c>、白名单含 <c>"Mtf"</c> 时，MTF 友好、其余
    /// FoundationStaff 下属阵营（如 <c>"MTFUnit"</c>）仍敌对；反向同理。
    /// </para>
    /// </remarks>
    public bool IsHostileTowards(CustomTeam? other, Team otherVanillaTeam) =>
        TryGetExplicitRelation(other, otherVanillaTeam) ?? true;

    /// <summary>
    /// 取得本阵营对目标的<b>明确</b>态度；未明确声明时返回 <see langword="null"/>。
    /// </summary>
    /// <param name="other">目标阵营；<see langword="null"/> 表示原版阵营。</param>
    /// <param name="otherVanillaTeam">目标的原版阵营。</param>
    /// <returns>
    /// 敌对为 <see langword="true"/>、友好为 <see langword="false"/>、
    /// 「未声明且不保留原版关系」为 <see langword="null"/>。
    /// </returns>
    /// <remarks>
    /// <para>
    /// 「明确」的含义：枚举（AllHostile/AllFriendly）、名单命中、或 <see cref="KeepVanillaRelate"/>
    /// 为 <see langword="true"/> 且能按原版关系比较。三者都不成立时视为默认值，交由上层决定。
    /// </para>
    /// <para>
    /// 这样当一方明确、另一方未声明时，明确的一方可同时作用于双向（绝对关系）；
    /// 只有双方都明确且结论相反时才各管各的方向（相对关系）。
    /// </para>
    /// </remarks>
    public bool? TryGetExplicitRelation(CustomTeam? other, Team otherVanillaTeam, RoleTypeId? otherRole = null)
    {
        if (ReferenceEquals(other, this))
            return false;

        // 枚举定性，但允许名单开出例外：AllHostile 下白名单豁免，AllFriendly 下黑名单排除
        if (Relation == CustomTeamRelation.AllHostile)
            return ResolveByLists(other, otherVanillaTeam, otherRole) ?? true;

        if (Relation == CustomTeamRelation.AllFriendly)
            return ResolveByLists(other, otherVanillaTeam, otherRole) ?? false;

        var decided = ResolveByLists(other, otherVanillaTeam, otherRole);
        if (decided.HasValue)
            return decided;

        if (KeepVanillaRelate)
            return other is not null ? IsCustomEnemy(other) : IsVanillaEnemy(otherVanillaTeam);

        return null;
    }

    /// <summary>
    /// 按<b>本方</b>黑白名单给出结论；未命中任何条目时返回 <see langword="null"/>，把决定权交回上层。
    /// </summary>
    /// <param name="other">目标阵营；<see langword="null"/> 表示原版阵营。</param>
    /// <param name="otherVanillaTeam">目标的原版阵营。</param>
    /// <param name="otherRole">目标的原版角色。</param>
    /// <returns>敌对为 <see langword="true"/>、友好为 <see langword="false"/>、未命中为 <see langword="null"/>。</returns>
    /// <remarks>
    /// <para>
    /// 名单条目是强类型 <see cref="RelationEntry"/>，非法键在构造期即被拒绝。
    /// 匹配按<b>粒度从细到粗</b>逐层进行，每层先查黑名单再查白名单：
    /// </para>
    /// <list type="number">
    /// <item>目标为自定义阵营：CustomTeam(Id) → Team(对方 VanillaTeam) → Faction(对方 VanillaFaction)。</item>
    /// <item>目标为原版阵营：Role(对方 RoleTypeId) → Team → Faction。</item>
    /// </list>
    /// <para>全部未命中时看 <see cref="KeepVanillaRelate"/>：<see langword="true"/> 走原版关系，
    /// <see langword="false"/> 一律敌对。</para>
    /// </remarks>
    private bool? ResolveByLists(CustomTeam? other, Team otherVanillaTeam, RoleTypeId? otherRole)
    {
        // 逐层：细粒度优先于粗粒度；同层黑名单优先于白名单
        foreach (var kind in TargetKindOrder(other))
        {
            if (FindEntry(Blacklist, kind, other, otherVanillaTeam, otherRole))
                return true;

            if (FindEntry(Whitelist, kind, other, otherVanillaTeam, otherRole))
                return false;
        }

        return null;
    }

    /// <summary>按目标类型给出条目类别的匹配顺序（细 → 粗）。</summary>
    /// <param name="other">目标阵营。</param>
    /// <returns>类别顺序。</returns>
    private static IEnumerable<RelationEntryKind> TargetKindOrder(CustomTeam? other) => other is null
        ? [RelationEntryKind.Role, RelationEntryKind.Team, RelationEntryKind.Faction]
        : [RelationEntryKind.CustomTeam, RelationEntryKind.Team, RelationEntryKind.Faction];

    /// <summary>在名单中查找指定类别且命中目标的条目。</summary>
    /// <param name="list">名单。</param>
    /// <param name="kind">条目类别。</param>
    /// <param name="other">目标自定义阵营。</param>
    /// <param name="otherVanillaTeam">目标原版阵营。</param>
    /// <param name="otherRole">目标原版角色。</param>
    /// <returns>命中返回 <see langword="true"/>。</returns>
    private static bool FindEntry(IReadOnlyList<RelationEntry> list, RelationEntryKind kind,
        CustomTeam? other, Team otherVanillaTeam, RoleTypeId? otherRole)
    {
        if (list is null || list.Count == 0)
            return false;

        for (var i = 0; i < list.Count; i++)
        {
            var entry = list[i];
            if (entry.Kind == kind && entry.Matches(other, otherVanillaTeam, otherRole))
                return true;
        }

        return false;
    }

    /// <summary>
    /// 判断本阵营与原版阵营的关系（仅 <see cref="KeepVanillaRelate"/> 为 <see langword="true"/> 时使用）。
    /// </summary>
    /// <param name="otherTeam">目标的原版阵营。</param>
    /// <returns>敌对返回 <see langword="true"/>。</returns>
    /// <remarks>
    /// 自定义阵营没有原版 <see cref="Team"/>，需要声明 <see cref="VanillaAnchor"/> 作为「我方在原版体系中的代表」。
    /// 未声明锚点时按敌对处理。
    /// </remarks>
    protected virtual bool IsVanillaEnemy(Team otherTeam)
    {
        if (otherTeam == Team.Dead)
            return true;

        if (VanillaTeam != Team.OtherAlive && VanillaTeam != Team.Dead)
            return HitboxIdentity.IsEnemy(VanillaTeam, otherTeam);

        if (VanillaFaction != Faction.Unclassified)
            return VanillaFaction != otherTeam.GetFaction();

        return true;   // 既无 Team 也无 Faction 声明 → 无法比较，按敌对处理
    }

    /// <summary>
    /// 判断两个自定义阵营在「原版关系」下的敌友（仅 <see cref="KeepVanillaRelate"/> 为 <see langword="true"/> 时使用）。
    /// </summary>
    /// <param name="other">目标阵营。</param>
    /// <returns>敌对返回 <see langword="true"/>。</returns>
    /// <remarks>比较双方的 <see cref="VanillaAnchor"/>；任一方未声明锚点则按敌对处理。</remarks>
    protected virtual bool IsCustomEnemy(CustomTeam other)
    {
        if (VanillaTeam != Team.OtherAlive && other.VanillaTeam != Team.OtherAlive)
            return HitboxIdentity.IsEnemy(VanillaTeam, other.VanillaTeam);

        if (VanillaFaction != Faction.Unclassified && other.VanillaFaction != Faction.Unclassified)
            return VanillaFaction != other.VanillaFaction;

        return true;
    }

    /// <inheritdoc />
    public bool Equals(CustomTeam? other) => ReferenceEquals(this, other);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is CustomTeam other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => Id.GetHashCode();

    /// <inheritdoc />
    public override string ToString() => $"{Name}({Id})";
}
