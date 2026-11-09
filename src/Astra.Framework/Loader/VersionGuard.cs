using System;
using System.Reflection;

using Astra.Attributes;

using LabApi.Features;

using Logger = LabApi.Features.Console.Logger;
using Astra;
using Astra.Enums;
using Astra.Extensions;
using Astra.Roles;
using Astra.Teams;
using Astra.Waves;
using Astra.Items;
using Astra.Effects;
using Astra.Abilities;
using Astra.HumeShield;
using Astra.Patching;
using Astra.Hints;
using Astra.Audio;
using Astra.Countdown;
using Astra.Cancellation;
using Astra.ServerSpecificSettings;

namespace Astra.Loader;

/// <summary>
/// 版本门禁：按 <see cref="PatchApiVersionAttribute"/> / <see cref="PatchGameVersionAttribute"/> 校验组件兼容性。
/// </summary>
/// <remarks>
/// 规则：
/// <list type="bullet">
/// <item><b>大版本不一致</b>（LabApi 主版本 / 游戏主版本）→ 拒绝登记并输出 Error。</item>
/// <item>其它位不一致 → 允许登记，但输出 Warning。</item>
/// <item>未标注任何版本特性 → 直接放行，不做任何检查。</item>
/// </list>
/// 成员上未标注时，会回退到其声明类型上查找，便于在类级统一声明、方法级继承约束。
/// </remarks>
internal static class VersionGuard
{
    /// <summary>校验成员（类型或方法）是否允许在当前运行环境中登记。</summary>
    /// <param name="member">待校验的成员。</param>
    /// <param name="ignoreApiMajorDiff">为 <see langword="true"/> 时把 LabApi 主版本差异降级为警告。</param>
    /// <param name="ignoreGameMajorDiff">为 <see langword="true"/> 时把游戏主版本差异降级为警告。</param>
    /// <returns>允许登记返回 <see langword="true"/>；大版本不匹配且未忽略时返回 <see langword="false"/>。</returns>
    public static bool Validate(MemberInfo member, bool ignoreApiMajorDiff = false, bool ignoreGameMajorDiff = false)
    {
        if (member is null)
            return true;

        var name = Describe(member);
        var allowed = true;

        var api = GetAttribute<PatchApiVersionAttribute>(member);
        if (api is not null)
            allowed &= CheckApiVersion(name, api.ApiVersion, ignoreApiMajorDiff);

        var game = GetAttribute<PatchGameVersionAttribute>(member);
        if (game is not null)
            allowed &= CheckGameVersion(name, game, ignoreGameMajorDiff);

        return allowed;
    }

    /// <summary>校验 LabApi 版本约束。</summary>
    /// <param name="name">成员描述，用于日志。</param>
    /// <param name="expected">声明的版本。</param>
    /// <param name="ignoreMajorDiff">是否把主版本差异降级为警告。</param>
    /// <returns>允许登记时为 <see langword="true"/>。</returns>
    private static bool CheckApiVersion(string name, Version expected, bool ignoreMajorDiff)
    {
        var current = LabApiProperties.CurrentVersion;

        if (expected.Major != current.Major)
        {
            if (ignoreMajorDiff)
            {
                Logger.Warn($"[VersionGuard] {name} 要求 LabApi {expected}，当前 {current}：主版本不一致，" +
                            "配置已忽略该差异，继续登记。");
                return true;
            }

            Logger.Error($"[VersionGuard] {name} 要求 LabApi {expected}，当前 {current}：主版本不一致，拒绝登记。");
            return false;
        }

        if (expected != current)
        {
            Logger.Warn($"[VersionGuard] {name} 声明 LabApi {expected}，当前 {current}：非主版本差异，继续登记。");
        }

        return true;
    }

    /// <summary>校验游戏版本约束。</summary>
    /// <param name="name">成员描述，用于日志。</param>
    /// <param name="expected">声明的版本。</param>
    /// <param name="ignoreMajorDiff">是否把主版本差异降级为警告。</param>
    /// <returns>允许登记时为 <see langword="true"/>。</returns>
    private static bool CheckGameVersion(string name, PatchGameVersionAttribute expected, bool ignoreMajorDiff)
    {
        var currentMajor = GameCore.Version.Major;
        var currentMinor = GameCore.Version.Minor;
        var currentRevision = GameCore.Version.Revision;

        var expectedText = $"{expected.MajorVersion}.{expected.MinorVersion}.{expected.RevisionVersion}{expected.VersionSuffix}";
        var currentText = $"{currentMajor}.{currentMinor}.{currentRevision}";

        if (expected.MajorVersion != currentMajor)
        {
            if (ignoreMajorDiff)
            {
                Logger.Warn($"[VersionGuard] {name} 要求游戏 {expectedText}，当前 {currentText}：主版本不一致，" +
                            "配置已忽略该差异，继续登记。");
                return true;
            }

            Logger.Error($"[VersionGuard] {name} 要求游戏 {expectedText}，当前 {currentText}：主版本不一致，拒绝登记。");
            return false;
        }

        if (expected.MinorVersion != currentMinor ||
            expected.RevisionVersion != currentRevision ||
            !string.IsNullOrEmpty(expected.VersionSuffix))
        {
            Logger.Warn($"[VersionGuard] {name} 声明游戏 {expectedText}，当前 {currentText}：非主版本差异，继续登记。");
        }

        return true;
    }

    /// <summary>读取特性：成员本身优先，找不到时回退到声明类型。</summary>
    /// <typeparam name="TAttribute">特性类型。</typeparam>
    /// <param name="member">成员。</param>
    /// <returns>找到的特性；都没有时为 <see langword="null"/>。</returns>
    private static TAttribute? GetAttribute<TAttribute>(MemberInfo member) where TAttribute : Attribute
    {
        var own = member.GetCustomAttribute<TAttribute>();
        if (own is not null)
            return own;

        return member.DeclaringType?.GetCustomAttribute<TAttribute>();
    }

    /// <summary>生成用于日志的成员描述。</summary>
    /// <param name="member">成员。</param>
    /// <returns>形如 <c>命名空间.类型.方法</c> 或 <c>命名空间.类型</c> 的字符串。</returns>
    private static string Describe(MemberInfo member) => member switch
    {
        Type type => type.FullName ?? type.Name,
        MethodInfo method => $"{method.DeclaringType?.FullName ?? method.DeclaringType?.Name}.{method.Name}",
        _ => member.Name,
    };
}
