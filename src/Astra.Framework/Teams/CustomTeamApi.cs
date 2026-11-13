using System;
using System.Collections.Generic;
using System.Linq;

namespace Astra.Teams;

/// <summary>自定义阵营对外 API：注册表查询与每回合实例化。</summary>
public static class CustomTeamApi
{
    /// <summary>当前回合的全部阵营实例。</summary>
    public static IReadOnlyDictionary<string, CustomTeam> Teams => CustomTeam.All;

    /// <summary>按 Id 查找当前回合的阵营实例。</summary>
    /// <param name="id">阵营 Id。</param>
    /// <returns>阵营实例；不存在为 <see langword="null"/>。</returns>
    public static CustomTeam? Find(string id) => CustomTeam.Find(id);

    /// <summary>按类型查找阵营实例。</summary>
    /// <typeparam name="T">阵营类型。</typeparam>
    /// <returns>阵营实例；未注册或类型不匹配为 <see langword="null"/>。</returns>
    /// <remarks>用类型代替 Id 字符串，避免拼写错误导致的静默失效。</remarks>
    public static T? Find<T>() where T : CustomTeam =>
        Teams.Values.OfType<T>().FirstOrDefault();

    /// <summary>按类型查找阵营实例。</summary>
    /// <typeparam name="T">阵营类型。</typeparam>
    /// <param name="team">取到的实例。</param>
    /// <returns>存在时返回 <see langword="true"/>。</returns>
    public static bool TryFind<T>(out T team) where T : CustomTeam
    {
        team = Find<T>()!;
        return team is not null;
    }

    /// <summary>按模板实例化阵营（幂等，框架启动时自动调用；跨回合复用不重建）。</summary>
    public static void Initialize() => CustomTeam.Initialize();

    /// <summary>登记阵营模板（框架自动扫描时调用，通常无需手动）。</summary>
    /// <param name="type">阵营类型。</param>
    public static void RegisterTemplate(Type type) => CustomTeam.RegisterTemplate(type);

    /// <summary>按实例注册阵营（外部框架 / 动态定义用）。</summary>
    /// <param name="team">阵营实例。</param>
    /// <returns>注册成功（或同实例重复注册）返回 <see langword="true"/>；未初始化或 Id 冲突返回 <see langword="false"/>。</returns>
    /// <remarks>须在框架自动注册完成后调用，否则会因阵营实例化早退而丢失 C# 模板。</remarks>
    public static bool Register(CustomTeam team) => CustomTeam.RegisterInstance(team);

    /// <summary>按 Id 注销阵营。</summary>
    /// <param name="id">阵营 Id。</param>
    /// <returns>确实注销时返回 <see langword="true"/>；仍被波次引用时返回 <see langword="false"/>。</returns>
    public static bool Unregister(string id) => CustomTeam.Unregister(id);
}
