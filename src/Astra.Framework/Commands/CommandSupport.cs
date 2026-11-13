using System;
using System.Collections.Generic;
using System.Linq;

using CommandSystem;

using LabApi.Features.Console;
using LabApi.Features.Wrappers;

namespace Astra.Commands;

/// <summary>RA 指令公共辅助：权限判定、玩家匹配、文本格式与异常记录。</summary>
internal static class CommandSupport
{
    /// <summary>判断发送者是否持有指定原版权限（全权限发送者直接放行）。</summary>
    /// <param name="sender">指令发送者。</param>
    /// <param name="permission">所需权限。</param>
    /// <returns>持有权限时返回 <see langword="true"/>。</returns>
    public static bool HasPermission(ICommandSender sender, PlayerPermissions permission)
    {
        return sender is CommandSender commandSender &&
               (commandSender.FullPermissions ||
                PermissionsHandler.IsPermitted(commandSender.Permissions, permission));
    }

    /// <summary>权限不足时的统一反馈。</summary>
    /// <param name="permission">缺少的权限。</param>
    /// <returns>反馈文本。</returns>
    public static string MissingPermission(PlayerPermissions permission) =>
        $"缺少权限：PlayerPermissions.{permission}";

    /// <summary>玩家匹配失败时的统一反馈（回显使用者输入）。</summary>
    /// <param name="input">使用者尝试匹配的内容。</param>
    /// <returns>反馈文本。</returns>
    public static string NoMatch(string input) =>
        $"无法匹配：{input}（可用 SteamId / PlayerId / Nickname；SteamId 与 Nickname 需全字匹配，忽略大小写）";

    /// <summary>按 SteamId → PlayerId → Nickname 的顺序匹配在线玩家。</summary>
    /// <param name="input">SteamId、PlayerId 或 Nickname。</param>
    /// <returns>匹配到的玩家；无法匹配时为 <see langword="null"/>。</returns>
    public static Player? MatchPlayer(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        var players = Player.List;

        var byUserId = players.FirstOrDefault(player =>
            string.Equals(player.UserId, input, StringComparison.OrdinalIgnoreCase));
        if (byUserId is not null)
            return byUserId;

        if (int.TryParse(input, out var playerId))
        {
            var byPlayerId = players.FirstOrDefault(player => player.PlayerId == playerId);
            if (byPlayerId is not null)
                return byPlayerId;
        }

        return players.FirstOrDefault(player =>
            string.Equals(player.Nickname, input, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>按 Id → Name 的顺序匹配模板（均忽略大小写）。</summary>
    /// <typeparam name="T">模板类型。</typeparam>
    /// <param name="templates">候选模板。</param>
    /// <param name="key">Id 或 Name。</param>
    /// <param name="idSelector">取 Id 的委托。</param>
    /// <param name="nameSelector">取 Name 的委托。</param>
    /// <returns>匹配到的模板；未匹配时为 <see langword="null"/>。</returns>
    public static T? MatchTemplate<T>(IEnumerable<T> templates, string key, Func<T, string> idSelector,
        Func<T, string> nameSelector) where T : class
    {
        var byId = templates.FirstOrDefault(template =>
            string.Equals(idSelector(template), key, StringComparison.OrdinalIgnoreCase));
        return byId ?? templates.FirstOrDefault(template =>
            string.Equals(nameSelector(template), key, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>玩家显示格式：昵称 (SteamId) (PlayerId)。</summary>
    /// <param name="player">玩家。</param>
    /// <returns>显示文本。</returns>
    public static string FormatPlayer(Player player) =>
        $"{player.Nickname} ({player.UserId}) ({player.PlayerId})";

    /// <summary>记录异常堆栈到 LabApi 日志，并生成不含堆栈的失败反馈。</summary>
    /// <param name="action">失败的动作描述。</param>
    /// <param name="exception">异常。</param>
    /// <returns>反馈文本。</returns>
    public static string Failure(string action, Exception exception)
    {
        Logger.Error($"[Astra.Commands] {action} 执行失败：{exception}");
        return $"执行失败：{action}（{exception.GetType().Name}: {exception.Message}）";
    }
}
