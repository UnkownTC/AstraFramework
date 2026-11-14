using System;
using System.Collections.Generic;
using System.Linq;

using Astra.Attributes;
using Astra.Roles;

using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.CustomHandlers;
using LabApi.Features.Console;

using EventManager = Astra.Events.EventManager;

namespace Astra.Abilities;
    /// <summary>
    /// 技能模板管理器：保存全局技能模板（按 Id / Type 索引），
    /// 并监听玩家加入 / 离开 / 换角色，自动维护每个玩家的技能实例。
    /// </summary>
    [ManagedEvent("Astra - CustomAbilityTemplateManager")]
    internal sealed class CustomAbilityTemplateManager : CustomEventsHandler
    {
        private static readonly Dictionary<string, CustomAbility> RegisteredAbilities = new();
        private static readonly Dictionary<Type, CustomAbility> AbilitiesByType = new();

        /// <summary>获取当前已注册的全部技能模板。</summary>
        internal static IReadOnlyCollection<CustomAbility> AbilityTemplates => RegisteredAbilities.Values;

        /// <summary>按类型注册技能模板（框架内部调用）。</summary>
        /// <remarks>
        /// <see cref="RegisteredAbilities"/> 以 <see cref="IIdentifiable.Id"/> 为全局唯一键。
        /// 不同类型声明了相同 Id 时**拒绝新模板并保留旧模板**（绝不静默覆盖抛弃）；
        /// 同一类型重复注册则直接跳过（AutoRegister 幂等场景）。
        /// </remarks>
        internal static bool RegisterAbility(Type type)
        {
            if (type is null)
                return false;

            if (AbilitiesByType.TryGetValue(type, out var existedAbilityTemplate))
            {
                Logger.Warn($"[CustomAbilityTemplateManager] 发现冲突Id {existedAbilityTemplate.Id}");
                Logger.Warn($"[CustomAbilityTemplateManager] {type.FullName} 与 {existedAbilityTemplate.GetType().FullName} 发生冲突");
                return false;
            }
            if (Activator.CreateInstance(type) is not CustomAbility ability)
                return false;

            return RegisterInstance(ability);
        }

        /// <summary>按实例注册技能模板（外部框架 / JSON 适配器用）。</summary>
        /// <param name="ability">技能模板实例。</param>
        /// <returns>登记成功（或同实例重复登记）返回 <see langword="true"/>；Id 冲突返回 <see langword="false"/>。</returns>
        internal static bool RegisterInstance(CustomAbility ability)
        {
            if (ability is null || string.IsNullOrWhiteSpace(ability.Id))
                return false;

            if (RegisteredAbilities.TryGetValue(ability.Id, out var occupiedById))
            {
                if (ReferenceEquals(occupiedById, ability))
                    return true;

                Logger.Warn($"[CustomAbilityTemplateManager] 技能 Id {ability.Id} 已被 " +
                            $"{occupiedById.GetType().FullName} 占用，{ability.GetType().FullName} 注册被拒绝（不覆盖既有模板）");
                return false;
            }

            RegisteredAbilities[ability.Id] = ability;
            if (!AbilitiesByType.ContainsKey(ability.GetType()))
                AbilitiesByType[ability.GetType()] = ability;

            // 模板实例同时承担该类技能的事件监听（事件回调内按玩家查实例，不在模板上存状态）。
            EventManager.RegisterInstance(ability, ability.GetType());
            return true;
        }

        /// <summary>按 Id 注销技能模板；玩家已获得的技能实例不受影响。</summary>
        /// <param name="id">技能 Id。</param>
        /// <returns>确实注销时返回 <see langword="true"/>。</returns>
        internal static bool Unregister(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || !RegisteredAbilities.TryGetValue(id, out var ability))
                return false;

            RegisteredAbilities.Remove(id);

            var type = ability.GetType();
            if (AbilitiesByType.TryGetValue(type, out var mapped) && ReferenceEquals(mapped, ability))
            {
                AbilitiesByType.Remove(type);
                var replacement = RegisteredAbilities.Values.FirstOrDefault(candidate => candidate.GetType() == type);
                if (replacement is not null)
                    AbilitiesByType[type] = replacement;
            }

            EventManager.UnregisterInstance(ability);
            return true;
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

        /// <summary>清空模板注册表（框架卸载时调用）；模板的事件注销与注销回调由 <c>EventManager</c> 负责。</summary>
        internal static void Clear()
        {
            RegisteredAbilities.Clear();
            AbilitiesByType.Clear();
        }
    }

