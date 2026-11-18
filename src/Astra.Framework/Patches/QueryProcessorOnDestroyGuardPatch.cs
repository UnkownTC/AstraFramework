using System.Reflection;

using Astra.Attributes;

using CommandSystem;

using HarmonyLib;

using RemoteAdmin;

namespace Astra.Patches;

/// <summary>
/// 原版 <see cref="QueryProcessor.OnDestroy"/> 的清理条件把
/// <c>string.IsNullOrEmpty(_sender.OutputId)</c> 写反：OutputId 为 null 时会进入分支，
/// 随后 <c>ServerConsole.ConsoleOutputs.TryRemove(null, ...)</c> 抛
/// <see cref="System.ArgumentNullException"/>，干扰玩家断线流程。
/// </summary>
/// <remarks>
/// 只在 OutputId 为 null 时跳过整个 OnDestroy；空字符串仍交给原版（空键不会触发该异常）。
/// 跳过时仅损失一次 RA 断线日志，连接事件仍会记录断线。
/// </remarks>
[ManagedPatch]
[HarmonyPatch(typeof(QueryProcessor), "OnDestroy")]
internal static class QueryProcessorOnDestroyGuardPatch
{
    private static readonly FieldInfo? SenderField = AccessTools.Field(typeof(QueryProcessor), "_sender");

    /// <summary>在 OutputId 为 null 时阻止原版以 null 调用 TryRemove。</summary>
    /// <param name="__instance">正在销毁的查询处理器。</param>
    /// <returns>允许原方法执行时返回 <see langword="true"/>。</returns>
    [HarmonyPrefix]
    private static bool Prefix(QueryProcessor __instance)
    {
        if (__instance == null)
            return false;

        if (SenderField is null)
            return true;

        var sender = SenderField.GetValue(__instance) as CommandSender;

        return sender is null || sender.OutputId != null;
    }
}
