using System;
using System.Collections.Generic;
using System.Linq;

using Astra.Waves;

using CommandSystem;

namespace Astra.Commands;

/// <summary>自定义波次（<see cref="CustomWave"/>）的 RA 指令；波次不归属玩家，因此不提供 get / give。</summary>
[CommandHandler(typeof(RemoteAdminCommandHandler))]
public sealed class CustomWaveCommand : ICommand
{
    private const string UsageText =
        "用法：\n" +
        "  customwave list — 列出当前回合的全部波次\n" +
        "  customwave info <Id|Name> — 查看指定波次（优先按 Id 匹配）";

    /// <inheritdoc />
    public string Command => "customwave";

    /// <inheritdoc />
    public string[] Aliases => new[] { "cwave" };

    /// <inheritdoc />
    public string Description => "Astra 自定义波次查询";

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

        var waves = CustomWaveApi.Waves;
        if (waves.Count == 0)
        {
            response = "当前回合没有自定义波次。";
            return true;
        }

        var lines = new List<string> { $"自定义波次（{waves.Count}）：" };
        lines.AddRange(waves.Values.Select(wave => $"- {wave.Name} ({wave.Id})"));
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

        var wave = CommandSupport.MatchTemplate(
            CustomWaveApi.Waves.Values, args[1], entry => entry.Id, entry => entry.Name);
        if (wave is null)
        {
            response = CommandSupport.NoMatch(args[1]);
            return false;
        }

        var teams = wave.Teams.Count == 0
            ? "无"
            : string.Join(", ", wave.Teams.Select(team => $"{team.Name} ({team.Id})"));
        response = $"波次：{wave.Name} (Id: {wave.Id})，实现类型：{wave.GetType().Name}，阵营：{teams}，" +
                   $"已登记角色数：{wave.Roles.Count}";
        return true;
    }
}
