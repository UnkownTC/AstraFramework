using System;
using System.Collections.Generic;

using Astra.Attributes;

using JetBrains.Annotations;

using LabApi.Events.CustomHandlers;
using LabApi.Features.Console;
using LabApi.Features.Wrappers;

using UserSettings.ServerSpecific;

using ServerSpecificSettingBase = UserSettings.ServerSpecific.ServerSpecificSettingBase;

// ReSharper disable MemberCanBePrivate.Global

namespace Astra.Abilities;
    /// <summary>
    /// 单个玩家的技能管理器：负责该玩家技能实例的增删查与触发。
    /// 每个玩家在加入服务器时由框架创建一个实例，离开时释放。
    /// </summary>
    public sealed class CustomAbilityManager : CustomEventsHandler, IDisposable
    {
        /// <summary>按玩家 NetworkId 索引的全局管理器表。</summary>
        private static Dictionary<uint, CustomAbilityManager> Managers { get; } = new();

        /// <summary>按 SettingId 索引的技能（仅按键技能）。</summary>
        private Dictionary<int, CustomAbility> AbilitiesBySettingId { get; } = new();

        /// <summary>「技能槽」的起始 SettingId（对应下发的 Ability1 按键设置）。</summary>
        private const int FirstAbilitySlotId = 0;

        /// <summary>可选技能槽数量（对应 Ability1..Ability4 四个主动技能键位）。</summary>
        private const int MaxAbilitySlots = 4;

        /// <summary>按 Id 索引的技能。</summary>
        private Dictionary<string, CustomAbility> AbilitiesById { get; } = new();

        /// <summary>
        /// 初始化技能管理器。
        /// </summary>
        /// <param name="owner">归属玩家。</param>
        public CustomAbilityManager(Player owner)
        {
            Owner = owner;
        }

        /// <summary>归属玩家。</summary>
        public Player Owner { get; }

        /// <summary>该玩家当前拥有的所有技能。</summary>
        public IReadOnlyCollection<CustomAbility> Abilities => AbilitiesById.Values;

        /// <summary>技能总数。</summary>
        public int AbilityCount => AbilitiesById.Count;

        /// <summary>按键技能总数。</summary>
        public int KeybindAbilityCount => AbilitiesBySettingId.Count;

        /// <summary>按泛型类型获取技能实例。</summary>
        public bool TryGetAbility<T>(out T ability) where T : CustomAbility
        {
            ability = null;
            if (!CustomAbilityTemplateManager.TryGetAbilityTemplate(typeof(T), out var abilityTemplate) ||
                !TryGetAbility(abilityTemplate.Id, out var instance) ||
                instance is not T ability2)
                return false;
            ability = ability2;

            return true;
        }

        /// <summary>按 Id 获取技能实例。</summary>
        public bool TryGetAbility(string id, out CustomAbility ability)
        {
            return AbilitiesById.TryGetValue(id, out ability);
        }

        /// <summary>按 SettingId 获取技能实例。</summary>
        public bool TryGetAbility(int settingId, out CustomAbility ability)
        {
            return AbilitiesBySettingId.TryGetValue(settingId, out ability);
        }

        /// <summary>按泛型类型触发技能。</summary>
        public void TriggerAbility<T>() where T : CustomAbility
        {
            if (!TryGetAbility(out T ability))
                return;
            ability.Triggered(Owner);
        }

        /// <summary>按 Id 触发技能。</summary>
        public void TriggerAbility(string id)
        {
            if (!TryGetAbility(id, out var ability))
                return;
            ability.Triggered(Owner);
        }

        /// <summary>按 SettingId 触发技能。</summary>
        public void TriggerAbility(int settingId)
        {
            if (!TryGetAbility(settingId, out var ability))
                return;
            ability.Triggered(Owner);
        }

        /// <summary>获取指定玩家的技能管理器。</summary>
        public static bool TryGetAbilityManager(Player player, out CustomAbilityManager manager)
        {
            return Managers.TryGetValue(player.NetworkId, out manager);
        }

        /// <summary>获取指定玩家的技能管理器（不存在时返回 null）。</summary>
        [CanBeNull]
        public static CustomAbilityManager GetAbilityManager(Player player)
        {
            Managers.TryGetValue(player.NetworkId, out var manager);
            return manager;
        }

        /// <summary>为指定玩家创建并初始化技能管理器。</summary>
        public static void AddManager(Player player)
        {
            var manager = new CustomAbilityManager(player);
            manager.Init();
        }

        /// <summary>移除并释放指定玩家的技能管理器。</summary>
        public static void RemoveManager(Player player)
        {
            var manager = GetAbilityManager(player);
            manager?.Dispose();
        }

        /// <summary>将管理器登记到全局表。</summary>
        public void Init()
        {
            Managers[Owner.NetworkId] = this;
        }

        /// <summary>清空该玩家的所有技能。</summary>
        public void ClearAll()
        {
            foreach (var ability in AbilitiesById.Values)
            {
                ability.RemoveAbility(Owner);
            }
            AbilitiesBySettingId.Clear();
            AbilitiesById.Clear();
        }

        /// <summary>按泛型类型添加技能实例。</summary>
        public CustomAbility AddAbility<T>() where T : CustomAbility, new()
        {
            if (!CustomAbilityTemplateManager.TryGetAbilityTemplate<T>(out var abilityTemplate))
                return null;
            return AddAbility(abilityTemplate);
        }

        /// <summary>按 Id 添加技能实例。</summary>
        public CustomAbility AddAbility(string id)
        {
            if (!CustomAbilityTemplateManager.TryGetAbilityTemplate(id, out var abilityTemplate))
                return null;
            return AddAbility(abilityTemplate);
        }

        /// <summary>
        /// 基于模板创建并挂载技能实例。
        /// 重复 Id 被阻止；按键技能的 SettingId 按「技能槽顺序」自动分配——
        /// 槽位即该技能被添加时在玩家技能列表中的落位下标
        /// （第一个加入的技能恒为 Ability1，其后依次递增）；
        /// 槽位耗尽时回落到模板声明值，仍冲突则找一个空闲键位。
        /// </summary>
        /// <returns>挂载成功的实例；因无可用 SettingId 被拒绝时为 <see langword="null"/>。</returns>
        private CustomAbility AddAbility(CustomAbility abilityTemplate)
        {
            if (TryGetAbility(abilityTemplate.Id, out var existedAbility))
            {
                Logger.Warn($"[CustomAbilityManager] 发现技能 {existedAbility.Id} 重复添加 已阻止");
                return existedAbility;
            }

            // 统一走模板的 CreateInstance，保证覆写了实例化逻辑的模板在两种添加方式下行为一致。
            var ability = abilityTemplate.CreateInstance();

            if (ability is IKeybind { SettingId: >= 0 } keybind)
            {
                // 按「被添加时的槽位」分配：槽位 = 该玩家此刻已挂载的技能数。
                var settingId = GetSettingIdForSlot(AbilitiesById.Count);

                if (settingId < 0 &&
                    !TryAssignSettingId(ability, keybind.SettingId, out settingId))
                {
                    Logger.Warn($"[CustomAbilityManager] 技能 {ability.Id} 无可用 SettingId 已拒绝添加");
                    return null;
                }

                ability.AssignedSettingId = settingId;
                AbilitiesBySettingId[settingId] = ability;
            }

            AbilitiesById[ability.Id] = ability;
            ability.AddAbility(Owner);
            return ability;
        }

        /// <summary>
        /// 按「技能槽顺序」把槽位下标映射为 SettingId：
        /// 第一个加入的技能落在 <see cref="FirstAbilitySlotId"/>（Ability1），其后依次递增。
        /// </summary>
        /// <param name="slotIndex">该技能在玩家技能列表中的落位下标（= 添加时的现有技能数）。</param>
        /// <returns>对应的 SettingId；槽位耗尽时为 <c>-1</c>。</returns>
        private static int GetSettingIdForSlot(int slotIndex)
            => slotIndex >= 0 && slotIndex < MaxAbilitySlots
                ? FirstAbilitySlotId + slotIndex
                : -1;

        /// <summary>
        /// 槽位耗尽后的兜底分配：模板 SettingId 未被占用时直接沿用；冲突时按升序从已下发的
        /// 按键设置里寻找第一个空闲 SettingId 并写入实例（仅改实例，模板不变）。
        /// <para>
        /// 正常情况下技能会走 <see cref="GetSettingIdForSlot"/> 的槽位分配，本方法只在
        /// 玩家技能数超过 <see cref="MaxAbilitySlots"/> 时作为最后手段。
        /// </para>
        /// </summary>
        /// <param name="ability">玩家专属技能实例。</param>
        /// <param name="templateSettingId">模板声明的 SettingId。</param>
        /// <param name="assignedSettingId">实际生效的 SettingId；失败为 <c>-1</c>。</param>
        /// <returns>成功确定 SettingId 时为 <see langword="true"/>。</returns>
        private bool TryAssignSettingId(CustomAbility ability, int templateSettingId, out int assignedSettingId)
        {
            if (!AbilitiesBySettingId.TryGetValue(templateSettingId, out var occupiedBy))
            {
                assignedSettingId = templateSettingId;
                return true;
            }

            Logger.Warn($"[CustomAbilityManager] 技能 {ability.Id} 的 SettingId {templateSettingId} 已被 {occupiedBy.Id} 占用，为该玩家实例改配");

            foreach (var candidate in EnumerateKeybindSettingIds())
            {
                if (AbilitiesBySettingId.ContainsKey(candidate))
                    continue;

                ability.AssignedSettingId = candidate;
                assignedSettingId = candidate;
                Logger.Warn($"[CustomAbilityManager] 技能 {ability.Id} 已改配 SettingId {candidate}（仅该玩家实例，模板未修改）");
                return true;
            }

            assignedSettingId = -1;
            return false;
        }

        /// <summary>枚举当前已下发给客户端的按键设置 SettingId（升序），作为改配候选池。</summary>
        /// <remarks>
        /// 排序策略：**优先返回已被技能模板声明的键位**（技能性质槽位，如站点自定义的
        /// <c>Ability1~4</c>），再返回其余已下发的按键设置（如投票键）。
        /// 这样冲突改配会尽量落在"技能槽"上，只有技能槽全部占满才会借用其它功能键位。
        /// </remarks>
        private static IEnumerable<int> EnumerateKeybindSettingIds()
        {
            var definedSettings = ServerSpecificSettingsSync.DefinedSettings;
            if (definedSettings is null)
                return [];

            var declaredByTemplates = new HashSet<int>();
            foreach (var template in CustomAbilityTemplateManager.AbilityTemplates)
            {
                if (template is IKeybind { SettingId: >= 0 } keybindTemplate)
                    declaredByTemplates.Add(keybindTemplate.SettingId);
            }

            var preferred = new List<int>();
            var fallback = new List<int>();
            foreach (var setting in definedSettings)
            {
                if (setting is not SSKeybindSetting { SettingId: >= 0 } keybindSetting)
                    continue;

                if (declaredByTemplates.Contains(keybindSetting.SettingId))
                    preferred.Add(keybindSetting.SettingId);
                else
                    fallback.Add(keybindSetting.SettingId);
            }

            preferred.Sort();
            fallback.Sort();
            preferred.AddRange(fallback);
            return preferred;
        }

        /// <summary>按 Id 移除技能。</summary>
        public void RemoveAbility(string id)
        {
            if (!TryGetAbility(id, out var ability))
                return;
            ability.RemoveAbility(Owner);
            AbilitiesById.Remove(ability.Id);
            if (ability.EffectiveSettingId >= 0)
                AbilitiesBySettingId.Remove(ability.EffectiveSettingId);
        }

        /// <summary>按泛型类型移除技能。</summary>
        public void RemoveAbility<T>() where T : CustomAbility
        {
            if (!TryGetAbility(out T ability))
                return;
            ability.RemoveAbility(Owner);
            AbilitiesById.Remove(ability.Id);
            if (ability.EffectiveSettingId >= 0)
                AbilitiesBySettingId.Remove(ability.EffectiveSettingId);
        }

        /// <summary>
        /// 处理玩家的按键设置输入，触发对应技能的 KeyDown / KeyUp。
        /// 通过 <see cref="EventMethodAttribute"/> 订阅到 <see cref="ServerSpecificSettingsSync.ServerOnSettingValueReceived"/>。
        /// </summary>
        [EventMethod(typeof(ServerSpecificSettingsSync), nameof(ServerSpecificSettingsSync.ServerOnSettingValueReceived))]
        public static void ProcessUserInput(ReferenceHub hub, ServerSpecificSettingBase settings)
        {
            if (settings is not SSKeybindSetting keybindSetting)
                return;
            var player = Player.Get(hub);
            if (GetAbilityManager(player) is not { } abilityManager)
                return;
            if (!abilityManager.TryGetAbility(settings.SettingId, out var ability))
                return;
            var keybindAbility = (IKeybind)ability;
            keybindAbility.SyncIsPressed = keybindSetting.SyncIsPressed;
            if (keybindAbility.SyncIsPressed)
                keybindAbility.KeyDown();
            else
                keybindAbility.KeyUp();
        }

        /// <summary>释放管理器：清空技能并从全局表移除。</summary>
        public void Dispose()
        {
            ClearAll();
            Managers.Remove(Owner.NetworkId);
        }
    }

