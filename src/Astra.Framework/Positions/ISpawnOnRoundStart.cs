namespace Astra.Positions;

/// <summary>
/// 回合开始自动生成契约：在 <see cref="ISpawnDefinition"/> 的刷新点基础上增加生成数量上下限。
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Astra.Spawning.RoundStartSpawnManager"/> 只收集本接口的实现；仅实现
/// <see cref="ISpawnDefinition"/> 的 <see cref="Astra.Roles.StandardRole"/> 不参与自动生成。
/// </para>
/// <para>
/// 需要按人数/阵营等条件限制时，同时实现 <see cref="ISpawnCondition"/>。
/// </para>
/// </remarks>
public interface ISpawnOnRoundStart : ISpawnDefinition
{
    /// <summary>
    /// 单次生成的最少实体数量。
    /// </summary>
    /// <remarks>
    /// 小于等于 <c>0</c> 表示不设下限；管理器会尽量补足到该数量，但受
    /// <see cref="MaxSpawnCount"/>、可用刷新点与可用玩家限制。
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
}
