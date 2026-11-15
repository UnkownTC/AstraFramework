using System;
using System.Collections.Generic;

using Astra.Extensions;

using LabApi.Features.Wrappers;

using MapGeneration;

using UnityEngine;

namespace Astra.Positions;

/// <summary>
/// 位置条目：表示精确世界坐标，或「房间筛选条件 + 房间内本地坐标」。
/// </summary>
/// <remarks>
/// <para>
/// 精确世界坐标由 <see cref="Find(Vector3)"/> 创建；房间条目由具体房间或筛选条件创建。
/// </para>
/// <para>
/// 只给出房间筛选条件时，未给出的条件视为不限；解析时从全部匹配房间中随机选择。
/// 因此同一个筛选条目的 <see cref="WorldPosition"/> 在多次读取时可能不同。
/// </para>
/// </remarks>
public readonly struct PositionEntry
{
    private readonly RoomIdentifier? _room;
    private readonly bool _hasWorldPosition;
    private readonly Vector3 _worldPosition;

    /// <summary>房间名称；具体房间条目为其实际名称，筛选条目为筛选值。</summary>
    public RoomName RoomName { get; } = RoomName.Unnamed;

    /// <summary>区域；具体房间条目为其实际区域，筛选条目为筛选值。</summary>
    public FacilityZone FacilityZone { get; } = FacilityZone.None;

    /// <summary>房间形状；具体房间条目为其实际形状，筛选条目为筛选值。</summary>
    public RoomShape RoomShape { get; } = RoomShape.Undefined;

    /// <summary>房间内本地坐标；纯世界坐标条目为 <see cref="Vector3.zero"/>。</summary>
    public Vector3 LocalPosition { get; } = Vector3.zero;

    /// <summary>是否持有精确世界坐标。</summary>
    public bool HasWorldPosition => _hasWorldPosition;

    /// <summary>
    /// 解析后的世界坐标。房间条目会随机选择一个匹配房间；无法解析时返回 <see cref="Vector3.zero"/>。
    /// </summary>
    public Vector3 WorldPosition => TryGetWorldPosition(out var position) ? position : Vector3.zero;

    /// <summary>
    /// 匹配的房间集合。具体房间条目只包含该房间；世界坐标条目只包含包含该点的房间。
    /// </summary>
    public IReadOnlyList<RoomIdentifier> Rooms
    {
        get
        {
            if (ExactRoom is { } exactRoom)
                return new[] { exactRoom };

            if (_hasWorldPosition)
                return _worldPosition.TryGetRoom(out var room)
                    ? new[] { room }
                    : Array.Empty<RoomIdentifier>();

            RoomName? name = RoomName == RoomName.Unnamed ? null : RoomName;
            FacilityZone? zone = FacilityZone == FacilityZone.None ? null : FacilityZone;
            RoomShape? shape = RoomShape == RoomShape.Undefined ? null : RoomShape;

            var rooms = new List<RoomIdentifier>();
            RoomUtils.FindRooms(name, zone, shape, rooms);
            return rooms;
        }
    }

    /// <summary>随机选择一个匹配房间；具体房间条目返回该房间。</summary>
    public RoomIdentifier? Room => ExactRoom ?? Rooms.GetRandom();

    private RoomIdentifier? ExactRoom =>
        _room is { } room && room != null ? room : null;

    private PositionEntry(RoomIdentifier room, Vector3 localPosition)
    {
        _room = room ?? throw new ArgumentNullException(nameof(room));
        RoomName = room.Name;
        FacilityZone = room.Zone;
        RoomShape = room.Shape;
        LocalPosition = localPosition;
    }

    private PositionEntry(RoomName? roomName, FacilityZone? zone, RoomShape? shape, Vector3 localPosition)
    {
        RoomName = roomName ?? RoomName.Unnamed;
        FacilityZone = zone ?? FacilityZone.None;
        RoomShape = shape ?? RoomShape.Undefined;
        LocalPosition = localPosition;
    }

    private PositionEntry(Vector3 worldPosition)
    {
        _hasWorldPosition = true;
        _worldPosition = worldPosition;
    }

    /// <summary>按世界坐标创建；若坐标位于房间内，会同时记录具体房间和本地坐标。</summary>
    /// <param name="worldPosition">世界坐标。</param>
    /// <returns>位置条目。</returns>
    public static PositionEntry Find(Vector3 worldPosition)
    {
        if (!worldPosition.TryGetRoom(out var room))
            return new PositionEntry(worldPosition);

        return new PositionEntry(room, room.transform.InverseTransformPoint(worldPosition));
    }

    /// <summary>按具体房间 + 房间内本地坐标创建。</summary>
    /// <param name="room">具体房间。</param>
    /// <param name="localPosition">房间内本地坐标。</param>
    /// <returns>位置条目。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="room"/> 为 <see langword="null"/>。</exception>
    public static PositionEntry Find(RoomIdentifier room, Vector3 localPosition) =>
        new(room, localPosition);

    /// <summary>按房间名称 + 本地坐标创建；多个匹配房间时解析阶段随机选房。</summary>
    /// <param name="roomName">房间名称。</param>
    /// <param name="localPosition">房间内本地坐标。</param>
    /// <returns>位置条目。</returns>
    public static PositionEntry Find(RoomName roomName, Vector3 localPosition) =>
        new(roomName, null, null, localPosition);

    /// <summary>按区域 + 本地坐标创建；多个匹配房间时解析阶段随机选房。</summary>
    /// <param name="zone">设施区域。</param>
    /// <param name="localPosition">房间内本地坐标。</param>
    /// <returns>位置条目。</returns>
    public static PositionEntry Find(FacilityZone zone, Vector3 localPosition) =>
        new(null, zone, null, localPosition);

    /// <summary>按房间形状 + 本地坐标创建；多个匹配房间时解析阶段随机选房。</summary>
    /// <param name="shape">房间形状。</param>
    /// <param name="localPosition">房间内本地坐标。</param>
    /// <returns>位置条目。</returns>
    public static PositionEntry Find(RoomShape shape, Vector3 localPosition) =>
        new(null, null, shape, localPosition);

    /// <summary>按组合筛选条件 + 本地坐标创建；未给出的条件视为不限，解析阶段随机选房。</summary>
    /// <param name="roomName">房间名称筛选；<see langword="null"/> 表示不限。</param>
    /// <param name="zone">区域筛选；<see langword="null"/> 表示不限。</param>
    /// <param name="shape">房间形状筛选；<see langword="null"/> 表示不限。</param>
    /// <param name="localPosition">房间内本地坐标。</param>
    /// <returns>位置条目。</returns>
    public static PositionEntry Find(RoomName? roomName, FacilityZone? zone, RoomShape? shape,
        Vector3 localPosition) =>
        new(roomName, zone, shape, localPosition);

    /// <summary>创建「随机房间 + 本地坐标」条目。</summary>
    /// <param name="localPosition">房间内本地坐标。</param>
    /// <returns>位置条目。</returns>
    public static PositionEntry Random(Vector3 localPosition = default) =>
        new(null, null, null, localPosition);

    /// <summary>尝试解析世界坐标；筛选条目会随机选择一个匹配房间。</summary>
    /// <param name="position">解析出的世界坐标；失败时为 <see cref="Vector3.zero"/>。</param>
    /// <returns>成功解析返回 <see langword="true"/>。</returns>
    public bool TryGetWorldPosition(out Vector3 position)
    {
        if (_hasWorldPosition)
        {
            position = _worldPosition;
            return true;
        }

        var room = Room;
        if (room is null)
        {
            position = default;
            return false;
        }

        position = room.transform.TransformPoint(LocalPosition);
        return true;
    }

    /// <summary>
    /// 将玩家传送到本条目解析出的世界坐标（不修改朝向）。
    /// </summary>
    /// <param name="player">目标玩家。</param>
    /// <returns>位置可解析且玩家有效时返回 <see langword="true"/>。</returns>
    public bool Teleport(Player player) => player.Teleport(this);
}
