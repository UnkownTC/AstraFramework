using LabApi.Features.Wrappers;

namespace Astra.Positions;

/// <summary>
/// 位置条目的玩家传送扩展。
/// </summary>
public static class PositionTeleport
{
    /// <summary>
    /// 将玩家传送到位置条目解析出的世界坐标（不修改朝向）。
    /// </summary>
    /// <param name="player">目标玩家。</param>
    /// <param name="position">位置条目。</param>
    /// <returns>位置可解析且玩家有效时返回 <see langword="true"/>。</returns>
    public static bool Teleport(this Player player, PositionEntry position)
    {
        if (player is null || !position.TryGetWorldPosition(out var worldPosition))
            return false;

        player.Position = worldPosition;
        return true;
    }

    /// <summary>
    /// 将玩家传送到刷怪点解析出的世界坐标与朝向。
    /// </summary>
    /// <param name="player">目标玩家。</param>
    /// <param name="spawnPoint">刷怪点条目。</param>
    /// <returns>位置可解析且玩家有效时返回 <see langword="true"/>。</returns>
    public static bool Teleport(this Player player, SpawnPointEntry spawnPoint)
    {
        if (player is null || !spawnPoint.TryResolve(out var worldPosition, out var rotation))
            return false;

        player.Position = worldPosition;
        player.Rotation = rotation;
        return true;
    }
}
