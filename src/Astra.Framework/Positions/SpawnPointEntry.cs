using MapGeneration;

using UnityEngine;

namespace Astra.Positions;

/// <summary>
/// 刷怪点条目：位置 + 可选旋转。
/// </summary>
/// <remarks>
/// 位置由 <see cref="PositionEntry"/> 表达；未指定旋转时，<see cref="TryResolve"/> 会生成一个随机水平朝向。
/// 位置条件不明确时（只给房间筛选或完全未指定），解析会随机选择匹配房间。
/// </remarks>
public readonly struct SpawnPointEntry
{
    /// <summary>构造刷怪点条目。</summary>
    /// <param name="position">位置条目。</param>
    /// <param name="rotation">旋转；<see langword="null"/> 表示解析时随机。</param>
    public SpawnPointEntry(PositionEntry position, Quaternion? rotation = null)
    {
        Position = position;
        Rotation = rotation;
    }

    /// <summary>位置条目。</summary>
    public PositionEntry Position { get; }

    /// <summary>显式旋转；未指定时为 <see langword="null"/>。</summary>
    public Quaternion? Rotation { get; }

    /// <summary>是否显式指定了旋转。</summary>
    public bool HasRotation => Rotation.HasValue;

    /// <summary>解析后的世界坐标；条件不明确时可能随机变化。</summary>
    public Vector3 WorldPosition => Position.WorldPosition;

    /// <summary>解析为具体的世界坐标与旋转。</summary>
    /// <param name="position">解析出的世界坐标。</param>
    /// <param name="rotation">解析出的旋转；未指定时为一个随机水平朝向。</param>
    /// <returns>位置可解析时返回 <see langword="true"/>。</returns>
    public bool TryResolve(out Vector3 position, out Quaternion rotation)
    {
        if (!Position.TryGetWorldPosition(out position))
        {
            rotation = default;
            return false;
        }

        rotation = Rotation ?? RandomYaw();
        return true;
    }

    /// <summary>解析旋转；未指定时返回一个随机水平朝向。</summary>
    /// <returns>旋转。</returns>
    public Quaternion ResolveRotation() => Rotation ?? RandomYaw();

    /// <summary>按世界坐标创建刷怪点。</summary>
    /// <param name="worldPosition">世界坐标。</param>
    /// <param name="rotation">旋转；<see langword="null"/> 表示解析时随机。</param>
    /// <returns>刷怪点条目。</returns>
    public static SpawnPointEntry Find(Vector3 worldPosition, Quaternion? rotation = null) =>
        new(PositionEntry.Find(worldPosition), rotation);

    /// <summary>按具体房间 + 本地坐标创建刷怪点。</summary>
    /// <param name="room">具体房间。</param>
    /// <param name="localPosition">房间内本地坐标。</param>
    /// <param name="rotation">旋转；<see langword="null"/> 表示解析时随机。</param>
    /// <returns>刷怪点条目。</returns>
    public static SpawnPointEntry Find(RoomIdentifier room, Vector3 localPosition,
        Quaternion? rotation = null) =>
        new(PositionEntry.Find(room, localPosition), rotation);

    /// <summary>按房间名称 + 本地坐标创建刷怪点；多个匹配房间时解析阶段随机选择。</summary>
    /// <param name="roomName">房间名称。</param>
    /// <param name="localPosition">房间内本地坐标。</param>
    /// <param name="rotation">旋转；<see langword="null"/> 表示解析时随机。</param>
    /// <returns>刷怪点条目。</returns>
    public static SpawnPointEntry Find(RoomName roomName, Vector3 localPosition,
        Quaternion? rotation = null) =>
        new(PositionEntry.Find(roomName, localPosition), rotation);

    /// <summary>按区域 + 本地坐标创建刷怪点；多个匹配房间时解析阶段随机选择。</summary>
    /// <param name="zone">设施区域。</param>
    /// <param name="localPosition">房间内本地坐标。</param>
    /// <param name="rotation">旋转；<see langword="null"/> 表示解析时随机。</param>
    /// <returns>刷怪点条目。</returns>
    public static SpawnPointEntry Find(FacilityZone zone, Vector3 localPosition,
        Quaternion? rotation = null) =>
        new(PositionEntry.Find(zone, localPosition), rotation);

    /// <summary>按房间形状 + 本地坐标创建刷怪点；多个匹配房间时解析阶段随机选择。</summary>
    /// <param name="shape">房间形状。</param>
    /// <param name="localPosition">房间内本地坐标。</param>
    /// <param name="rotation">旋转；<see langword="null"/> 表示解析时随机。</param>
    /// <returns>刷怪点条目。</returns>
    public static SpawnPointEntry Find(RoomShape shape, Vector3 localPosition,
        Quaternion? rotation = null) =>
        new(PositionEntry.Find(shape, localPosition), rotation);

    /// <summary>按组合筛选条件 + 本地坐标创建刷怪点；未给出的条件视为不限，解析阶段随机选房。</summary>
    /// <param name="roomName">房间名称筛选；<see langword="null"/> 表示不限。</param>
    /// <param name="zone">区域筛选；<see langword="null"/> 表示不限。</param>
    /// <param name="shape">房间形状筛选；<see langword="null"/> 表示不限。</param>
    /// <param name="localPosition">房间内本地坐标。</param>
    /// <param name="rotation">旋转；<see langword="null"/> 表示解析时随机。</param>
    /// <returns>刷怪点条目。</returns>
    public static SpawnPointEntry Find(RoomName? roomName, FacilityZone? zone, RoomShape? shape,
        Vector3 localPosition, Quaternion? rotation = null) =>
        new(PositionEntry.Find(roomName, zone, shape, localPosition), rotation);

    /// <summary>创建「随机房间 + 本地坐标」刷怪点。</summary>
    /// <param name="localPosition">房间内本地坐标。</param>
    /// <param name="rotation">旋转；<see langword="null"/> 表示解析时随机。</param>
    /// <returns>刷怪点条目。</returns>
    public static SpawnPointEntry Random(Vector3 localPosition = default, Quaternion? rotation = null) =>
        new(PositionEntry.Random(localPosition), rotation);

    private static Quaternion RandomYaw() =>
        Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
}
