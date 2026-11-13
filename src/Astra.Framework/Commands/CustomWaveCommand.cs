using System;
using System.Collections.Generic;
using System.Linq;

using Astra.Waves;

using CommandSystem;

using LabApi.Features.Wrappers;

namespace Astra.Commands;

/// <summary>自定义波次（<see cref="CustomWave"/>）的 RA 指令；波次不归属玩家，因此不提供 get / give。</summary>
[CommandHandler(typeof(RemoteAdminCommandHandler))]
public sealed class CustomWaveCommand : ICommand
{
    private const string UsageText =
        "用法：\n" +
        "  customwave list — 列出当前回合的全部波次\n" +
        "  customwave info <Id> — 查看指定波次\n" +
        "  customwave spawn <Id> [玩家] — 立即生成指定波次（不修改波次池；省略玩家时对 ReadyList 生成）";

    /// <inheritdoc />
    public string Command => "customwave";

    /// <inheritdoc />
    public string[] Aliases => new[] { "cwave", "cw" };

    /// <inheritdoc />
    public string Description => "Astra 自定义波次查询与生成";

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
                case "spawn":
                    return Spawn(args, sender, out response);
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

        var wave = CommandSupport.MatchById(
            CustomWaveApi.Waves.Values, args[1], entry => entry.Id);
        if (wave is null)
        {
            response = CommandSupport.NoMatch(args[1]);
            return false;
        }

        var teams = wave.Teams.Count == 0
            ? "无"
            : string.Join(", ", wave.Teams.Select(team => team.CustomTeam is { } custom
                ? $"{custom.Name} ({custom.Id})"
                : team.VanillaTeam.ToString()));
        response = $"波次：{wave.Name} (Id: {wave.Id})，实现类型：{wave.GetType().Name}，阵营：{teams}，" +
                   $"已登记角色数：{wave.Roles.Count}";
        return true;
    }

    private static bool Spawn(string[] args, ICommandSender sender, out string response)
    {
        if (!CommandSupport.HasPermission(sender, PlayerPermissions.RespawnEvents))
        {
            response = CommandSupport.MissingPermission(PlayerPermissions.RespawnEvents);
            return false;
        }

        if (args.Length < 2)
        {
            response = UsageText;
            return false;
        }

        var wave = CommandSupport.MatchById(
            CustomWaveApi.Waves.Values, args[1], entry => entry.Id);
        if (wave is null)
        {
            response = CommandSupport.NoMatch(args[1]);
            return false;
        }

        IReadOnlyList<Player> players;
        if (args.Length >= 3)
        {
            var player = CommandSupport.MatchPlayer(args[2]);
            if (player is null)
            {
                response = CommandSupport.NoMatch(args[2]);
                return false;
            }

            players = new[] { player };
        }
        else
        {
            players = Player.ReadyList.ToList();
        }

        var spawned = CustomWaveApi.Spawn(wave, players);
        if (spawned.Count == 0)
        {
            response = $"波次 {wave.Name} ({wave.Id}) 生成完成，但没有玩家被套用（检查波次表与角色生成器）。";
            return false;
        }

        response = $"波次 {wave.Name} ({wave.Id}) 已生成，实际套用 {spawned.Count} 名玩家。";
        return true;
    }
}
