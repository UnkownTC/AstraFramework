using System;

using LabApi.Features.Wrappers;
using Astra;
using Astra.Attributes;
using Astra.Enums;
using Astra.Extensions;
using Astra.Roles;
using Astra.Teams;
using Astra.Waves;
using Astra.Items;
using Astra.Effects;
using Astra.HumeShield;
using Astra.Patching;
using Astra.Hints;
using Astra.Audio;
using Astra.Countdown;
using Astra.Cancellation;
using Astra.ServerSpecificSettings;
using Astra.Loader;

namespace Astra.Abilities;
    /// <summary>
    /// 表示一个带有玩家触发条件的能力。
    /// </summary>
    public interface IPlayerCondition
    {
        /// <summary>
        /// 获取用于判断能力是否可用的玩家条件委托。
        /// </summary>
        Func<Player, bool> Condition { get; }
    }

