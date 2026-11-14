using System;
using System.Reflection;

using Astra.Abilities;
using Astra.Events;
using Astra.Items;
using Astra.Patching;
using Astra.Roles;
using Astra.ServerSpecificSettings;
using Astra.Teams;
using Astra.Waves;

namespace Astra.Lifecycle;

/// <summary>
/// 为各管理器的注册动作提供统一的「带回溯校验的撤销委托」。
/// </summary>
/// <remarks>
/// 撤销委托先确认注册表里仍是当初登记的实例，再执行注销；这样即使同 Id 已被
/// 新来源重新注册，旧作用域也不会误删新实例。
/// </remarks>
internal static class RegistrationTracking
{
    internal static void Role(CustomRole role, RegistrationSource source)
    {
        if (role is null)
            return;

        AstraLifecycle.Track(source, $"role:{role.Id}", () =>
        {
            if (CustomRoleApi.TryGetRoleTemplate(role.Id, out var current) && ReferenceEquals(current, role))
                CustomRoleApi.Unregister(role.Id);
        });
    }

    internal static void Team(CustomTeam team, RegistrationSource source)
    {
        if (team is null)
            return;

        AstraLifecycle.Track(source, $"team:{team.Id}", () =>
        {
            if (ReferenceEquals(CustomTeamApi.Find(team.Id), team))
                CustomTeamApi.Unregister(team.Id);
        });
    }

    internal static void Wave(CustomWave wave, RegistrationSource source)
    {
        if (wave is null)
            return;

        AstraLifecycle.Track(source, $"wave:{wave.Id}", () =>
        {
            if (ReferenceEquals(CustomWaveApi.Find(wave.Id), wave))
                CustomWaveApi.Unregister(wave.Id);
        });
    }

    internal static void Item(CustomItem item, RegistrationSource source)
    {
        if (item is null)
            return;

        AstraLifecycle.Track(source, $"item:{item.Id}", () =>
        {
            if (ReferenceEquals(CustomItemApi.Find(item.Id), item))
                CustomItemApi.Unregister(item.Id);
        });
    }

    internal static void Ability(CustomAbility ability, RegistrationSource source)
    {
        if (ability is null)
            return;

        AstraLifecycle.Track(source, $"ability:{ability.Id}", () =>
        {
            if (CustomAbilityApi.TryGetAbilityTemplate(ability.Id, out var current) &&
                ReferenceEquals(current, ability))
                CustomAbilityApi.Unregister(ability.Id);
        });
    }

    internal static void EventHandler(Type type, RegistrationSource source)
    {
        if (type is null)
            return;

        AstraLifecycle.Track(source, $"event:{type.FullName}", () => EventManager.UnregisterType(type));
    }

    internal static void Setting(Type type, RegistrationSource source)
    {
        if (type is null)
            return;

        AstraLifecycle.Track(source, $"setting:{type.FullName}", () => ServerSpecificSettingManager.Unregister(type));
    }

    internal static void PatchType(Type type, RegistrationSource source)
    {
        if (type is null)
            return;

        AstraLifecycle.Track(source, $"patch:{type.FullName}", () => PatchManager.UnpatchType(type));
    }

    internal static void PatchMethod(MethodInfo method, RegistrationSource source)
    {
        if (method is null)
            return;

        AstraLifecycle.Track(source, $"patch-method:{method.DeclaringType?.FullName}.{method.Name}",
            () => PatchManager.UnpatchMethod(method));
    }

    internal static void EventMethod(MethodInfo method, RegistrationSource source)
    {
        if (method is null)
            return;

        AstraLifecycle.Track(source, $"event-method:{method.DeclaringType?.FullName}.{method.Name}",
            () => EventMethodManager.Unregister(method));
    }
}
