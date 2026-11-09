using System;
using System.Collections.Generic;
using System.Reflection;

using Astra.Abstractions;
using Astra.Systems.CustomTeamSystem;
using Astra.Attributes;
using Astra.Interfaces;
using Astra.Systems.CancellationTokenSource;
using Astra.Systems.CustomAbilitySystem;
using Astra.Systems.CustomRoleSystem;
using Astra.Systems.CustomWaveSystem;
using Astra.Systems.EventSystem;
using Astra.Systems.PatchSystem;
using Astra.Systems.ServerSpecificSettingSystem;

using HarmonyLib;

using LabApi.Events.CustomHandlers;
using LabApi.Features.Console;

namespace Astra
{
    /// <summary>
    /// 自动注册核心：扫描所有已加载插件程序集，按类型自动注册事件处理器、补丁、技能、设置与事件方法。
    /// 单个程序集或类型的注册失败只记录日志，不会中断其余组件的注册。
    /// </summary>
    public static class AutoRegister
    {
        /// <summary>框架统一使用的 Harmony 实例。</summary>
        private static Harmony HarmonyInstance { get; } = new("com.azure.framework");

        /// <summary>是否已完成注册（幂等标志）。</summary>
        private static bool Initialized { get; set; }

        /// <summary>
        /// 扫描并注册所有组件。幂等：重复调用返回 false。
        /// </summary>
        /// <param name="assemblies">要扫描的程序集集合。</param>
        /// <returns>首次注册成功返回 true，重复调用返回 false。</returns>
        public static bool RegisterAll(IEnumerable<Assembly> assemblies)
        {
            if (Initialized)
                return false;
            Initialized = true;

            var allTypes = new List<Type>();
            foreach (var assembly in assemblies)
            {
                try
                {
                    allTypes.AddRange(assembly.GetTypes());
                }
                catch (Exception ex)
                {
                    Logger.Error($"[AutoRegister] 无法枚举程序集 {assembly.FullName} 中的类型，已跳过。Exception: {ex.Message}");
                }
            }

            // Team / Wave / Role 先收集、后注册：遍历期间完成其余组件的注册，
            // 遍历结束后再按 Team → Wave → Role 的顺序统一处理，保证依赖就位。
            var teams = new List<Type>();
            var waves = new List<Type>();
            var roles = new List<Type>();
            var items = new List<Type>();

            foreach (var type in allTypes)
            {
                try
                {
                    RegisterType(type, teams, waves, roles, items);
                }
                catch (Exception ex)
                {
                    Logger.Error($"[AutoRegister] 注册类型 {type.FullName} 时发生异常，已跳过。Exception: {ex.Message}");
                    Logger.Error(ex.StackTrace);
                }
            }

            // ① Team：登记模板 → 实例化（波次需要解析阵营实例）
            foreach (var type in teams)
                CustomTeam.RegisterTemplate(type);
            CustomTeam.Initialize();

            // ② Wave：登记模板 → 实例化（角色需要波次实例才能反向登记）
            foreach (var type in waves)
                CustomWaveManager.RegisterTemplate(type);
            CustomWaveManager.Initialize();

            // ③ Item：登记模板 → 实例化（独立于角色/波次，供发放与事件溯源使用）
            foreach (var type in items)
                Systems.CustomItemSystem.CustomItemManager.RegisterTemplate(type);
            Systems.CustomItemSystem.CustomItemManager.Initialize();

            // ④ Role：注册模板并挂到其声明的波次
            foreach (var type in roles)
            {
                try
                {
                    CustomRoleTemplateManager.RegisterRole(type);
                    CustomWaveManager.AttachRole(type);
                }
                catch (Exception ex)
                {
                    Logger.Error($"[AutoRegister] 注册角色 {type.FullName} 时发生异常，已跳过。Exception: {ex.Message}");
                }
            }

            ServerSpecificSettingManager.SendToAll();
            return true;
        }

