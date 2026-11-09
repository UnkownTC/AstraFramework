using System;
using System.Collections.Generic;

using Astra.Teams;
using Astra.Waves;

using LabApi.Features.Wrappers;

namespace Astra.Items;

/// <summary>
/// 自定义物品的模板登记、一次性实例化与原版物品类型映射。
/// </summary>
/// <remarks>
/// 生命周期与 <see cref="CustomTeam"/> / <see cref="CustomWave"/> 一致：插件启用时实例化一次，跨回合复用。
/// 同一 <see cref="ItemType"/> 只保留首个注册的自定义物品，冲突时输出告警。
/// </remarks>
internal static class CustomItemManager
{
    private static readonly Dictionary<string, CustomItem> Registry =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<ItemType, CustomItem> ByItemType = new();

    private static readonly List<Type> Templates = [];

    /// <summary>物品实例 → 自定义物品定义（用于射击等事件溯源）。</summary>
    private static readonly Dictionary<Item, CustomItem> Tracked = [];

    /// <summary>当前的全部自定义物品定义。</summary>
    internal static IReadOnlyDictionary<string, CustomItem> All => Registry;

    /// <summary>登记物品模板类型。</summary>
    /// <param name="type">物品类型。</param>
    internal static void RegisterTemplate(Type type)
    {
        if (type is null || type.IsAbstract || !typeof(CustomItem).IsAssignableFrom(type))
            return;

        if (!Templates.Contains(type))
            Templates.Add(type);
    }

    /// <summary>按模板实例化全部自定义物品（幂等）。</summary>
    internal static void Initialize()
    {
        if (Registry.Count > 0)
            return;

        foreach (var type in Templates)
        {
            try
            {
                if (Activator.CreateInstance(type, nonPublic: true) is not CustomItem item ||
                    string.IsNullOrWhiteSpace(item.Id))
                {
                    LabApi.Features.Console.Logger.Warn(
                        $"[CustomItemManager] 模板 {type.Name} 实例化失败或缺少 Id，已跳过。");
                    continue;
                }

                if (Registry.TryGetValue(item.Id, out var sameId))
                {
                    LabApi.Features.Console.Logger.Warn(
                        $"[CustomItemManager] 物品 Id 重复：{item.Id}（{sameId.GetType().Name} 已被 {type.Name} 覆盖）。");
                }

                if (ByItemType.TryGetValue(item.BaseItem, out var sameBase))
                {
                    LabApi.Features.Console.Logger.Warn(
                        $"[CustomItemManager] 物品类型 {item.BaseItem} 已被 {sameBase.GetType().Name} 占用，" +
                        $"{type.Name} 无法通过类型识别。");
                }
                else
                {
                    ByItemType[item.BaseItem] = item;
                }

                Registry[item.Id] = item;
            }
            catch (Exception ex)
            {
                LabApi.Features.Console.Logger.Error($"[CustomItemManager] 模板 {type.Name} 实例化异常：{ex.Message}");
            }
        }
    }

    /// <summary>按 Id 查找物品定义。</summary>
    /// <param name="id">物品 Id。</param>
    /// <returns>物品定义；不存在为 <see langword="null"/>。</returns>
    internal static CustomItem? Find(string? id) =>
        id is not null && Registry.TryGetValue(id, out var item) ? item : null;

    /// <summary>按物品实例查找其自定义定义。</summary>
    /// <param name="item">物品实例。</param>
    /// <returns>自定义定义；非自定义物品为 <see langword="null"/>。</returns>
    internal static CustomItem? FindByItem(Item? item) =>
        item is not null && Tracked.TryGetValue(item, out var custom) ? custom : null;

    /// <summary>按原版物品类型查找自定义定义。</summary>
    /// <param name="itemType">原版物品类型。</param>
    /// <returns>自定义定义；无注册为 <see langword="null"/>。</returns>
    internal static CustomItem? FindByItemType(ItemType itemType) =>
        ByItemType.TryGetValue(itemType, out var custom) ? custom : null;

    /// <summary>登记物品实例与定义的对应关系。</summary>
    /// <param name="item">物品实例。</param>
    /// <param name="custom">自定义定义。</param>
    internal static void Track(Item item, CustomItem custom)
    {
        if (item is not null && custom is not null)
            Tracked[item] = custom;
    }

    /// <summary>清空模板、实例与追踪表（框架卸载时调用）。</summary>
    internal static void Clear()
    {
        Templates.Clear();
        Registry.Clear();
        ByItemType.Clear();
        Tracked.Clear();
    }
}
