using UnityEngine;
using Astra;
using Astra.Attributes;
using Astra.Enums;
using Astra.Extensions;
using Astra.Roles;
using Astra.Teams;
using Astra.Waves;
using Astra.Items;
using Astra.Effects;
using Astra.Abilities;
using Astra.Patching;
using Astra.Hints;
using Astra.Audio;
using Astra.Countdown;
using Astra.Cancellation;
using Astra.ServerSpecificSettings;
using Astra.Loader;

namespace Astra.HumeShield;

/// <summary>
/// 自定义角色的休谟护盾参数。
/// </summary>
/// <remarks>
/// <para>
/// 本接口只描述「护盾长什么样」，不负责驱动恢复。每帧恢复、受伤冷却、上限同步
/// 统一由 <c>CustomHumeShieldManager</c> 承担，行为对齐原版
/// <c>PlayerRoles.PlayableScps.HumeShield.DynamicHumeShieldController</c>。
/// </para>
/// <para>
/// 为什么不直接继承 <c>HumeShieldStat</c>：其 <c>Update()</c> 在运行时程序集中是
/// <c>internal virtual</c>，外部程序集无法真正覆写（publicized 引用能编译，但运行时会
/// 因可见性不匹配而失败）。
/// </para>
/// </remarks>
public interface ICustomHumeShieldStat
{
    /// <summary>角色是否已销毁。销毁后护盾立即收口。</summary>
    bool IsDestroyed { get; }

    /// <summary>
    /// 护盾上限。仅在 <see cref="HumeShieldCurve"/> 为 <see langword="null"/> 时生效。
    /// </summary>
    float MaxHumeShield { get; }

    /// <summary>
    /// 上限随血量比例的曲线，复刻原版 <c>ShieldOverHealth</c>。
    /// </summary>
    /// <remarks>
    /// 横轴为当前血量比例（0~1），纵轴为**护盾绝对值**（不是倍率）。
    /// 为 <see langword="null"/> 时退化为 <see cref="MaxHumeShield"/> 常数。
    /// </remarks>
    AnimationCurve HumeShieldCurve { get; }

    /// <summary>每秒恢复量。</summary>
    float HumeShieldRegenRate { get; }

    /// <summary>受伤后延迟多少秒才开始恢复。</summary>
    float HumeShieldRegenDelay { get; }

    // 注：护盾条警示色与「空盾隐藏」由原版 IHumeShieldProvider 驱动，
    // 自定义角色无法通过本接口影响它们，故不提供这两个成员，避免出现「设了没效果」的假 API。
}
