using System;
using System.Collections.Generic;
using System.Linq;

using Astra.Teams;

using CommandSystem;

namespace Astra.Commands;

/// <summary>自定义阵营（<see cref="CustomTeam"/>）的 RA 指令；阵营不归属玩家，因此不提供 get / give。</summary>
[CommandHandler(typeof(RemoteAdminCommandHandler))]
public sealed class CustomTeamCommand : ICommand
{
    private const string UsageText =
        "用法：\n" +
        "  customteam list — 列出当前回合的全部阵营\n" +
        "  customteam info <Id|Name> — 查看指定阵营（优先按 Id 匹配）";

    /// <inheritdoc />
    public string Command => "customteam";

    /// <inheritdoc />
    public string[] Aliases => new[] { "cteam" };

    /// <inheritdoc />
    public string Description => "Astra 自定义阵营查询";

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

        var teams = CustomTeamApi.Teams;
        if (teams.Count == 0)
        {
            response = "当前回合没有自定义阵营。";
            return true;
        }

        var lines = new List<string> { $"自定义阵营（{teams.Count}）：" };
        lines.AddRange(teams.Values.Select(team => $"- {team.Name} ({team.Id})"));
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

        var team = CommandSupport.MatchTemplate(
            CustomTeamApi.Teams.Values, args[1], entry => entry.Id, entry => entry.Name);
        if (team is null)
        {
            response = CommandSupport.NoMatch(args[1]);
            return false;
        }

        response = $"阵营：{team.Name} (Id: {team.Id})，实现类型：{team.GetType().Name}，" +
                   $"原版阵营：{team.VanillaTeam}，原版派系：{team.VanillaFaction}，关系模式：{team.Relation}";
        return true;
    }
}
