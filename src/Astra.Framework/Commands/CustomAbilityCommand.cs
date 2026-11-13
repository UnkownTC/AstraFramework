using System;
using System.Collections.Generic;
using System.Linq;

using Astra.Abilities;

using CommandSystem;

namespace Astra.Commands;

/// <summary>自定义技能（<see cref="CustomAbility"/>）的 RA 指令。</summary>
[CommandHandler(typeof(RemoteAdminCommandHandler))]
public sealed class CustomAbilityCommand : ICommand
{
    private const string UsageText =
        "用法：\n" +
        "  customability list — 列出全部技能模板\n" +
        "  customability info <Id|Name> — 查看指定模板（优先按 Id 匹配）\n" +
        "  customability get <玩家> — 查看玩家拥有的技能\n" +
        "  customability give <玩家> <Id|Name> — 给玩家授予技能\n" +
        "玩家参数可用 SteamId / PlayerId / Nickname（SteamId 与 Nickname 全字匹配、忽略大小写）";

    /// <inheritdoc />
    public string Command => "customability";

    /// <inheritdoc />
    public string[] Aliases => new[] { "cability" };

    /// <inheritdoc />
    public string Description => "Astra 自定义技能管理";

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

        var templates = CustomAbilityApi.AbilityTemplates;
        if (templates.Count == 0)
        {
            response = "暂无已注册的技能模板。";
            return true;
        }

        var lines = new List<string> { $"技能模板（{templates.Count}）：" };
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

        var template = CommandSupport.MatchTemplate(
            CustomAbilityApi.AbilityTemplates, args[1], ability => ability.Id, ability => ability.Name);
        if (template is null)
        {
            response = CommandSupport.NoMatch(args[1]);
            return false;
        }

        response = $"技能：{template.Name} (Id: {template.Id})，实现类型：{template.GetType().Name}";
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

        if (!CustomAbilityApi.TryGetAbilityManager(player, out var manager))
        {
            response = $"玩家 {CommandSupport.FormatPlayer(player)} 没有技能管理器。";
            return false;
        }

        var abilities = manager.Abilities;
        if (abilities.Count == 0)
        {
            response = $"玩家 {CommandSupport.FormatPlayer(player)} 当前没有技能。";
            return true;
        }

        var lines = new List<string> { $"玩家 {CommandSupport.FormatPlayer(player)} 拥有技能（{abilities.Count}）：" };
        lines.AddRange(abilities.Select(ability => $"- {ability.Name} ({ability.Id})"));
        response = string.Join("\n", lines);
        return true;
    }

    private static bool Give(string[] args, ICommandSender sender, out string response)
    {
        if (!CommandSupport.HasPermission(sender, PlayerPermissions.Effects))
        {
            response = CommandSupport.MissingPermission(PlayerPermissions.Effects);
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

        var template = CommandSupport.MatchTemplate(
            CustomAbilityApi.AbilityTemplates, args[2], ability => ability.Id, ability => ability.Name);
        if (template is null)
        {
            response = CommandSupport.NoMatch(args[2]);
            return false;
        }

        if (!CustomAbilityApi.TryGetAbilityManager(player, out var manager))
        {
            response = $"执行失败：玩家 {CommandSupport.FormatPlayer(player)} 没有技能管理器";
            return false;
        }

        var added = manager.AddAbility(template.Id);
        if (added is null)
        {
            response = $"执行失败：无法给玩家 {CommandSupport.FormatPlayer(player)} 授予技能 {template.Name} ({template.Id})";
            return false;
        }

        response = $"技能 {template.Name} 已被添加至玩家 {CommandSupport.FormatPlayer(player)}";
        return true;
    }
}
