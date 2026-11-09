using PlayerRoles.Subroutines;

namespace Astra.Abilities;
    /// <summary>
    /// 表示一个带有冷却时间的能力。
    /// </summary>
    public interface ICooldown
    {
        /// <summary>获取能力当前是否处于可用（冷却已结束）状态。</summary>
        bool IsReady { get; }

        /// <summary>获取冷却时长（秒）。</summary>
        double CooldownTime { get; }

        /// <summary>获取剩余冷却时间（秒）。</summary>
        double Remaining { get; }

        /// <summary>获取底层冷却对象。</summary>
        AbilityCooldown Cooldown { get; }

        /// <summary>触发一次冷却。</summary>
        /// <param name="cooldown">冷却时长（秒）。</param>
        void TriggerCooldown(double cooldown);
    }

