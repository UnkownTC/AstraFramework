using System;

namespace Astra.Items;

/// <summary>
/// 物品条目：同时表达「原版物品类型」与「自定义物品定义」，供角色初始物品等场景统一声明。
/// </summary>
/// <remarks>
/// <para>
/// 原版物品用 <see cref="ItemType"/>；自定义物品可传实例或 Id（Id 在应用时解析，允许先声明后注册）。
/// 自定义条目的 <see cref="VanillaType"/> 为其承载类型 <see cref="CustomItem.BaseItem"/>。
/// </para>
/// <para>
/// 相等性：原版按 <see cref="ItemType"/>，自定义按 <see cref="CustomItemId"/>（忽略大小写）。
/// </para>
/// </remarks>
public readonly struct ItemEntry : IEquatable<ItemEntry>
{
    private ItemEntry(ItemType vanillaType, CustomItem? customItem, string? customItemId)
    {
        VanillaType = vanillaType;
        CustomItem = customItem;
        CustomItemId = customItemId;
    }

    /// <summary>原版物品类型；自定义条目为其承载类型；未解析的 Id-only 条目为 <see cref="ItemType.None"/>。</summary>
    public ItemType VanillaType { get; }

    /// <summary>自定义物品定义；原版条目或尚未注册时为 <see langword="null"/>。</summary>
    public CustomItem? CustomItem { get; }

    /// <summary>自定义物品 Id；原版条目为 <see langword="null"/>。</summary>
    public string? CustomItemId { get; }

    /// <summary>是否为自定义物品条目。</summary>
    public bool IsCustom => CustomItem is not null || !string.IsNullOrWhiteSpace(CustomItemId);

    /// <summary>声明一个原版物品条目。</summary>
    /// <param name="type">原版物品类型。</param>
    /// <returns>物品条目。</returns>
    public static ItemEntry Vanilla(ItemType type) => new(type, null, null);

    /// <summary>用自定义物品定义声明条目。</summary>
    /// <param name="item">自定义物品定义。</param>
    /// <returns>物品条目。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> 为 <see langword="null"/>。</exception>
    public static ItemEntry Custom(CustomItem item)
    {
        if (item is null)
            throw new ArgumentNullException(nameof(item));

        return new ItemEntry(item.BaseItem, item, item.Id);
    }

    /// <summary>用自定义物品 Id 声明条目；应用时再解析定义。</summary>
    /// <param name="id">自定义物品 Id。</param>
    /// <returns>物品条目。</returns>
    /// <exception cref="ArgumentException"><paramref name="id"/> 为空白。</exception>
    public static ItemEntry Custom(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("自定义物品 Id 不能为空白。", nameof(id));

        var existing = CustomItemManager.Find(id);
        return new ItemEntry(existing?.BaseItem ?? ItemType.None, existing, id);
    }

    /// <summary>原版物品类型隐式转换为条目。</summary>
    /// <param name="type">原版物品类型。</param>
    public static implicit operator ItemEntry(ItemType type) => Vanilla(type);

    /// <summary>自定义物品定义隐式转换为条目。</summary>
    /// <param name="item">自定义物品定义。</param>
    public static implicit operator ItemEntry(CustomItem item) => Custom(item);

    /// <summary>解析出自定义物品定义（Id 条目会查当前注册表）。</summary>
    /// <returns>定义；原版条目或未注册时为 <see langword="null"/>。</returns>
    internal CustomItem? ResolveCustom() =>
        CustomItem ?? (string.IsNullOrWhiteSpace(CustomItemId) ? null : CustomItemManager.Find(CustomItemId));

    /// <summary>解析出原版物品类型（自定义条目取其承载类型）。</summary>
    /// <returns>原版物品类型；无法解析时为 <see cref="ItemType.None"/>。</returns>
    internal ItemType ResolveVanillaType() =>
        IsCustom ? ResolveCustom()?.BaseItem ?? ItemType.None : VanillaType;

    /// <inheritdoc />
    public bool Equals(ItemEntry other)
    {
        if (IsCustom != other.IsCustom)
            return false;

        return IsCustom
            ? string.Equals(CustomItem?.Id ?? CustomItemId, other.CustomItem?.Id ?? other.CustomItemId,
                StringComparison.OrdinalIgnoreCase)
            : VanillaType == other.VanillaType;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is ItemEntry other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() =>
        IsCustom
            ? StringComparer.OrdinalIgnoreCase.GetHashCode(CustomItem?.Id ?? CustomItemId ?? string.Empty)
            : (int)VanillaType;

    /// <inheritdoc />
    public override string ToString() =>
        IsCustom ? $"Custom:{CustomItem?.Id ?? CustomItemId}" : $"Vanilla:{VanillaType}";
}
