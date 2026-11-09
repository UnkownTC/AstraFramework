using System.Collections.Generic;

namespace Astra.Patching;

/// <summary>托管补丁对外 API：状态查询与启停。</summary>
public static class PatchApi
{
    /// <summary>全部已登记补丁的状态快照。</summary>
    public static IReadOnlyList<PatchStatus> Patches => PatchManager.Patches;

    /// <summary>按 Id 查询补丁状态。</summary>
    /// <param name="id">补丁 Id（完整类型名或唯一短名）。</param>
    /// <param name="status">补丁状态。</param>
    /// <returns>存在时返回 <see langword="true"/>。</returns>
    public static bool TryGetStatus(string id, out PatchStatus status) =>
        PatchManager.TryGetPatchStatus(id, out status);

    /// <summary>启用补丁。</summary>
    /// <param name="id">补丁 Id。</param>
    /// <returns>状态发生变化时返回 <see langword="true"/>。</returns>
    public static bool Enable(string id) => PatchManager.TryPatch(id);

    /// <summary>禁用补丁。</summary>
    /// <param name="id">补丁 Id。</param>
    /// <returns>状态发生变化时返回 <see langword="true"/>。</returns>
    public static bool Disable(string id) => PatchManager.TryUnpatch(id);

    /// <summary>卸载全部补丁。</summary>
    public static void DisableAll() => PatchManager.UnpatchAll();
}
