using System.Collections.Generic;

namespace Astra.Positions;

/// <summary>
/// 表示可以在回合开始时于若干刷怪点生成的内容。
/// </summary>
/// <remarks>
/// <para>
/// 本接口只声明「在哪些点生成、各点的概率是多少」，不规定生成什么实体；
/// 具体由消费方（刷怪系统 / 外部内容插件）读取 <see cref="SpawnPoints"/> 后决定。
/// </para>
/// <para>
/// 概率按条目独立理解：值应位于 <c>0</c>~<c>1</c>；小于等于 <c>0</c> 表示该点不生成，
/// 大于等于 <c>1</c> 表示必定生成。实现方应返回稳定的只读字典，空字典表示不在回合开始时生成。
/// </para>
/// </remarks>
public interface ISpawnOnRoundStart
{
    /// <summary>
    /// 刷怪点与对应的生成概率。
    /// </summary>
    /// <remarks>
    /// 键为刷怪点，值为该点的生成概率；刷怪点会在真正生成时解析，因此筛选型
    /// <see cref="SpawnPointEntry"/> 每次解析可能落在不同房间。
    /// </remarks>
    IReadOnlyDictionary<SpawnPointEntry, float> SpawnPoints { get; }
}
