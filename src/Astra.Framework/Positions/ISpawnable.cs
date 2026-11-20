using System.Collections.Generic;

namespace Astra.Positions;

/// <summary>
/// 表示可在指定时机生成的内容（角色、波次或物品）。
/// </summary>
/// <remarks>
/// <para>
/// 本接口只声明生成策略，不规定生成什么实体；框架由
/// <c>Astra.Spawning.RoundStartSpawnManager</c> 统一读取并在原版角色分配完成后生成。
/// </para>
/// <para>
/// <see cref="SpawnOnRoundStart"/> 用于显式选择是否参与回合开始生成；实现方默认应返回
/// <see langword="false"/>，避免无意间改变服务器回合流程。
/// </para>
/// <para>
/// <see cref="SpawnPoints"/> 的空字典表示「允许生成，但不进行传送」。角色/波次会保留
/// 原版分配位置；物品仍需要至少一个刷怪点才能确定生成位置。
/// </para>
/// </remarks>
public interface ISpawnable
{
    /// <summary>
    /// 是否参与回合开始生成。
    /// </summary>
    /// <remarks>默认应为 <see langword="false"/>；只有显式返回 <see langword="true"/> 的实现才会被管理器执行。</remarks>
    bool SpawnOnRoundStart { get; }

    /// <summary>
    /// 单次生成的最少实体数量。
    /// </summary>
    /// <remarks>
    /// 小于等于 <c>0</c> 表示不设下限；管理器会尽量补足到该数量，但受
    /// <see cref="MaxSpawnCount"/>、可用刷怪点与可用玩家限制。
    /// </remarks>
    int MinAmount { get; }

    /// <summary>
    /// 单次生成的最多实体数量。
    /// </summary>
    /// <remarks>
    /// 对波次/角色实现表示最多处理的玩家数，对物品实现表示最多生成的物品数；
    /// 小于等于 <c>0</c> 表示不生成，<see cref="int.MaxValue"/> 表示不限制。
    /// </remarks>
    int MaxSpawnCount { get; }

    /// <summary>
    /// 刷怪点与对应的生成概率。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 键为刷怪点，值为该点的生成概率：小于等于 <c>0</c> 表示该点不生成，
    /// 大于等于 <c>1</c> 表示必定生成；<see cref="float.NaN"/> 会被忽略。
    /// </para>
    /// <para>
    /// 空字典表示允许生成但不传送；刷怪点会在真正生成时解析，因此筛选型
    /// <see cref="SpawnPointEntry"/> 每次解析可能落在不同房间。
    /// </para>
    /// </remarks>
    IReadOnlyDictionary<SpawnPointEntry, float> SpawnPoints { get; }
}
