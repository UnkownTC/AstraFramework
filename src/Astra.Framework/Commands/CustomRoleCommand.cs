using System;
using System.Collections.Generic;
using System.Linq;

using Astra.Roles;

using CommandSystem;

namespace Astra.Commands;

/// <summary>自定义角色（<see cref="CustomRole"/>）的 RA 指令。</summary>
[CommandHandler(typeof(RemoteAdminCommandHandler))]
public sealed class CustomRoleCommand : ICommand
{
    private const string UsageText =
        "用法：\n" +
        "  customrole list — 列出全部角色模板\n" +
        "  customrole info <Id> — 查看指定模板\n" +
        "  customrole get <玩家> — 查看玩家当前角色\n" +
        "  customrole give <玩家> <Id> — 给玩家套用角色\n" +
        "玩家参数可用 SteamId / PlayerId（不能含空格）";

    /// <inheritdoc />
    public string Command => "customrole";

    /// <inheritdoc />
    public string[] Aliases => new[] { "crole", "cr" };

    /// <inheritdoc />
    public string Description => "Astra 自定义角色管理";

    /// <inheritdoc />
    public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
    {
        try
        {
            var args = arguments.ToArray();

            if (args.Length == 0)
            {
                response = UsageText;
                return false;
            }

            switch (args[0].ToLowerInvariant())
            {
                case "list":
                    return List(args, sender, out response);
                case "info":
                    return Info(args, sender, out response);
                case "get":
                    return Get(args, sender, out response);
                case "give":
                    return Give(args, sender, out response);
                default:
                    response = UsageText;
                    return false;
            }
        }
        catch (Exception exception)
        {
            response = CommandSupport.Failure(Command, exception);
            return false;
        }
    }

    private static bool List(string[] args, ICommandSender sender, out string response)
    {
        if (!CommandSupport.HasPermission(sender, PlayerPermissions.GameplayData))
        {
            response = CommandSupport.MissingPermission(PlayerPermissions.GameplayData);
            return false;
        }

        var templates = CustomRoleApi.RoleTemplates;
        if (templates.Count == 0)
        {
            response = "暂无已注册的角色模板。";
            return true;
        }

        var lines = new List<string> { $"角色模板（{templates.Count}）：" };
        lines.AddRange(templates.Select(template => $"- {template.Name} ({template.Id})"));
        response = string.Join("\n", lines);
        return true;
    }

    private static bool Info(string[] args, ICommandSender sender, out string response)
    {
        if (!CommandSupport.HasPermission(sender, PlayerPermissions.GameplayData))
        {
            response = CommandSupport.MissingPermission(PlayerPermissions.GameplayData);
            return false;
        }

        if (args.Length < 2)
        {
            response = UsageText;
            return false;
        }

        var template = CommandSupport.MatchById(
            CustomRoleApi.RoleTemplates, args[1], role => role.Id);
        if (template is null)
        {
            response = CommandSupport.NoMatch(args[1]);
            return false;
        }

        response = $"角色：{template.Name} (Id: {template.Id})，实现类型：{template.GetType().Name}，" +
                   $"原版角色：{template.RoleType}";
        return true;
    }

    private static bool Get(string[] args, ICommandSender sender, out string response)
    {
        if (!CommandSupport.HasPermission(sender, PlayerPermissions.PlayerSensitiveDataAccess))
        {
            response = CommandSupport.MissingPermission(PlayerPermissions.PlayerSensitiveDataAccess);
            return false;
        }

        if (args.Length < 2)
        {
            response = UsageText;
            return false;
        }

        var player = CommandSupport.MatchPlayer(args[1]);
        if (player is null)
        {
            response = CommandSupport.NoMatch(args[1]);
            return false;
        }

        var role = CustomRoleApi.GetRole(player);
        response = role is null
            ? $"玩家 {CommandSupport.FormatPlayer(player)} 当前没有自定义角色。"
            : $"玩家 {CommandSupport.FormatPlayer(player)} 当前角色：{role.Name} ({role.Id})";
        return true;
    }

    private static bool Give(string[] args, ICommandSender sender, out string response)
    {
        if (!CommandSupport.HasPermission(sender, PlayerPermissions.ForceclassWithoutRestrictions))
        {
            response = CommandSupport.MissingPermission(PlayerPermissions.ForceclassWithoutRestrictions);
            return false;
        }

        if (args.Length < 3)
        {
            response = UsageText;
            return false;
        }

        var player = CommandSupport.MatchPlayer(args[1]);
        if (player is null)
        {
            response = CommandSupport.NoMatch(args[1]);
            return false;
        }

        var template = CommandSupport.MatchById(
            CustomRoleApi.RoleTemplates, args[2], role => role.Id);
        if (template is null)
        {
            response = CommandSupport.NoMatch(args[2]);
            return false;
        }

        var applied = CustomRoleApi.AddRole(player, template.Id);
        if (applied is null)
        {
            response = $"执行失败：无法给玩家 {CommandSupport.FormatPlayer(player)} 套用角色 {template.Name} ({template.Id})";
            return false;
        }

        response = $"角色 {template.Name} 已被添加至玩家 {CommandSupport.FormatPlayer(player)}";
        return true;
    }
}
