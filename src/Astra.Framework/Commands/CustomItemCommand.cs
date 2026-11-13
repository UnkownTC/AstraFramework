using System;
using System.Collections.Generic;
using System.Linq;

using Astra.Items;

using CommandSystem;

using LabApi.Features.Wrappers;

namespace Astra.Commands;

/// <summary>自定义物品（<see cref="CustomItem"/>）的 RA 指令。</summary>
[CommandHandler(typeof(RemoteAdminCommandHandler))]
public sealed class CustomItemCommand : ICommand
{
    private const string UsageText =
        "用法：\n" +
        "  customitem list — 列出全部物品定义\n" +
        "  customitem info <Id> — 查看指定定义\n" +
        "  customitem get <玩家> — 查看玩家持有的自定义物品\n" +
        "  customitem give <玩家> <Id> — 给玩家发放物品\n" +
        "玩家参数可用 SteamId / PlayerId（不能含空格）";

    /// <inheritdoc />
    public string Command => "customitem";

    /// <inheritdoc />
    public string[] Aliases => new[] { "citem", "ci" };

    /// <inheritdoc />
    public string Description => "Astra 自定义物品管理";

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

        var items = CustomItemApi.Items;
        if (items.Count == 0)
        {
            response = "暂无已注册的物品定义。";
            return true;
        }

        var lines = new List<string> { $"物品定义（{items.Count}）：" };
        lines.AddRange(items.Values.Select(item => $"- {item.Name} ({item.Id})"));
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
            CustomItemApi.Items.Values, args[1], item => item.Id);
        if (template is null)
        {
            response = CommandSupport.NoMatch(args[1]);
            return false;
        }

        response = $"物品：{template.Name} (Id: {template.Id})，实现类型：{template.GetType().Name}，" +
                   $"承载物品：{template.BaseItem}";
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

        var owned = player.Items
            .Select(item => CustomItemManager.FindByItem(item) ?? CustomItemManager.FindByItemType(item.Type))
            .Where(item => item is not null)
            .Select(item => item!)
            .GroupBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

        if (owned.Count == 0)
        {
            response = $"玩家 {CommandSupport.FormatPlayer(player)} 当前没有自定义物品。";
            return true;
        }

        var lines = new List<string> { $"玩家 {CommandSupport.FormatPlayer(player)} 持有自定义物品（{owned.Count}）：" };
        lines.AddRange(owned.Select(item => $"- {item.Name} ({item.Id})"));
        response = string.Join("\n", lines);
        return true;
    }

    private static bool Give(string[] args, ICommandSender sender, out string response)
    {
        if (!CommandSupport.HasPermission(sender, PlayerPermissions.GivingItems))
        {
            response = CommandSupport.MissingPermission(PlayerPermissions.GivingItems);
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
            CustomItemApi.Items.Values, args[2], item => item.Id);
        if (template is null)
        {
            response = CommandSupport.NoMatch(args[2]);
            return false;
        }

        var item = CustomItemApi.Give(player, template.Id);
        if (item is null)
        {
            response = $"执行失败：无法给玩家 {CommandSupport.FormatPlayer(player)} 发放物品 {template.Name} ({template.Id})";
            return false;
        }

        response = $"物品 {template.Name} 已被添加至玩家 {CommandSupport.FormatPlayer(player)}";
        return true;
    }
}
