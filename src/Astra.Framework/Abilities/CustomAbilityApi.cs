using System;
using System.Collections.Generic;

using Astra.Lifecycle;

using LabApi.Features.Wrappers;
// ReSharper disable MemberCanBePrivate.Global

namespace Astra.Abilities;
    /// <summary>
    /// 自定义技能对外静态 API，提供技能模板与玩家技能实例的查询能力。
    /// </summary>
    public static class CustomAbilityApi
    {
        /// <summary>获取当前已注册的全部技能模板。</summary>
        public static IReadOnlyCollection<CustomAbility> AbilityTemplates => CustomAbilityTemplateManager.AbilityTemplates;

        /// <summary>按实例注册技能模板（外部框架 / JSON 适配器用）。</summary>
        /// <param name="ability">技能模板实例。</param>
        /// <returns>登记成功（或同实例重复登记）返回 <see langword="true"/>；Id 冲突返回 <see langword="false"/>。</returns>
        /// <remarks>玩家已获得的技能实例不会因模板注销被移除。</remarks>
        public static bool Register(CustomAbility ability)
        {
            if (ability is null || !CustomAbilityTemplateManager.RegisterInstance(ability))
                return false;

            RegistrationTracking.Ability(ability, RegistrationSource.FromType(ability.GetType()));
            return true;
        }

        /// <summary>在指定注册作用域内按实例注册技能模板。</summary>
        /// <param name="ability">技能模板实例。</param>
        /// <param name="scope">注册作用域；释放时按作用域来源撤销该技能。</param>
        /// <returns>登记成功（或同实例重复登记）返回 <see langword="true"/>；Id 冲突返回 <see langword="false"/>。</returns>
        public static bool Register(CustomAbility ability, RegistrationScope scope)
        {
            if (ability is null)
                return false;
            if (scope is null)
                throw new ArgumentNullException(nameof(scope));

            if (!CustomAbilityTemplateManager.RegisterInstance(ability))
                return false;

            RegistrationTracking.Ability(ability, scope.Source);
            return true;
        }

        /// <summary>按 Id 注销技能模板。</summary>
        /// <param name="id">技能 Id。</param>
        /// <returns>确实注销时返回 <see langword="true"/>。</returns>
        public static bool Unregister(string id) => CustomAbilityTemplateManager.Unregister(id);

        /// <summary>按 Id 获取全局技能模板。</summary>
        public static bool TryGetAbilityTemplate(string id, out CustomAbility ability)
        {
            return CustomAbilityTemplateManager.TryGetAbilityTemplate(id, out ability);
        }

        /// <summary>按类型获取全局技能模板。</summary>
        public static bool TryGetAbilityTemplate(Type type, out CustomAbility ability)
        {
            return CustomAbilityTemplateManager.TryGetAbilityTemplate(type, out ability);
        }

        /// <summary>按泛型类型获取全局技能模板。</summary>
        public static bool TryGetAbilityTemplate<T>(out T ability) where T : CustomAbility, new()
        {
            return CustomAbilityTemplateManager.TryGetAbilityTemplate(out ability);
        }

        /// <summary>获取指定玩家的技能管理器。</summary>
        public static bool TryGetAbilityManager(Player player, out CustomAbilityManager manager)
        {
            return CustomAbilityManager.TryGetAbilityManager(player, out manager);
        }

        /// <summary>按泛型类型获取指定玩家的技能实例。</summary>
        public static bool TryGetPlayerAbility<T>(Player player, out T ability) where T : CustomAbility, new()
        {
            ability = null;
            return TryGetAbilityManager(player, out var manager) && manager.TryGetAbility(out ability);
        }

        /// <summary>按 Id 获取指定玩家的技能实例。</summary>
        public static bool TryGetPlayerAbility(Player player, string id, out CustomAbility ability)
        {
            ability = null;
            return TryGetAbilityManager(player, out var manager) && manager.TryGetAbility(id, out ability);
        }

        /// <summary>按 SettingId 获取指定玩家的技能实例。</summary>
        public static bool TryGetPlayerAbility(Player player, int settingId, out CustomAbility ability)
        {
            ability = null;
            return TryGetAbilityManager(player, out var manager) && manager.TryGetAbility(settingId, out ability);
        }
    }

