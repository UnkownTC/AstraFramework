namespace Astra.HumeShield;

/// <summary>
/// 可临时阻断休谟护盾恢复的来源（技能、区域、状态）。
/// </summary>
/// <remarks>
/// 对齐原版 <c>PlayerRoles.PlayableScps.HumeShield.IHumeShieldBlocker</c>：
/// 阻断期间恢复速率按 0 处理，但护盾本身不会掉，阻断结束后立即继续恢复。
/// </remarks>
public interface ICustomHumeShieldBlocker
{
    /// <summary>当前是否正在阻断恢复。</summary>
    bool HumeShieldBlocked { get; }
}
