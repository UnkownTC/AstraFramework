using System;
using System.Collections.Generic;

using Astra.Roles;
using Astra.Teams;

namespace Astra.Waves;

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

    /// <summary>是否已完成一次性实例化；外部内容只能在完成后通过实例注册进入。</summary>
    internal static bool Initialized { get; private set; }

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
                    NotifyUnregister(existing);
                }

                Registry[wave.Id] = wave;

                foreach (var team in wave.Teams)
                {
                    if (team.CustomTeam is not { } customTeam)
                        continue;   // 原版阵营条目只用于配色 / 兜底

                    customTeam.AddWave(wave);
                }

                NotifyRegister(wave);
            }
            catch (Exception ex)
            {
                LabApi.Features.Console.Logger.Error($"[CustomWaveManager] 模板 {type.Name} 实例化异常：{ex.Message}");
            }
        }

        Initialized = true;
    }

    /// <summary>按实例注册波次（外部框架 / 动态定义用）。</summary>
    /// <param name="wave">波次实例。</param>
    /// <returns>注册成功（或同实例重复注册）时返回 <see langword="true"/>；未初始化、Id 冲突、依赖缺失等失败返回 <see langword="false"/>。</returns>
    internal static bool RegisterInstance(CustomWave wave)
    {
        if (!Initialized || wave is null || string.IsNullOrWhiteSpace(wave.Id))
            return false;

        if (Registry.TryGetValue(wave.Id, out var occupied))
        {
            if (ReferenceEquals(occupied, wave))
                return true;   // 同实例重复注册：幂等

            LabApi.Features.Console.Logger.Warn(
                $"[CustomWaveManager] 波次 Id {wave.Id} 已被 {occupied.GetType().FullName} 占用，" +
                $"{wave.GetType().FullName} 注册被拒绝（不覆盖既有实例）。");
            return false;
        }

        IReadOnlyList<TeamRef> teams;
        try
        {
            teams = wave.Teams;
        }
        catch (Exception ex)
        {
            LabApi.Features.Console.Logger.Error(
                $"[CustomWaveManager] 波次 {wave.Id} 的 Teams 解析失败，注册被拒绝：{ex.Message}");
            return false;
        }

        Registry[wave.Id] = wave;

        foreach (var team in teams)
        {
            if (team.CustomTeam is not { } customTeam)
                continue;   // 原版阵营条目只用于配色 / 兜底

            customTeam.AddWave(wave);
        }

        NotifyRegister(wave);
        return true;
    }

    /// <summary>按 Id 注销波次，并摘除它与阵营、角色的反向关联。</summary>
    /// <param name="id">波次 Id。</param>
    /// <returns>确实注销时返回 <see langword="true"/>。</returns>
    internal static bool Unregister(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || !Registry.TryGetValue(id, out var wave))
            return false;

        Registry.Remove(id);

        try
        {
            foreach (var team in wave.Teams)
                team.CustomTeam?.RemoveWave(wave);
        }
        catch (Exception ex)
        {
            LabApi.Features.Console.Logger.Error($"[CustomWaveManager] 波次 {id} 注销时摘除阵营关联失败：{ex.Message}");
        }

        wave.ClearRoles();
        NotifyUnregister(wave);
        return true;
    }

    /// <summary>按 Id 查找波次实例。</summary>
    /// <param name="id">波次 Id。</param>
    /// <returns>波次实例；不存在为 <see langword="null"/>。</returns>
    internal static CustomWave? Find(string? id) =>
        id is not null && Registry.TryGetValue(id, out var wave) ? wave : null;

    /// <summary>清空模板与实例（框架卸载时调用）。</summary>
    internal static void Clear()
    {
        foreach (var wave in Registry.Values)
        {
            wave.ClearRoles();
            NotifyUnregister(wave);
        }

        Templates.Clear();
        Registry.Clear();
        Initialized = false;
    }

    /// <summary>调用实例的登记回调；回调异常只记日志，不中断其余实例。</summary>
    /// <param name="wave">已登记的波次实例。</param>
    private static void NotifyRegister(CustomWave wave)
    {
        try
        {
            wave.OnRegisterEvents();
        }
        catch (Exception ex)
        {
            LabApi.Features.Console.Logger.Error($"[CustomWaveManager] 波次 {wave.Id} 登记回调异常：{ex.Message}");
        }
    }

    /// <summary>调用实例的注销回调；回调异常只记日志。</summary>
    /// <param name="wave">待注销的波次实例。</param>
    private static void NotifyUnregister(CustomWave wave)
    {
        try
        {
            wave.OnUnregisterEvents();
        }
        catch (Exception ex)
        {
            LabApi.Features.Console.Logger.Error($"[CustomWaveManager] 波次 {wave.Id} 注销回调异常：{ex.Message}");
        }
    }
}
