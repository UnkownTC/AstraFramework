using System;
using System.Text;

using Astra.Enums;
using Astra;

using LabApi.Features.Wrappers;

using PlayerRoles.Subroutines;
using Astra.Attributes;
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
    /// 标准技能的抽象基类：在 <see cref="CustomAbility"/> 基础上集成按键（<see cref="IKeybind"/>）、
    /// 冷却（<see cref="ICooldown"/>）与玩家条件（<see cref="IPlayerCondition"/>）。
    /// </summary>
    public abstract class StandardAbility : CustomAbility, IKeybind, ICooldown, IPlayerCondition
    {
        /// <summary>
        /// 获取技能绑定的服务器专属设置 SettingId（对应 SSKeybindSetting）。
        /// <para>
        /// <b>已降级为兜底值</b>：正常情况下框架会在技能加入玩家时按「技能槽顺序」
        /// 自动分配并写入 <see cref="CustomAbility.AssignedSettingId"/>，本声明值仅在
        /// 槽位耗尽（该玩家技能数超过 <c>CustomAbilityManager.MaxAbilitySlots</c>）时生效。
        /// </para>
        /// <para>-1 表示该技能没有按键绑定，适用于仅提供元数据的占位技能（不参与槽位分配）。</para>
        /// </summary>
        public virtual int SettingId => -1;

        /// <summary>获取或设置触发条件（默认始终为 true）。</summary>
        public virtual Func<Player, bool> Condition { get; protected set; } = player => true;

        /// <summary>获取按键触发类型（默认按下触发）。</summary>
        public virtual KeybindType KeybindType => KeybindType.Press;

        /// <summary>获取或设置按键当前是否被按下。</summary>
        public bool SyncIsPressed { get; set; }

        /// <summary>获取技能是否就绪（冷却结束且条件满足）。</summary>
        public bool IsReady => Cooldown.IsReady && Condition.Invoke(Owner);

        /// <summary>获取剩余冷却时间（秒）。</summary>
        public double Remaining => Cooldown.Remaining;

        /// <summary>获取底层冷却对象。</summary>
        public AbilityCooldown Cooldown { get; } = new();

        /// <summary>获取或设置冷却时长（秒）；默认值为 0（无冷却）。</summary>
        public virtual double CooldownTime { get; set; }

        /// <summary>按键松开时调用。</summary>
        public void KeyUp()
        {
            if (KeybindType == KeybindType.Hold)
                Triggered(Owner);
            OnKeyUp(Owner);
        }

        /// <summary>按键松开后的扩展钩子。</summary>
        protected virtual void OnKeyUp(Player player)
        {
        }

        /// <summary>按键按下时调用。</summary>
        public void KeyDown()
        {
            Triggered(Owner);
            OnKeyDown(Owner);
        }

        /// <summary>按键按下后的扩展钩子。</summary>
        protected virtual void OnKeyDown(Player player)
        {
        }

        /// <summary>触发失败时调用（框架内部调用）。</summary>
        internal virtual void TriggerFailed(Player player, AbilityFailReason failReason)
        {
            OnTriggerFailed(player, failReason);
        }

        /// <summary>触发失败时的扩展钩子。</summary>
        protected virtual void OnTriggerFailed(Player player, AbilityFailReason failReason)
        {
        }

        /// <summary>触发技能（框架内部调用），校验归属、冷却与条件。</summary>
        internal override void Triggered(Player player)
        {
            if (player != Owner)
                return;
            if (!IsReady)
            {
                var flag = Remaining >= 0 ? AbilityFailReason.NotReady : AbilityFailReason.ConditionFail;
                TriggerFailed(player, flag);
                return;
            }

            OnTriggered(player);
            TriggerCooldown(CooldownTime);
        }

        /// <summary>触发一次冷却。</summary>
        public virtual void TriggerCooldown(double cooldown)
        {
            if (cooldown == 0)
                return;
            Cooldown.Trigger(cooldown);
        }

        /// <summary>
        /// 将技能的显示状态写入 <see cref="StringBuilder"/>（形如 "[名称] 已就绪"）。
        /// </summary>
        public virtual StringBuilder AbilityStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("[");
            stringBuilder.Append(Name);
            stringBuilder.Append("] ");
            var status = (IsReady, Remaining <= 0) switch
            {
                (true, true) => "<color=green>已就绪</color>",
                (_, false) => $"<color=yellow>{Math.Ceiling(Remaining)}s</color>",
                (false, true) => "<color=red>未就绪</color>",
            };
            stringBuilder.Append(status);
            stringBuilder.AppendLine();
            return stringBuilder;
        }
    }

