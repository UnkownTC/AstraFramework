using Astra.Roles;

using PlayerRoles;

namespace Astra.Waves;

/// <summary>
/// 波次的刷新型条目：角色（原版或自定义）+ 数量。
/// </summary>
/// <remarks>
/// 用于 <see cref="CustomWave.Spawns"/>：波次可额外声明/覆盖刷新项；
/// 自定义角色仍可通过 <see cref="IWaveSpawnRole"/> 声明默认数量，角色侧数值优先被这里的条目覆盖。
/// </remarks>
public readonly struct WaveSpawnEntry
{
    /// <summary>构造刷新型条目。</summary>
    /// <param name="role">角色条目。</param>
    /// <param name="count">数量；<c>-1</c> 表示填充剩余玩家。</param>
    public WaveSpawnEntry(RoleEntry role, int count)
    {
        Role = role;
        Count = count;
    }

    /// <summary>目标角色（原版或自定义）。</summary>
    public RoleEntry Role { get; }

    /// <summary>刷新数量；<c>-1</c> 表示填充剩余玩家。</summary>
    public int Count { get; }

    /// <summary>用自定义角色实例创建条目。</summary>
    /// <param name="role">自定义角色定义。</param>
    /// <param name="count">数量。</param>
    /// <returns>刷新型条目。</returns>
    public static WaveSpawnEntry Custom(CustomRole role, int count) => new(RoleEntry.Custom(role), count);

    /// <summary>用自定义角色 Id 创建条目。</summary>
    /// <param name="roleId">自定义角色 Id。</param>
    /// <param name="count">数量。</param>
    /// <returns>刷新型条目。</returns>
    public static WaveSpawnEntry Custom(string roleId, int count) => new(RoleEntry.Custom(roleId), count);

    /// <summary>用原版角色创建条目。</summary>
    /// <param name="role">原版角色类型。</param>
    /// <param name="count">数量。</param>
    /// <returns>刷新型条目。</returns>
    public static WaveSpawnEntry Vanilla(RoleTypeId role, int count) => new(RoleEntry.Vanilla(role), count);
}
