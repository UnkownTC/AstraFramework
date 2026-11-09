using System;
using System.Collections.Generic;

using Astra.Abstractions;
using Astra.Interfaces;
using Astra.Systems.CustomRoleSystem;

namespace Astra.Systems.CustomWaveSystem;

/// <summary>
/// 自定义波次的模板登记、一次性实例化与双向关联维护。
/// </summary>
/// <remarks>
/// <para>生命周期与 <see cref="CustomTeam"/> 一致：插件启用时 <see cref="Initialize"/> 实例化一次，
/// 跨回合复用，不因回合重启重建。</para>
/// <para>关联方向：</para>
/// <list type="bullet">
/// <item>角色 → 波次：角色实现 <see cref="IWaveSpawnRole.Waves"/>，注册时直接写入波次的 <see cref="CustomWave.Roles"/>。</item>
/// <item>波次 → 阵营：波次声明 <see cref="CustomWave.Teams"/>，实例化时反向写入阵营的 <see cref="CustomTeam.Waves"/>。</item>
/// </list>
/// </remarks>
internal static class CustomWaveManager
{
    private static readonly Dictionary<string, CustomWave> Registry =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly List<Type> Templates = [];

    /// <summary>当前的全部波次实例。</summary>
    internal static IReadOnlyDictionary<string, CustomWave> All => Registry;

    /// <summary>登记波次模板类型。</summary>
    /// <param name="type">波次类型。</param>
    internal static void RegisterTemplate(Type type)
    {
        if (type is null || type.IsAbstract || !typeof(CustomWave).IsAssignableFrom(type))
            return;

        if (!Templates.Contains(type))
            Templates.Add(type);
    }

    /// <summary>
    /// 按模板实例化全部波次（幂等），并建立「波次 → 阵营」关联。
    /// </summary>
    /// <remarks>须在 <see cref="CustomTeam.Initialize"/> 之后调用，波次才能解析到阵营实例。</remarks>
    internal static void Initialize()
    {
        if (Registry.Count > 0)
            return;

        foreach (var type in Templates)
        {
            try
            {
                if (Activator.CreateInstance(type, nonPublic: true) is not CustomWave wave ||
                    string.IsNullOrWhiteSpace(wave.Id))
                {
                    LabApi.Features.Console.Logger.Warn($"[CustomWaveManager] 模板 {type.Name} 实例化失败或缺少 Id，已跳过。");
                    continue;
                }

                if (Registry.TryGetValue(wave.Id, out var existing))
                {
                    LabApi.Features.Console.Logger.Warn(
                        $"[CustomWaveManager] 波次 Id 重复：{wave.Id}（{existing.GetType().Name} 已被 {type.Name} 覆盖）。");
                }

                Registry[wave.Id] = wave;

                foreach (var team in wave.Teams)
                {
                    if (team is null)
                    {
                        LabApi.Features.Console.Logger.Warn(
                            $"[CustomWaveManager] 波次 {wave.Id} 的 Teams 含 null 项，已跳过该关联。");
                        continue;
                    }

                    team.AddWave(wave);
                }
            }
            catch (Exception ex)
            {
                LabApi.Features.Console.Logger.Error($"[CustomWaveManager] 模板 {type.Name} 实例化异常：{ex.Message}");
            }
        }
    }

    /// <summary>
    /// 把角色登记到其声明的波次（角色注册时调用）。
    /// </summary>
    /// <param name="type">角色类型（需已注册为模板）。</param>
    internal static void AttachRole(Type type)
    {
        if (!CustomRoleTemplateManager.TryGetRoleTemplate(type, out var role) ||
            role is not IWaveSpawnRole spawn)
        {
            return;   // 未实现 IWaveSpawnRole 的角色不参与波次刷新
        }

        foreach (var wave in spawn.Waves)
            wave?.AddRole(role);
    }

    /// <summary>按 Id 查找波次实例。</summary>
    /// <param name="id">波次 Id。</param>
    /// <returns>波次实例；不存在为 <see langword="null"/>。</returns>
    internal static CustomWave? Find(string? id) =>
        id is not null && Registry.TryGetValue(id, out var wave) ? wave : null;

    /// <summary>清空模板与实例（框架卸载时调用）。</summary>
    internal static void Clear()
    {
        Templates.Clear();
        Registry.Clear();
    }
}
