using Astra.Roles;

using LabApi.Features.Wrappers;

namespace Astra.Waves;

/// <summary>
/// 扩展的角色生成器：同时接收原版与自定义角色条目。
/// </summary>
/// <remarks>
/// 实现本接口后，<see cref="CustomWave.Spawn"/> 会把 <see cref="RoleEntry"/> 原样交给生成器；
/// 未实现的旧 <see cref="ICustomWaveRoleSpawner"/> 只会收到自定义角色的 Id 字符串。
/// </remarks>
public interface ICustomWaveRoleSpawnerV2 : ICustomWaveRoleSpawner
{
    /// <summary>把玩家套用为指定角色条目（原版或自定义）。</summary>
    /// <param name="role">角色条目。</param>
    /// <param name="player">目标玩家。</param>
    /// <returns>成功套用返回 <see langword="true"/>。</returns>
    bool TrySpawn(RoleEntry role, Player player);
}
