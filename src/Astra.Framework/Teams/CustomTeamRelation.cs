using Astra;
using Astra.Attributes;
using Astra.Enums;
using Astra.Extensions;
using Astra.Roles;
using Astra.Waves;
using Astra.Items;
using Astra.Effects;
using Astra.Abilities;
using Astra.HumeShield;
using Astra.Patching;
using Astra.Hints;
using Astra.Audio;
using Astra.Countdown;
using Astra.Cancellation;
using Astra.ServerSpecificSettings;
using Astra.Loader;

namespace Astra.Teams;

/// <summary>
/// 自定义阵营与外界的关系模式。
/// </summary>
public enum CustomTeamRelation
{
    /// <summary>
    /// 自定义关系：由 <see cref="CustomTeam.Blacklist"/> / <see cref="CustomTeam.Whitelist"/> 与
    /// <see cref="CustomTeam.KeepVanillaRelate"/> 共同决定。
    /// </summary>
    Custom = 0,

    /// <summary>与所有其它阵营友好（忽略黑白名单与 <see cref="CustomTeam.KeepVanillaRelate"/>）。</summary>
    AllFriendly,

    /// <summary>与所有其它阵营敌对（忽略黑白名单与 <see cref="CustomTeam.KeepVanillaRelate"/>）。</summary>
    AllHostile,
}
