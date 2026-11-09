using System;
using System.Collections.Generic;

using Astra.Abstractions;
using Astra.Attributes;
using Astra.Interfaces;
using Astra.Systems.EventSystem;

using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.CustomHandlers;
using LabApi.Features.Console;

namespace Astra.Systems.CustomAbilitySystem
{
    /// <summary>
    /// 技能模板管理器：保存全局技能模板（按 Id / Type 索引），
    /// 并监听玩家加入 / 离开 / 换角色，自动维护每个玩家的技能实例。
    /// </summary>
    [ManagedEvent("Astra - CustomAbilityTemplateManager")]
    internal sealed class CustomAbilityTemplateManager : CustomEventsHandler
    {
        private static readonly Dictionary<string, Abstractions.CustomAbility> RegisteredAbilities = new();
        private static readonly Dictionary<Type, Abstractions.CustomAbility> AbilitiesByType = new();

        /// <summary>获取当前已注册的全部技能模板。</summary>
        internal static IReadOnlyCollection<CustomAbility> AbilityTemplates => RegisteredAbilities.Values;

        /// <summary>按类型注册技能模板（框架内部调用）。</summary>
        /// <remarks>
        /// <see cref="RegisteredAbilities"/> 以 <see cref="IIdentifiable.Id"/> 为全局唯一键。
        /// 不同类型声明了相同 Id 时**拒绝新模板并保留旧模板**（绝不静默覆盖抛弃）；
        /// 同一类型重复注册则直接跳过（AutoRegister 幂等场景）。
        /// </remarks>
        internal static void RegisterAbility(Type type)
        {
            if (AbilitiesByType.TryGetValue(type, out var existedAbilityTemplate))
            {
                Logger.Warn($"[CustomAbilityTemplateManager] 发现冲突Id {existedAbilityTemplate.Id}");
                Logger.Warn($"[CustomAbilityTemplateManager] {type.FullName} 与 {existedAbilityTemplate.GetType().FullName} 发生冲突");
                return;
            }
            if (Activator.CreateInstance(type) is not Abstractions.CustomAbility ability)
                return;

            if (RegisteredAbilities.TryGetValue(ability.Id, out var occupiedById) &&
                occupiedById.GetType() != type)
            {
                Logger.Warn($"[CustomAbilityTemplateManager] 技能 Id {ability.Id} 已被 {occupiedById.GetType().FullName} 占用，" +
                            $"{type.FullName} 注册被拒绝（不覆盖既有模板）");
                return;
            }

            RegisteredAbilities[ability.Id] = ability;
            AbilitiesByType[type] = ability;

            // 模板实例同时承担该类技能的事件监听（事件回调内按玩家查实例，不在模板上存状态）。
            EventManager.RegisterInstance(ability, type);
        }

        /// <inheritdoc />
        public override void OnPlayerJoined(PlayerJoinedEventArgs ev)
        {
            base.OnPlayerJoined(ev);
            CustomAbilityManager.AddManager(ev.Player);
        }

        /// <inheritdoc />
        public override void OnPlayerLeft(PlayerLeftEventArgs ev)
        {
            base.OnPlayerLeft(ev);
            CustomAbilityManager.RemoveManager(ev.Player);
        }

        /// <inheritdoc />
        public override void OnPlayerChangingRole(PlayerChangingRoleEventArgs ev)
        {
            base.OnPlayerChangingRole(ev);
            CustomAbilityManager.GetAbilityManager(ev.Player)?.ClearAll();
        }

        /// <inheritdoc />
        public override void OnPlayerChangedRole(PlayerChangedRoleEventArgs ev)
        {
            base.OnPlayerChangedRole(ev);
            if (!CustomAbilityManager.TryGetAbilityManager(ev.Player, out var manager))
                return;
            foreach (var ability in RegisteredAbilities.Values)
            {
                if (ability is not IOriginalRole roleAbility)
                    continue;
                if (ev.NewRole.RoleTypeId != roleAbility.RoleType)
                    continue;
                manager.AddAbility(ability.Id);
            }
        }

        /// <summary>按 Id 获取技能模板。</summary>
        internal static bool TryGetAbilityTemplate(string id, out CustomAbility ability)
        {
            return RegisteredAbilities.TryGetValue(id, out ability);
        }

        /// <summary>按类型获取技能模板。</summary>
        internal static bool TryGetAbilityTemplate(Type type, out CustomAbility ability)
        {
            return AbilitiesByType.TryGetValue(type, out ability);
        }

        /// <summary>按泛型类型获取技能模板。</summary>
        internal static bool TryGetAbilityTemplate<T>(out T ability) where T : CustomAbility
        {
            var flag = TryGetAbilityTemplate(typeof(T), out var ability1);
            ability = ability1 as T;
            return flag && ability is not null;
        }
    }
}
