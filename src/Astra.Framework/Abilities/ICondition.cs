using System;

using LabApi.Features.Wrappers;

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

