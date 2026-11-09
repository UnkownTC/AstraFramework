using System;
using System.Collections.Generic;
using System.Linq;

using Astra.Attributes;
using Astra.HumeShield;

using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.CustomHandlers;
using LabApi.Features.Wrappers;

using PlayerRoles;

using Logger = LabApi.Features.Console.Logger;

namespace Astra.Roles;

[ManagedEvent("Astra - CustomRoleManager")]
internal class CustomRoleManager : CustomEventsHandler
{
    private static Dictionary<uint, CustomRole> PlayerRoles { get; } = new();

    public static CustomRole? AddRole<T>(Player player) where T : CustomRole, new()
    {
        if (!CustomRoleTemplateManager.TryGetRoleTemplate<T>(out T roleTemplate))
            return null;
        return AddRole(player, roleTemplate);
    }

    public static CustomRole? AddRole(Player player, string id)
    {
        if (!CustomRoleTemplateManager.TryGetRoleTemplate(id, out var role))
            return null;
        return AddRole(player, role);
    }

    public static CustomRole? GetCustomRole(Player player) =>
        TryGetCustomRole(player, out CustomRole role) ? role : null;

    public static bool TryGetCustomRole(Player player, out CustomRole role)
    {
        role = null;
        if (player is null)
            return false;
        return PlayerRoles.TryGetValue(player.NetworkId, out role);
    }

    private static CustomRole AddRole(Player player, CustomRole roleTemplate)
    {
        if (player is null || roleTemplate is null)
            return null;

        RemoveRole(player);

        var role = roleTemplate.CreateInstance();
        role.AddRole(player);
        if (role.IsDestroyed)
        {
            // AddRole 内部已回滚（例如 SetRole 抛异常），不要登记到玩家表。
            Logger.Error($"[CustomRoleManager] 角色 {role.Id} 套用到玩家 {player.PlayerId} 失败，已放弃登记。");
            return null;
        }

        if (role is ICustomHumeShieldStat humeShieldStat)
            CustomHumeShieldManager.AddHumeShield(player, humeShieldStat);

        PlayerRoles[player.NetworkId] = role;
        return role;
    }

    public static void RemoveRole(Player player)
    {
        if (player is null)
            return;

        if (TryGetCustomRole(player, out CustomRole role))
            role.RemoveRole(player);

        PlayerRoles.Remove(player.NetworkId);
    }

    /// <summary>
    /// 原版角色变更后收口自定义角色。
    /// </summary>
    /// <remarks>
    /// 自定义角色实例是按玩家创建的，不会被自动注册为事件处理器，
    /// 因此 <see cref="CustomRole.OnPlayerChangingRole"/> 在玩家实例上永远收不到回调，
    /// 清理必须由已完成注册的本管理器承担。
    /// </remarks>
    public override void OnPlayerChangedRole(PlayerChangedRoleEventArgs ev)
    {
        if (ev?.Player is null)
            return;

        if (!TryGetCustomRole(ev.Player, out CustomRole role))
            return;

        // 初始化过程中的角色变更由 AddRole 自己处理。
        if (role.IsApplying)
            return;

        // 玩家仍停留在该自定义角色对应的原版角色上时保留。
        if (!role.IsDestroyed && role.RoleType is not RoleTypeId.None &&
            ev.Player.Role == role.RoleType)
            return;

        RemoveRole(ev.Player);
    }

    /// <summary>玩家离开时释放条目，避免离线玩家的 NetworkId 长期占用并污染查询。</summary>
    public override void OnPlayerLeft(PlayerLeftEventArgs ev)
    {
        if (ev?.Player is null)
            return;

        RemoveRole(ev.Player);
    }

    public override void OnServerRoundRestarted()
    {
        // 已离线玩家的 Player.Get 会返回 null，必须逐条容错，
        // 否则单条异常会中断整个循环，导致后续玩家都清理不到。
        foreach (var kvp in PlayerRoles.ToArray())
        {
            try
            {
                var player = Player.Get(kvp.Key);
                kvp.Value.RemoveRole(player);
            }
            catch (Exception ex)
            {
                Logger.Error($"[CustomRoleManager] 回合重启清理角色 {kvp.Value.Id} 失败：{ex.Message}");
            }
        }

        PlayerRoles.Clear();
    }
}
