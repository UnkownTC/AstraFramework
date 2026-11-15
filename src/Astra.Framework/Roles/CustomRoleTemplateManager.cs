using System;
using System.Collections.Generic;

using Astra.Waves;

using LabApi.Features.Console;

using EventManager = Astra.Events.EventManager;

namespace Astra.Roles;

internal sealed class CustomRoleTemplateManager
{
    private static readonly Dictionary<string, CustomRole> RegisteredRoles = new();
    private static readonly Dictionary<Type, CustomRole> RolesByType = new();
    internal static IReadOnlyCollection<CustomRole> RoleTemplates => RegisteredRoles.Values;
    internal static void RegisterRole(Type type)
    {
        if (type is null || type.IsAbstract || !typeof(CustomRole).IsAssignableFrom(type))
            return;

        if (RolesByType.ContainsKey(type))
        {
            Logger.Warn($"[CustomRoleTemplateManager] {type.FullName} 已注册，跳过重复注册。");
            return;
        }

        if (Activator.CreateInstance(type) is not CustomRole role)
            return;

        RegisterInstance(role);
    }

    /// <summary>按实例注册角色模板（外部框架 / 动态定义用）。</summary>
    /// <param name="role">角色模板实例。</param>
    /// <returns>注册成功（或同实例重复注册）时返回 <see langword="true"/>；Id 冲突等失败返回 <see langword="false"/>。</returns>
    internal static bool RegisterInstance(CustomRole role)
    {
        if (role is null || string.IsNullOrWhiteSpace(role.Id))
            return false;

        IReadOnlyList<CustomWave>? waves = null;
        if (role is IWaveSpawnRole spawn)
        {
            try
            {
                waves = spawn.Waves;
            }
            catch (Exception ex)
            {
                Logger.Error($"[CustomRoleTemplateManager] 角色 {role.Id} 的 Waves 解析失败，注册被拒绝：{ex.Message}");
                return false;
            }
        }

        if (RegisteredRoles.TryGetValue(role.Id, out var occupied))
        {
            if (ReferenceEquals(occupied, role))
                return true;   // 同实例重复注册：幂等

            Logger.Warn($"[CustomRoleTemplateManager] 角色 Id {role.Id} 已被 {occupied.GetType().FullName} 占用，" +
                        $"{role.GetType().FullName} 注册被拒绝（不覆盖既有模板）");
            return false;
        }

        RegisteredRoles[role.Id] = role;

        // 类型索引只服务「按类型查找」；同一类型可能有多个外部实例，保留首个映射。
        if (!RolesByType.ContainsKey(role.GetType()))
            RolesByType[role.GetType()] = role;

        EventManager.RegisterInstance(role, role.GetType());

        if (waves is not null)
        {
            foreach (var wave in waves)
                wave?.AddRole(role);
        }

        return true;
    }

    /// <summary>按 Id 注销角色模板；玩家身上已套用的实例需调用方自行摘除。</summary>
    /// <param name="id">角色 Id。</param>
    /// <returns>确实注销时返回 <see langword="true"/>。</returns>
    internal static bool Unregister(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || !RegisteredRoles.TryGetValue(id, out var role))
            return false;

        RegisteredRoles.Remove(id);

        if (RolesByType.TryGetValue(role.GetType(), out var mapped) && ReferenceEquals(mapped, role))
            RolesByType.Remove(role.GetType());

        if (role is IWaveSpawnRole spawn)
        {
            try
            {
                foreach (var wave in spawn.Waves)
                    wave?.RemoveRole(role);
            }
            catch (Exception ex)
            {
                Logger.Error($"[CustomRoleTemplateManager] 角色 {id} 注销时摘除波次关联失败：{ex.Message}");
            }
        }

        EventManager.UnregisterInstance(role);
        return true;
    }

    internal static bool TryGetRoleTemplate(string id, out CustomRole role) => RegisteredRoles.TryGetValue(id, out role);

    internal static bool TryGetRoleTemplate(Type type, out CustomRole role) => RolesByType.TryGetValue(type, out role);

    internal static bool TryGetRoleTemplate<T>(out T role) where T : CustomRole
    {
        var flag = TryGetRoleTemplate(typeof(T), out var role1);
        role = role1 as T;
        return flag && role is not null;
    }

    /// <summary>清空模板注册表（框架卸载时调用）；模板的事件注销与注销回调由 <c>EventManager</c> 负责。</summary>
    internal static void Clear()
    {
        RegisteredRoles.Clear();
        RolesByType.Clear();
    }
}