        /// <summary>注册单个类型（事件处理器 / 设置组 / 技能模板 / 补丁 / 事件方法）。</summary>
        /// <param name="teams">待注册的阵营类型（收集后统一处理）。</param>
        /// <param name="waves">待注册的波次类型（收集后统一处理）。</param>
        /// <param name="roles">待注册的角色类型（收集后统一处理）。</param>
        /// <param name="items">待注册的自定义物品类型（收集后统一处理）。</param>
        private static void RegisterType(Type type, List<Type> teams, List<Type> waves, List<Type> roles,
            List<Type> items)
        {
            var registeredEvent = false;
            if (typeof(IIdentifiable).IsAssignableFrom(type) && !type.IsAbstract)
            {
                if (typeof(CustomEventsHandler).IsAssignableFrom(type))
                {
                    registeredEvent = true;

                    // 角色 / 技能的事件监听改由各自模板管理器用「唯一模板实例」注册，
                    // 这里不再额外实例化，避免同一类型在启动期被构造两次。
                    if (!typeof(ICustomContent).IsAssignableFrom(type))
                        EventManager.Register(type);
                }

                if (typeof(ServerSpecificSettingBase).IsAssignableFrom(type))
                {
                    ServerSpecificSettingManager.Register(type);
                }

                if (typeof(CustomAbility).IsAssignableFrom(type))
                {
                    CustomAbilityTemplateManager.RegisterAbility(type);
                }

                if (typeof(CustomRole).IsAssignableFrom(type))
                {
                    roles.Add(type);   // 稍后统一注册（需波次实例就位）
                }

                if (typeof(CustomWave).IsAssignableFrom(type))
                {
                    waves.Add(type);   // 稍后统一注册（需阵营实例就位）
                }

                if (typeof(CustomTeam).IsAssignableFrom(type))
                {
                    teams.Add(type);   // 稍后统一注册（最先处理）
                }

                if (typeof(CustomItem).IsAssignableFrom(type))
                {
                    items.Add(type);   // 稍后统一注册
                }
            }

            if (!registeredEvent && type.GetCustomAttribute<ManagedEventAttribute>() is not null)
            {
                EventManager.Register(type);
            }

            if (type.GetCustomAttribute<ManagedPatchAttribute>() is not null &&
                (type.GetCustomAttribute<HarmonyPatch>() is not null || typeof(IManualPatch).IsAssignableFrom(type)))
            {
                PatchManager.PatchType(type, HarmonyInstance);
            }

            // 方法级补丁：无需外层类标 [ManagedPatch]，由方法上的 [ManagedPatchMethod] 自带目标信息。
            foreach (var patchMethod in type.GetMethods(
                         BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                if (patchMethod.GetCustomAttribute<PatchMethodAttribute>() is not null)
                    PatchManager.PatchMethod(patchMethod, HarmonyInstance);
            }

            // 静态 [EventMethod] 方法没有实例，只能在此按类型扫描注册；
            // 实例方法由 EnhancedEventsHandler 构造函数自我注册，这里必须跳过。
            foreach (var eventMethod in type.GetMethods(
                         BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                if (eventMethod.GetCustomAttribute<EventMethodAttribute>() is not null)
                    EventMethodManager.Register(eventMethod);
            }
        }

        /// <summary>
        /// 注销所有自动注册的资源（事件、设置、补丁、令牌、事件方法）。
        /// </summary>
        public static void UnregisterAll()
        {
            if (!Initialized)
                return;
            Initialized = false;
            EventManager.UnregisterAll();
            ServerSpecificSettingManager.UnregisterAll();
            PatchManager.UnpatchAll();
            TokenManager.CancelAll();
            EventMethodManager.UnregisterAll();
            CustomTeam.Clear();
            CustomWaveManager.Clear();
            Systems.CustomItemSystem.CustomItemManager.Clear();
        }
    }
}
