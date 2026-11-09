using System.Collections.Generic;

using LabApi.Features.Wrappers;

namespace Astra.Roles;

/// <summary>自定义角色对外 API：模板查询与玩家角色增删。</summary>
public static class CustomRoleApi
{
    /// <summary>当前已注册的全部角色模板。</summary>
    public static IReadOnlyCollection<CustomRole> RoleTemplates => CustomRoleTemplateManager.RoleTemplates;

    /// <summary>按 Id 获取角色模板。</summary>
    /// <param name="id">角色 Id。</param>
    /// <param name="role">取到的模板。</param>
    /// <returns>存在时返回 <see langword="true"/>。</returns>
    public static bool TryGetRoleTemplate(string id, out CustomRole role) =>
        CustomRoleTemplateManager.TryGetRoleTemplate(id, out role);

    /// <summary>按类型获取角色模板。</summary>
    /// <typeparam name="T">角色类型。</typeparam>
    /// <param name="role">取到的模板。</param>
    /// <returns>存在时返回 <see langword="true"/>。</returns>
    public static bool TryGetRoleTemplate<T>(out T role) where T : CustomRole, new() =>
        CustomRoleTemplateManager.TryGetRoleTemplate(out role);

    /// <summary>按泛型类型给玩家套用角色。</summary>
    /// <typeparam name="T">角色类型。</typeparam>
    /// <param name="player">目标玩家。</param>
    /// <returns>套用后的角色实例；失败为 <see langword="null"/>。</returns>
    public static CustomRole? AddRole<T>(Player player) where T : CustomRole, new() =>
        CustomRoleManager.AddRole<T>(player);

    /// <summary>按 Id 给玩家套用角色。</summary>
    /// <param name="player">目标玩家。</param>
    /// <param name="id">角色 Id。</param>
    /// <returns>套用后的角色实例；失败为 <see langword="null"/>。</returns>
    public static CustomRole? AddRole(Player player, string id) => CustomRoleManager.AddRole(player, id);

    /// <summary>取玩家当前的自定义角色。</summary>
    /// <param name="player">目标玩家。</param>
    /// <returns>角色实例；没有则为 <see langword="null"/>。</returns>
    public static CustomRole? GetRole(Player player) => CustomRoleManager.GetCustomRole(player);

    /// <summary>尝试取玩家当前的自定义角色。</summary>
    /// <param name="player">目标玩家。</param>
    /// <param name="role">取到的角色。</param>
    /// <returns>存在时返回 <see langword="true"/>。</returns>
    public static bool TryGetRole(Player player, out CustomRole role) =>
        CustomRoleManager.TryGetCustomRole(player, out role);

    /// <summary>移除玩家当前的自定义角色。</summary>
    /// <param name="player">目标玩家。</param>
    public static void RemoveRole(Player player) => CustomRoleManager.RemoveRole(player);
}
