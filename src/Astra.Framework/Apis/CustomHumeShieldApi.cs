using Astra.Interfaces;
using Astra.Systems.CustomHumeShieldSystem;

using LabApi.Features.Wrappers;

namespace Astra.Apis;

/// <summary>自定义护盾对外 API：护盾驱动器的挂载与移除。</summary>
public static class CustomHumeShieldApi
{
    /// <summary>给玩家挂载自定义护盾驱动器。</summary>
    /// <param name="player">目标玩家。</param>
    /// <param name="stat">护盾参数。</param>
    public static void Apply(Player player, ICustomHumeShieldStat stat) =>
        CustomHumeShieldManager.AddHumeShield(player, stat);

    /// <summary>移除玩家的自定义护盾驱动器（护盾清零并解除覆盖锁）。</summary>
    /// <param name="player">目标玩家。</param>
    public static void Remove(Player player) => CustomHumeShieldManager.RemoveHumeShield(player);
}
