using System;
using System.Collections.Generic;
using System.Linq;

using Astra.Loader;
using Astra.Roles;
using Astra.Teams;

using LabApi.Features.Wrappers;

using UnityEngine;

namespace Astra.Waves;

/// <summary>
/// 自定义刷新波次的框架基类：声明「刷新哪些自定义角色、各多少个」以及所属阵营与播报。
/// </summary>
/// <remarks>
/// <para>
/// 与具体角色系统的唯一耦合是一层抽象 <see cref="ICustomWaveRoleSpawner"/>：
/// 波次本身不认识角色注册表，由宿主（如 FirearmPack 或插件）提供「按 Id 给玩家套角色」的实现。
/// </para>
/// <para>
/// <see cref="Table"/> 的取值语义：正数为上限；<c>-1</c> 表示「填充剩余玩家」，多个 -1 条目随机分配。
/// </para>
/// </remarks>
public abstract class CustomWave : ICustomContent, ICallOnRegister, ICallOnUnregister
{
    /// <inheritdoc />
    public abstract string Id { get; }

    /// <inheritdoc />
    public abstract string Name { get; }

    /// <summary>
    /// 刷新配置：自定义角色 Id → 数量（<c>-1</c> 表示填充剩余玩家）。
    /// </summary>
    /// <remarks>
    /// 由 <see cref="Roles"/> 中实现 <see cref="IWaveSpawnRole"/> 的角色聚合而成，
    /// 波次本身不再维护刷新表——角色是数量与归属的唯一来源。
    /// </remarks>
    public IReadOnlyDictionary<string, int> Table
    {
        get
        {
            var table = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var role in Roles)
            {
                if (role is IWaveSpawnRole spawn)
                    table[role.Id] = spawn.WaveSpawnCount;
            }

            return table;
        }
    }

    /// <summary>
    /// 波次的主阵营：自定义阵营优先，未声明时回落到原版 <see cref="PlayerRoles.Team.OtherAlive"/>。
    /// </summary>
    /// <remarks>
    /// 与角色一致，用 <see cref="TeamRef"/> 同时承载自定义阵营与原版阵营；完整归属见 <see cref="Teams"/>。
    /// 需要固定原版阵营的波次直接覆写本属性返回 <see cref="PlayerRoles.Team"/> 即可（隐式转换）。
    /// </remarks>
    public virtual TeamRef Team => Teams.Count > 0
        ? TeamRef.FromCustom(Teams[0])
        : TeamRef.FromVanilla(PlayerRoles.Team.OtherAlive);

    /// <summary>
    /// 本波次归属的自定义阵营实例列表。
    /// </summary>
    /// <remarks>
    /// 波次初始化时框架会把本波次反向登记到这些阵营（<see cref="CustomTeam.Waves"/>）。
    /// 阵营与波次均为一次性实例化、跨回合复用，因此可直接持有实例引用。
    /// </remarks>
    public virtual IReadOnlyList<CustomTeam> Teams => [];

    private readonly List<CustomRole> _roles = [];

    /// <summary>本波次包含的自定义角色（角色实现 IWaveSpawnRole 时由框架反向登记）。</summary>
    public IReadOnlyList<CustomRole> Roles => _roles;

    /// <summary>登记一个归属本波次的角色（框架在波次实例化时调用）。</summary>
    /// <param name="role">角色模板实例。</param>
    internal void AddRole(CustomRole role)
    {
        if (role is not null && !_roles.Contains(role))
            _roles.Add(role);
    }

    /// <summary>是否参与设施威胁指数调整。</summary>
    public virtual bool ModifyThreat => true;

    /// <summary>自定义威胁调整值；小于零时由处理器按波次威胁等级计算。</summary>
    public virtual float CustomThreat => -1f;

    /// <summary>是否为生成的玩家附带出生保护。</summary>
    public virtual bool SpawnProtect => true;

    /// <summary>出生保护时长（秒）；<c>-1</c> 表示使用框架默认值。</summary>
    public virtual float SpawnProtectDuration => -1f;

    /// <summary>Announcer 播报正文；<see langword="null"/> 表示不播报。</summary>
    public virtual string? Announcement => null;

    /// <summary>Announcer 播报的翻译键或译文；<see langword="null"/> 表示不翻译。</summary>
    public virtual string? AnnouncementTranslation => null;

    /// <summary>波次配色：取主阵营中自定义阵营的配色，无自定义阵营时为白色。</summary>
    public virtual Color WaveColor => Team.CustomTeam?.TeamColor ?? Color.white;

    /// <summary>
    /// 按 <see cref="Table"/> 把玩家分配到各角色并套用。
    /// </summary>
    /// <param name="players">候选玩家。</param>
    /// <returns>实际成功套用的玩家（按分配顺序）。</returns>
    public virtual IReadOnlyList<Player> Spawn(IEnumerable<Player> players)
    {
        var table = Table;
        if (players is null || table.Count == 0)
            return Array.Empty<Player>();

        var spawner = CustomWaveRoleSpawner.Current;
        if (spawner is null)
        {
            LabApi.Features.Console.Logger.Error(
                $"[CustomWave] 波次 {Name}({Id}) 无法生成：未注册 ICustomWaveRoleSpawner。");
            return Array.Empty<Player>();
        }

        var candidates = new Queue<Player>(Shuffle(players.Where(CheckAllowSpawn).ToList()));
        var spawned = new List<Player>(candidates.Count);

        foreach (var slot in table)
        {
            var limit = slot.Value < 0 ? int.MaxValue : slot.Value;
            var taken = 0;

            while (taken < limit && candidates.Count > 0)
            {
                var player = candidates.Dequeue();
                if (player is null || !spawner.TrySpawn(slot.Key, player))
                    continue;

                ApplySpawnProtection(player);
                OnWavePlayerSpawned(player);
                spawned.Add(player);
                taken++;
            }
        }

        OnWaveSpawned(spawned);
        return spawned;
    }

    /// <summary>判断玩家是否允许被本波次刷新。</summary>
    /// <param name="player">候选玩家。</param>
    /// <returns>允许时返回 <see langword="true"/>。</returns>
    public virtual bool CheckAllowSpawn(Player player) => true;

    /// <summary>单个玩家生成完成后的钩子。</summary>
    /// <param name="player">已生成的玩家。</param>
    protected virtual void OnWavePlayerSpawned(Player player)
    {
    }

    /// <summary>全部玩家生成完成后的钩子。</summary>
    /// <param name="players">成功生成的玩家。</param>
    protected virtual void OnWaveSpawned(IReadOnlyList<Player> players)
    {
    }

    /// <summary>给刚生成的玩家施加出生保护。</summary>
    /// <param name="player">目标玩家。</param>
    private void ApplySpawnProtection(Player player)
    {
        if (!SpawnProtect)
            return;

        var duration = SpawnProtectDuration < 0f ? DefaultSpawnProtectDuration : SpawnProtectDuration;
        player.EnableEffect<CustomPlayerEffects.SpawnProtected>(1, duration);
    }

    /// <summary>波次实例被框架登记后调用；默认空实现，可覆写做实例级初始化。</summary>
    public virtual void OnRegisterEvents()
    {
    }

    /// <summary>波次实例从框架注销前调用；默认空实现，可覆写清理实例级资源。</summary>
    public virtual void OnUnregisterEvents()
    {
    }

    /// <summary>默认出生保护时长（秒）。</summary>
    protected virtual float DefaultSpawnProtectDuration => 10f;

    /// <summary>Fisher–Yates 原地洗牌。</summary>
    /// <param name="list">待洗牌列表。</param>
    /// <returns>同一列表。</returns>
    private static List<Player> Shuffle(List<Player> list)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = UnityEngine.Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }

        return list;
    }
}
