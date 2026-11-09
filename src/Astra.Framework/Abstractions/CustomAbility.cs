using System;

using Astra.Interfaces;

using LabApi.Events.CustomHandlers;
using LabApi.Features.Wrappers;

// ReSharper disable MemberCanBePrivate.Global

namespace Astra.Abstractions
{
    /// <summary>
    /// 自定义技能的抽象基类。
    /// 继承该类并实现 <see cref="Id"/> 即可注册为全局技能模板，
    /// 框架会为每个玩家创建独立的技能实例并管理其生命周期。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>事件回调运行在模板实例上</b>：启动时每个技能类型只实例化一次，该实例既是模板，
    /// 也是本类覆写的 LabApi 事件的唯一接收者。因此事件回调里必须按玩家反查实例
    /// （<see cref="Apis.CustomAbilityApi.TryGetPlayerAbility{T}(LabApi.Features.Wrappers.Player, out T)"/>）
    /// 再操作，<b>不要把运行时状态存进实例字段</b> —— 那会污染所有玩家共用的模板。
    /// </para>
    /// <para>
    /// 玩家专属状态请放在 <see cref="Systems.CustomAbilitySystem.CustomAbilityManager"/> 查出的实例上，
    /// 或放进以 <c>ReferenceHub</c> 为键的静态表。
    /// </para>
    /// </remarks>
    public abstract class CustomAbility : CustomEventsHandler, IIdentifiable
    {
        /// <summary>获取技能的全局唯一标识。</summary>
        public abstract string Id { get; }

        /// <summary>
        /// 该实例实际生效的 SettingId 覆盖值；<c>-1</c> 表示沿用模板声明的 SettingId。
        /// 仅在把技能授予玩家且模板 SettingId 与该玩家既有按键冲突时由框架改写，
        /// **只作用于当前玩家专属实例，模板自身不受影响**。
        /// </summary>
        public int AssignedSettingId { get; internal set; } = -1;

        /// <summary>
        /// 获取该实例实际生效的 SettingId：已改配则返回改配值，否则回落到模板的
        /// <see cref="IKeybind.SettingId"/>（非按键技能恒为 <c>-1</c>）。
        /// </summary>
        public int EffectiveSettingId => this is IKeybind keybind
            ? (AssignedSettingId >= 0 ? AssignedSettingId : keybind.SettingId)
            : -1;

        /// <summary>获取技能的显示名称。</summary>
        public virtual string Name { get; } = string.Empty;

        /// <summary>获取技能的描述。</summary>
        public virtual string Description { get; } = string.Empty;

        /// <summary>技能当前归属的玩家。</summary>
        /// <remarks>
        /// <b>仅玩家专属实例有值，模板实例恒为 <see langword="null"/></b>。
        /// 事件回调运行在模板上，因此事件里不要用 <c>this.Owner</c>；
        /// 需要玩家实例时用 <see cref="Apis.CustomAbilityApi.TryGetPlayerAbility{T}(LabApi.Features.Wrappers.Player, out T)"/> 反查。
        /// </remarks>
        protected Player Owner { get; set; }

        /// <summary>将技能添加到指定玩家（框架内部调用）。</summary>
        internal void AddAbility(Player player)
        {
            Owner = player;
            OnAdded(player);
        }

        /// <summary>技能被添加到玩家时调用。</summary>
        protected virtual void OnAdded(Player player)
        {
        }

        /// <summary>将技能从指定玩家移除（框架内部调用）。</summary>
        internal void RemoveAbility(Player player)
        {
            Owner = null;
            OnRemoved(player);
        }

        /// <summary>技能从玩家移除时调用。</summary>
        protected virtual void OnRemoved(Player player)
        {
        }

        /// <summary>触发技能（框架内部调用）。</summary>
        internal virtual void Triggered(Player player)
        {
            if (player != Owner)
                return;
            OnTriggered(player);
        }

        /// <summary>技能被触发时调用。</summary>
        protected virtual void OnTriggered(Player player)
        {
        }

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is CustomAbility other && Id == other.Id;

        /// <inheritdoc />
        public override int GetHashCode() => Id.GetHashCode();

        /// <summary>
        /// 基于当前模板创建一个技能实例。默认使用反射无参构造，可覆写以自定义实例化逻辑。
        /// </summary>
        public virtual CustomAbility CreateInstance()
        {
            return (CustomAbility)Activator.CreateInstance(GetType());
        }
    }
}
