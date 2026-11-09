using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Astra.Abstractions;
using Astra.Systems.EventSystem;

using LabApi.Features.Console;

namespace Astra.Systems.CustomRoleSystem;

internal sealed class CustomRoleTemplateManager
{
    private static readonly Dictionary<string, CustomRole> RegisteredRoles = new();
    private static readonly Dictionary<Type, CustomRole> RolesByType = new();
    internal static IReadOnlyCollection<CustomRole> RoleTemplates => RegisteredRoles.Values;
    internal static void RegisterRole(Type type)
    {
        if (RolesByType.TryGetValue(type, out var existedRoleTemplate))
        {
            Logger.Warn($"[CustomRoleTemplateManager] 发现冲突Id {existedRoleTemplate.Id}");
            Logger.Warn($"[CustomRoleTemplateManager] {type.FullName} 与 {existedRoleTemplate.GetType().FullName} 发生冲突");
            return;
        }
        if (Activator.CreateInstance(type) is not Abstractions.CustomRole role)
            return;

        if (RegisteredRoles.TryGetValue(role.Id, out var occupiedById) &&
            occupiedById.GetType() != type)
        {
            Logger.Warn($"[CustomRoleTemplateManager] 角色 Id {role.Id} 已被 {occupiedById.GetType().FullName} 占用，" +
                        $"{type.FullName} 注册被拒绝（不覆盖既有模板）");
            return;
        }

        RegisteredRoles[role.Id] = role;
        RolesByType[type] = role;

        EventManager.RegisterInstance(role, type);
    }

    internal static bool TryGetRoleTemplate(string id, out CustomRole role)
    {
        return RegisteredRoles.TryGetValue(id, out role);
    }

    internal static bool TryGetRoleTemplate(Type type, out CustomRole role)
    {
        return RolesByType.TryGetValue(type, out role);
    }

    internal static bool TryGetRoleTemplate<T>(out T role) where T : CustomRole
    {
        var flag = TryGetRoleTemplate(typeof(T), out var role1);
        role = role1 as T;
        return flag && role is not null;
    }
}
