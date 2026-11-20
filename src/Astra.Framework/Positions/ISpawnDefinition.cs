using System.Collections.Generic;

namespace Astra.Positions;

/// <summary>
/// 表示带自定义刷新点的内容定义（角色、波次或物品）。
/// </summary>
/// <remarks>
/// <para>
/// 本接口只声明刷新点，不规定生成数量或生成实体：<see cref="Astra.Roles.StandardRole"/>
/// 会用 <see cref="CustomSpawnPoints"/> 在套用角色后选择出生点；回合开始自动生成由
/// <see cref="ISpawnOnRoundStart"/> 表示。
/// </para>
/// <para>
/// <see cref="CustomSpawnPoints"/> 为空表示「允许生成，但不进行传送」。角色/波次会保留
/// 原版分配位置；物品仍需要至少一个刷新点才能确定生成位置。
/// </para>
/// </remarks>
public interface ISpawnDefinition
{
    /// <summary>
    /// 自定义刷新点与对应的生成概率。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 键为刷新点，值为该点的生成概率：小于等于 <c>0</c> 表示该点不生成，
    /// 大于等于 <c>1</c> 表示必定生成；<see cref="float.NaN"/> 会被忽略。
    /// </para>
    /// <para>
    /// 空字典表示允许生成但不传送；刷新点会在真正生成时解析，因此筛选型
    /// <see cref="SpawnPointEntry"/> 每次解析可能落在不同房间。
    /// </para>
    /// </remarks>
    IReadOnlyDictionary<SpawnPointEntry, float> CustomSpawnPoints { get; }
}
