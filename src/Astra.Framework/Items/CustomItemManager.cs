using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

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

    /// <summary>已经实例化过的模板类型，避免重复调用 <see cref="Initialize"/> 时重复构造。</summary>
    private static readonly HashSet<Type> InitializedTemplates = [];

    /// <summary>每个具体物品类型是否覆写了 <see cref="CustomItem.OnShooting"/> 的缓存。</summary>
    private static readonly Dictionary<Type, bool> ShootingOverrideCache = [];

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

    /// <summary>按模板实例化全部自定义物品（幂等；允许在初始化后追加模板再调用）。</summary>
    internal static void Initialize()
    {
        foreach (var type in Templates.ToArray())
        {
            if (!InitializedTemplates.Add(type))
                continue;

            try
            {
                InstantiateTemplate(type);
            }
            catch (Exception ex)
            {
                LabApi.Features.Console.Logger.Error($"[CustomItemManager] 模板 {type.Name} 实例化异常：{ex.Message}");
            }
        }
    }

    /// <summary>按实例登记一个已构造的自定义物品定义（外部框架 / JSON 适配器用）。</summary>
    /// <param name="item">物品定义实例。</param>
    /// <returns>登记成功（或同实例重复登记）返回 <see langword="true"/>；Id 冲突返回 <see langword="false"/>。</returns>
    internal static bool RegisterInstance(CustomItem item)
    {
        if (item is null || string.IsNullOrWhiteSpace(item.Id))
            return false;

        if (Registry.TryGetValue(item.Id, out var existing))
        {
            if (ReferenceEquals(existing, item))
                return true;

            LabApi.Features.Console.Logger.Warn(
                $"[CustomItemManager] 物品 Id 重复：{item.Id}（{existing.GetType().Name} 已存在，{item.GetType().Name} 被拒绝）。");
            return false;
        }

        if (ByItemType.TryGetValue(item.BaseItem, out var sameBase))
        {
            LabApi.Features.Console.Logger.Warn(
                $"[CustomItemManager] 物品类型 {item.BaseItem} 已被 {sameBase.GetType().Name} 占用，" +
                $"{item.GetType().Name} 仍按 Id 登记，但无法通过类型识别。");
        }
        else
        {
            ByItemType[item.BaseItem] = item;
        }

        Registry[item.Id] = item;
        NotifyRegister(item);
        return true;
    }

    /// <summary>按 Id 注销自定义物品定义；玩家已持有的物品实例不受影响。</summary>
    /// <param name="id">物品 Id。</param>
    /// <returns>确实注销时返回 <see langword="true"/>。</returns>
    internal static bool Unregister(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || !Registry.TryGetValue(id, out var item))
            return false;

        Registry.Remove(id);

        if (ByItemType.TryGetValue(item.BaseItem, out var mapped) && ReferenceEquals(mapped, item))
        {
            ByItemType.Remove(item.BaseItem);
            var replacement = Registry.Values.FirstOrDefault(candidate => candidate.BaseItem == item.BaseItem);
            if (replacement is not null)
                ByItemType[item.BaseItem] = replacement;
        }

        NotifyUnregister(item);
        InitializedTemplates.Remove(item.GetType());
        return true;
    }

    /// <summary>按模板类型构造并登记一个物品定义。</summary>
    private static void InstantiateTemplate(Type type)
    {
        if (Activator.CreateInstance(type, nonPublic: true) is not CustomItem item ||
            string.IsNullOrWhiteSpace(item.Id))
        {
            LabApi.Features.Console.Logger.Warn(
                $"[CustomItemManager] 模板 {type.Name} 实例化失败或缺少 Id，已跳过。");
            return;
        }

        if (Registry.TryGetValue(item.Id, out var sameId))
        {
            LabApi.Features.Console.Logger.Warn(
                $"[CustomItemManager] 物品 Id 重复：{item.Id}（{sameId.GetType().Name} 已被 {type.Name} 覆盖）。");
            NotifyUnregister(sameId);
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
        NotifyRegister(item);
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

    /// <summary>
    /// 判断该自定义物品是否接管玩家开火：只有覆写了 <see cref="CustomItem.OnShooting"/> 的类型才算。
    /// </summary>
    /// <param name="item">自定义物品定义。</param>
    /// <returns>覆写了 <see cref="CustomItem.OnShooting"/> 时返回 <see langword="true"/>。</returns>
    /// <remarks>
    /// 基类的默认实现只是「放行」，不构成接管。因此未覆写该方法的自定义物品不会进入射击链路，
    /// 原版射击完全不受影响；是否接管由开发者覆写方法自行选择。
    /// </remarks>
    internal static bool InterceptsShooting(CustomItem item)
    {
        var type = item.GetType();
        if (ShootingOverrideCache.TryGetValue(type, out var intercepts))
            return intercepts;

        var baseMethod = typeof(CustomItem).GetMethod(
            nameof(CustomItem.OnShooting), BindingFlags.Instance | BindingFlags.Public);
        intercepts = baseMethod is not null && type.GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Any(method => method.Name == nameof(CustomItem.OnShooting)
                           && method.DeclaringType != typeof(CustomItem)
                           && method.GetBaseDefinition().Equals(baseMethod));

        ShootingOverrideCache[type] = intercepts;
        return intercepts;
    }

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
        foreach (var item in Registry.Values)
            NotifyUnregister(item);

        Templates.Clear();
        InitializedTemplates.Clear();
        Registry.Clear();
        ByItemType.Clear();
        Tracked.Clear();
        ShootingOverrideCache.Clear();
    }

    /// <summary>调用定义的登记回调；回调异常只记日志。</summary>
    /// <param name="item">已登记的物品定义。</param>
    private static void NotifyRegister(CustomItem item)
    {
        try
        {
            item.OnRegisterEvents();
        }
        catch (Exception ex)
        {
            LabApi.Features.Console.Logger.Error($"[CustomItemManager] 物品 {item.Id} 登记回调异常：{ex.Message}");
        }
    }

    /// <summary>调用定义的注销回调；回调异常只记日志。</summary>
    /// <param name="item">待注销的物品定义。</param>
    private static void NotifyUnregister(CustomItem item)
    {
        try
        {
            item.OnUnregisterEvents();
        }
        catch (Exception ex)
        {
            LabApi.Features.Console.Logger.Error($"[CustomItemManager] 物品 {item.Id} 注销回调异常：{ex.Message}");
        }
    }
}
