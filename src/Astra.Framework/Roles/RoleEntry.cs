using System;

using PlayerRoles;

namespace Astra.Roles;

/// <summary>
/// 角色条目：同时表达「原版角色类型」与「自定义角色定义」，用于波次组成等需要混编的场景。
/// </summary>
/// <remarks>
/// <para>
/// 原版角色用 <see cref="RoleTypeId"/>；自定义角色可传实例或 Id（Id 在解析时查当前模板注册表）。
/// 自定义条目的 <see cref="VanillaRole"/> 优先取其 <see cref="CustomRole.RoleType"/>。
/// </para>
/// <para>
/// 相等性：原版按 <see cref="RoleTypeId"/>，自定义按 <see cref="CustomRoleId"/>（忽略大小写）。
/// </para>
/// </remarks>
public readonly struct RoleEntry : IEquatable<RoleEntry>
{
    private RoleEntry(RoleTypeId vanillaRole, CustomRole? customRole, string? customRoleId)
    {
        VanillaRole = vanillaRole;
        CustomRole = customRole;
        CustomRoleId = customRoleId;
    }

    /// <summary>原版角色类型；自定义条目为其 <see cref="CustomRole.RoleType"/>；未解析时为 <see cref="RoleTypeId.None"/>。</summary>
    public RoleTypeId VanillaRole { get; }

    /// <summary>自定义角色定义；原版条目或尚未注册时为 <see langword="null"/>。</summary>
    public CustomRole? CustomRole { get; }

    /// <summary>自定义角色 Id；原版条目为 <see langword="null"/>。</summary>
    public string? CustomRoleId { get; }

    /// <summary>是否为自定义角色条目。</summary>
    public bool IsCustom => CustomRole is not null || !string.IsNullOrWhiteSpace(CustomRoleId);

    /// <summary>声明一个原版角色条目。</summary>
    /// <param name="role">原版角色类型。</param>
    /// <returns>角色条目。</returns>
    public static RoleEntry Vanilla(RoleTypeId role) => new(role, null, null);

    /// <summary>用自定义角色定义声明条目。</summary>
    /// <param name="role">自定义角色定义。</param>
    /// <returns>角色条目。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="role"/> 为 <see langword="null"/>。</exception>
    public static RoleEntry Custom(CustomRole role)
    {
        if (role is null)
            throw new ArgumentNullException(nameof(role));

        return new RoleEntry(role.RoleType, role, role.Id);
    }

    public static RoleEntry Custom<T>() where T : CustomRole
    {
        if (!CustomRoleTemplateManager.TryGetRoleTemplate(typeof(T), out var role))
            throw new InvalidOperationException($"未注册自定义角色模板 {typeof(T).FullName}。");

        return new RoleEntry(role.RoleType, role, role.Id);
    }

    /// <summary>用自定义角色 Id 声明条目；解析时再查模板注册表。</summary>
    /// <param name="id">自定义角色 Id。</param>
    /// <returns>角色条目。</returns>
    /// <exception cref="ArgumentException"><paramref name="id"/> 为空白。</exception>
    public static RoleEntry Custom(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("自定义角色 Id 不能为空白。", nameof(id));

        return CustomRoleTemplateManager.TryGetRoleTemplate(id, out var role)
            ? new RoleEntry(role.RoleType, role, id)
            : new RoleEntry(RoleTypeId.None, null, id);
    }

    /// <summary>原版角色隐式转换为条目。</summary>
    /// <param name="role">原版角色类型。</param>
    public static implicit operator RoleEntry(RoleTypeId role) => Vanilla(role);

    /// <summary>自定义角色隐式转换为条目。</summary>
    /// <param name="role">自定义角色定义。</param>
    public static implicit operator RoleEntry(CustomRole role) => Custom(role);

    /// <summary>解析出自定义角色定义（Id 条目会查当前模板注册表）。</summary>
    /// <returns>定义；原版或未注册时为 <see langword="null"/>。</returns>
    internal CustomRole? ResolveCustom()
    {
        if (CustomRole is not null)
            return CustomRole;

        if (CustomRoleId is not { Length: > 0 } id)
            return null;

        return CustomRoleTemplateManager.TryGetRoleTemplate(id, out var role) ? role : null;
    }

    /// <summary>解析出原版角色类型（自定义条目取其 RoleType）。</summary>
    /// <returns>原版角色类型；无法解析时为 <see cref="RoleTypeId.None"/>。</returns>
    internal RoleTypeId ResolveVanillaRole()
    {
        if (!IsCustom)
            return VanillaRole;

        var resolved = ResolveCustom();
        return resolved is not null ? resolved.RoleType : VanillaRole;
    }

    /// <inheritdoc />
    public bool Equals(RoleEntry other)
    {
        if (IsCustom != other.IsCustom)
            return false;

        return IsCustom
            ? string.Equals(CustomRole?.Id ?? CustomRoleId, other.CustomRole?.Id ?? other.CustomRoleId,
                StringComparison.OrdinalIgnoreCase)
            : VanillaRole == other.VanillaRole;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is RoleEntry other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() =>
        IsCustom
            ? StringComparer.OrdinalIgnoreCase.GetHashCode(CustomRole?.Id ?? CustomRoleId ?? string.Empty)
            : (int)VanillaRole;

    /// <inheritdoc />
    public override string ToString() =>
        IsCustom ? $"Custom:{CustomRole?.Id ?? CustomRoleId}" : $"Vanilla:{VanillaRole}";
}
