using System;
using System.Collections.Generic;

using Astra.Abstractions;
using Astra.Systems.CustomAbilitySystem;

using LabApi.Features.Wrappers;
// ReSharper disable MemberCanBePrivate.Global

namespace Astra.Apis
{
    /// <summary>
    /// 自定义技能对外静态 API，提供技能模板与玩家技能实例的查询能力。
    /// </summary>
    public static class CustomAbilityApi
    {
        /// <summary>获取当前已注册的全部技能模板。</summary>
        public static IReadOnlyCollection<CustomAbility> AbilityTemplates => CustomAbilityTemplateManager.AbilityTemplates;

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
}
