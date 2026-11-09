using System;
using System.Collections.Generic;
using System.Linq;

using Astra.Abstractions;
using Astra.Systems.CustomItemSystem;

using InventorySystem.Items;

using LabApi.Features.Wrappers;

namespace Astra.Apis;

/// <summary>自定义物品对外 API：定义查询、发放与实例溯源。</summary>
public static class CustomItemApi
{
    /// <summary>当前已实例化的自定义物品定义。</summary>
    public static IReadOnlyDictionary<string, CustomItem> Items => CustomItemManager.All;

    /// <summary>按 Id 查找自定义物品定义。</summary>
    /// <param name="id">物品 Id。</param>
    /// <returns>定义；不存在为 <see langword="null"/>。</returns>
    public static CustomItem? Find(string id) => CustomItemManager.Find(id);

    /// <summary>按类型查找自定义物品定义。</summary>
    /// <typeparam name="T">物品类型。</typeparam>
    /// <returns>定义；未注册或类型不匹配为 <see langword="null"/>。</returns>
    public static T? Find<T>() where T : CustomItem => Items.Values.OfType<T>().FirstOrDefault();

    /// <summary>按原版物品类型查找自定义定义。</summary>
    /// <param name="itemType">原版物品类型。</param>
    /// <returns>定义；无注册为 <see langword="null"/>。</returns>
    public static CustomItem? FindByItemType(ItemType itemType) => CustomItemManager.FindByItemType(itemType);

    /// <summary>按物品实例查找其自定义定义。</summary>
    /// <param name="item">物品实例。</param>
    /// <returns>定义；非自定义物品为 <see langword="null"/>。</returns>
    public static CustomItem? FindByItem(Item? item) => CustomItemManager.FindByItem(item);

    /// <summary>按 Id 给玩家发放自定义物品。</summary>
    /// <param name="player">目标玩家。</param>
    /// <param name="id">物品 Id。</param>
    /// <returns>生成的物品实例；失败为 <see langword="null"/>。</returns>
    public static Item? Give(Player player, string id) => CustomItemManager.Find(id)?.Give(player);

    /// <summary>按类型给玩家发放自定义物品。</summary>
    /// <typeparam name="T">物品类型。</typeparam>
    /// <param name="player">目标玩家。</param>
    /// <returns>生成的物品实例；失败为 <see langword="null"/>。</returns>
    public static Item? Give<T>(Player player) where T : CustomItem => Find<T>()?.Give(player);

    /// <summary>登记物品模板（框架自动扫描时调用，通常无需手动）。</summary>
    /// <param name="type">物品类型。</param>
    public static void RegisterTemplate(Type type) => CustomItemManager.RegisterTemplate(type);

    /// <summary>按模板实例化全部自定义物品（幂等，框架启动时自动调用）。</summary>
    public static void Initialize() => CustomItemManager.Initialize();
}
