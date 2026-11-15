using System.Collections.Generic;

namespace Astra.Positions;

/// <summary>
/// 表示可以在回合开始时于若干刷怪点生成的内容。
/// </summary>
/// <remarks>
/// <para>
/// 本接口只声明「在哪些点生成、各点的概率是多少」，不规定生成什么实体；
/// 框架由 <c>Astra.Spawning.RoundStartSpawnManager</c> 统一读取并生成，外部插件也可自行读取。
/// </para>
/// <para>
/// 概率按条目独立理解：值应位于 <c>0</c>~<c>1</c>；小于等于 <c>0</c> 表示该点不生成，
/// 大于等于 <c>1</c> 表示必定生成。实现方应返回稳定的只读字典，空字典表示不在回合开始时生成。
/// </para>
/// </remarks>
public interface ISpawnOnRoundStart
{
    /// <summary>
    /// 单次回合开始最多生成的实体数量。
    /// </summary>
    /// <remarks>
    /// 对波次/角色实现表示最多替换的玩家数，对物品实现表示最多生成的物品数；
    /// 小于等于 <c>0</c> 表示不生成，<see cref="int.MaxValue"/> 表示不限制。
    /// </remarks>
    int MaxSpawnCount { get; }

    /// <summary>
    /// 刷怪点与对应的生成概率。
    /// </summary>
    /// <remarks>
    /// 键为刷怪点，值为该点的生成概率；刷怪点会在真正生成时解析，因此筛选型
    /// <see cref="SpawnPointEntry"/> 每次解析可能落在不同房间。
    /// </remarks>
    IReadOnlyDictionary<SpawnPointEntry, float> SpawnPoints { get; }
}
