namespace Astra.Positions;

/// <summary>
/// 回合开始生成条件。管理器在生成前调用一次；返回 <see langword="false"/> 时本次跳过该提供者。
/// </summary>
/// <remarks>
/// 只影响 <see cref="Astra.Spawning.RoundStartSpawnManager"/> 的自动生成，不影响手动调用
/// <see cref="CustomRoleApi.AddRole"/> 等直接注册/套用行为。
/// </remarks>
public interface ISpawnCondition
{
    /// <summary>
    /// 本次回合开始是否允许生成。
    /// </summary>
    /// <returns>允许生成返回 <see langword="true"/>；跳过返回 <see langword="false"/>。</returns>
    bool CanSpawnOnRoundStart();
}
